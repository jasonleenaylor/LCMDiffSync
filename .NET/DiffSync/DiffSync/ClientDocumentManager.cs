using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;

namespace DiffSync
{

	/// <summary>
	/// For use on a server to manage the client documents and their edit stacks
	/// </summary>
	public class ClientDocumentManager
	{
		public Dictionary<Guid, DiffSyncDocument> clientDocuments = new Dictionary<Guid, DiffSyncDocument>();
		private IServerToClientCommunicator _changeCommunicator;
		private Guid _localId;
		public Document Content { get; set; }
		public delegate void ContentChanged();
		public event ContentChanged OnContentChanged;

		public VersionedDocument GetShadow(Guid client)
		{
			return clientDocuments[client].Shadow;
		}
		public int GetClientCount()
		{
			return clientDocuments.Count;
		}

		private void ApplyClientEditToShadows(Guid clientId, IDocumentAction docEdit)
		{
			if (docEdit.Type != DocActionType.Edit)
			{
				throw new ArgumentException("Wrong IDocumentAction type sent for edit", nameof(docEdit));
			}
			if (!clientDocuments.TryGetValue(clientId, out var diffSyncDoc))
			{
				clientDocuments[clientId] = diffSyncDoc = new DiffSyncDocument();
			}

			if (diffSyncDoc.Shadow.ClientVersion > docEdit.ClientVersion)
			{
				// We have already seen this edit (probably a dropped server packet)
				return;
			}
			if (diffSyncDoc.Shadow.ClientVersion == docEdit.ClientVersion)
			{
				diffSyncDoc.Shadow.ClientVersion = docEdit.ClientVersion + 1;
				diffSyncDoc.Shadow.ServerVersion = docEdit.ServerVersion;
				diffSyncDoc.Shadow.Document = diffSyncDoc.Shadow.Document.Patch(docEdit.Diff.Clone());
				BackupShadow(clientId);
			}
		}

		public void ApplyRemoteChangesToServer(Queue<IDocumentAction> remoteEdits)
		{
			var clientEditsProcessed = new Dictionary<Guid, bool>();
			foreach (var remoteEdit in remoteEdits)
			{
				var diffSyncDoc = clientDocuments[remoteEdit.ClientId];
				switch (remoteEdit.Type)
				{
					case DocActionType.ServerAck:
					{
						RemoveAcknowledgedEdits(diffSyncDoc, remoteEdit);
						continue;
					}
					case DocActionType.Edit:
					{
							RemoveAcknowledgedEdits(diffSyncDoc, remoteEdit);
							if (remoteEdit.ServerVersion != diffSyncDoc.Shadow.ServerVersion &&
							    remoteEdit.ServerVersion == diffSyncDoc.Backup.ServerVersion)
							{
								// Client did not receive last response, roll back the shadow
								diffSyncDoc.Shadow.Document = diffSyncDoc.Backup.Document?.Clone();
								diffSyncDoc.Shadow.ServerVersion = diffSyncDoc.Backup.ServerVersion;
								diffSyncDoc.DocActions.Clear();
							}
							if (diffSyncDoc.Shadow.ClientVersion == remoteEdit.ClientVersion &&
								 diffSyncDoc.Shadow.ServerVersion == remoteEdit.ServerVersion)
							{
								ApplyClientEditToShadows(remoteEdit.ClientId, remoteEdit);
								Content = Content.Patch(remoteEdit.Diff); // Should be fuzzy patch
								var diff = clientDocuments[remoteEdit.ClientId].Shadow.Document?.Diff(Content);

								// REVIEW: Is there a better way to handle this situation?
								// Right now it covers both situations where we have made no edits, or where we both made the same edit
								if (diff == null)
								{
									diffSyncDoc.DocActions.Enqueue(DocumentActionFactory.CreateClientAck(remoteEdit.ClientId, _localId, remoteEdit.ClientVersion));
								}
								else
								{
									diffSyncDoc.DocActions.Enqueue(DocumentActionFactory.CreateEdit(remoteEdit.ClientId, _localId, remoteEdit.ClientVersion + 1,
										remoteEdit.ServerVersion, clientDocuments[remoteEdit.ClientId].Shadow.Document.Diff(Content)));
								}

								if (!clientEditsProcessed.TryGetValue(remoteEdit.ClientId, out var hasDiff))
									hasDiff = false;
								clientEditsProcessed[remoteEdit.ClientId] = hasDiff || diff != null;
								OnContentChanged?.Invoke();
							}
							else if (diffSyncDoc.Shadow.ClientVersion > remoteEdit.ClientVersion)
							{
								// We already saw this edit, and can safely drop it
							}
							else
							{
								throw new ApplicationException($"versions drifted: c{diffSyncDoc.Shadow.ClientVersion} vs c{remoteEdit.ClientVersion} and s{diffSyncDoc.Shadow.ServerVersion} s{remoteEdit.ServerVersion}");
							}

							break;
					}
					default:
						throw new ApplicationException("Unhandled IDocumentAction type");
				}
			}

			foreach (var clientId in clientEditsProcessed)
			{
				_changeCommunicator.SendServerEdits(clientId.Key, clientDocuments[clientId.Key].DocActions);
				// There have been changes to the server text - so update our shadow
				if (clientId.Value)
				{
					clientDocuments[clientId.Key].Shadow.Document = Content.Clone();
					clientDocuments[clientId.Key].Shadow.ServerVersion++;
				}
			}
		}

		private static void RemoveAcknowledgedEdits(DiffSyncDocument diffSyncDoc, IDocumentAction docAction)
		{
			if (diffSyncDoc == null)
				return;

			long serverVersion;
			if (docAction.Type == DocActionType.Edit || docAction.Type == DocActionType.ServerAck)
			{
				serverVersion = docAction.ServerVersion;
			}
			else
			{
				throw new ArgumentException("Unsupported IDocumentAction for RemoveAcknowledgedEdits");
			}

			var docActions = diffSyncDoc.DocActions.ToList();
			diffSyncDoc.DocActions.Clear();
			for (var i = 0; i < docActions.Count;)
			{
				var action = docActions[i];
				// remove any previous ServerAck from the queue as we'll likely be sending a new one
				// also remove any edits which have been acknowledged
				if (action.Type == DocActionType.ServerAck || action.Type == DocActionType.Edit && action.ServerVersion <= serverVersion
				                                           || action.Type == DocActionType.Reset && action.ServerVersion <= serverVersion)
				{
					docActions.RemoveAt(i);
					continue;
				}
				diffSyncDoc.DocActions.Enqueue(docActions[i]);
				break;
			}
		}

		public void InitializeServer(Document contents, IServerToClientCommunicator changeCommunicator, Guid myId)
		{
			Content = contents.Clone();
			_localId = myId;
			_changeCommunicator = changeCommunicator;
		}

		// Used to add a new client
		public void SyncClient(Guid clientGuid)
		{
			var dumpStack = new Queue<IDocumentAction>();
			dumpStack.Enqueue(DocumentActionFactory.CreateReset(clientGuid, _localId, 0, 0, Content.Clone()));
			// Create the backup and shadow for this client from the current content
			clientDocuments[clientGuid] = new DiffSyncDocument(
				new VersionedDocument
					{ ClientVersion = 0, ServerVersion = 0, Document = Content.Clone() },
				new VersionedDocument
					{ ClientVersion = 0, ServerVersion = 0, Document = Content.Clone() },
				dumpStack);
	
			_changeCommunicator.SendServerEdits(clientGuid, dumpStack);
			OnContentChanged?.Invoke();
		}

		private void BackupShadow(Guid shadowId)
		{
			clientDocuments[shadowId].Backup = (VersionedDocument)clientDocuments[shadowId].Shadow.Clone();
		}
	}
}
