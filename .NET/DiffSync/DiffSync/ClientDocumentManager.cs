using System;
using System.Collections.Generic;
using System.Linq;
using JsonDiffPatchDotNet;
using Newtonsoft.Json.Linq;

namespace DiffSync
{

	/// <summary>
	/// For use on a server to manage the client documents and their edit stacks
	/// </summary>
	public class ClientDocumentManager
	{
		private JsonDiffPatch differPatcher = new JsonDiffPatch(new Options {MinEfficientTextDiffLength = 2});
		public Dictionary<Guid, DiffSyncDocument> clientDocuments = new Dictionary<Guid, DiffSyncDocument>();
		private IServerToClientCommunicator _changeCommunicator;
		private Guid _localId;
		private long _localVersion;
		public JObject Content { get; set; }
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

		private void ApplyClientEditToShadows(Guid clientId, IEdit docEdit)
		{
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
				diffSyncDoc.Shadow.Document = (JObject)differPatcher.Patch(diffSyncDoc.Shadow.Document, docEdit.Diff?.DeepClone());
				BackupShadow(clientId);
			}
		}

		public void ApplyRemoteChangesToServer(Queue<IDocumentAction> remoteEdits)
		{
			var clientEditsProcessed = new Dictionary<Guid, bool>();
			foreach (var remoteEdit in remoteEdits)
			{
				var diffSyncDoc = clientDocuments[remoteEdit.ClientId];
				if (remoteEdit is IServerChangeAck)
				{
					RemoveAcknowledgedEdits(diffSyncDoc, remoteEdit);
					continue;
				}
				if (remoteEdit is IEdit docEdit)
				{
					RemoveAcknowledgedEdits(diffSyncDoc, docEdit);
					if (docEdit.ServerVersion != diffSyncDoc.Shadow.ServerVersion &&
					    docEdit.ServerVersion == diffSyncDoc.Backup.ServerVersion)
					{
						// Client did not receive last response, roll back the shadow
						diffSyncDoc.Shadow.Document = (JObject?)diffSyncDoc.Backup.Document?.DeepClone();
						diffSyncDoc.Shadow.ServerVersion = diffSyncDoc.Backup.ServerVersion;
						diffSyncDoc.DocActions.Clear();
					}
					if (diffSyncDoc.Shadow.ClientVersion == docEdit.ClientVersion &&
					    diffSyncDoc.Shadow.ServerVersion == docEdit.ServerVersion)
					{
						ApplyClientEditToShadows(docEdit.ClientId, docEdit);
						Content = (JObject)differPatcher.Patch(Content, docEdit.Diff); // Should be fuzzy patch
						var diff = (JObject?)differPatcher.Diff(clientDocuments[docEdit.ClientId].Shadow.Document, Content);

						if (diff == null)
						{
							diffSyncDoc.DocActions.Enqueue(new ClientAck(docEdit.ClientId, _localId, docEdit.ClientVersion));
						}
						else
						{
							diffSyncDoc.DocActions.Enqueue(new Edit(docEdit.ClientId, _localId, docEdit.ClientVersion + 1,
								docEdit.ServerVersion, (JObject)differPatcher.Diff(clientDocuments[docEdit.ClientId].Shadow.Document, Content)));
						} 
						
						if (!clientEditsProcessed.TryGetValue(remoteEdit.ClientId, out var hasDiff))
							hasDiff = false;
						clientEditsProcessed[remoteEdit.ClientId] = hasDiff || diff != null;
						OnContentChanged?.Invoke();
					}
					else if(diffSyncDoc.Shadow.ClientVersion > docEdit.ClientVersion)
					{
						// We already saw this edit, and can safely drop it
					}
					else
					{
						throw new ApplicationException($"versions drifted: c{diffSyncDoc.Shadow.ClientVersion} vs c{docEdit.ClientVersion} and s{diffSyncDoc.Shadow.ServerVersion} s{docEdit.ServerVersion}");
					}
				}
			}

			foreach (var clientId in clientEditsProcessed)
			{
				_changeCommunicator.SendServerEdits(clientDocuments[clientId.Key].DocActions);
				// There have been changes to the server text - so update our shadow
				if (clientId.Value)
				{
					clientDocuments[clientId.Key].Shadow.Document = (JObject)Content.DeepClone();
					clientDocuments[clientId.Key].Shadow.ServerVersion++;
				}
			}
		}

		private static void RemoveAcknowledgedEdits(DiffSyncDocument diffSyncDoc, IDocumentAction docAction)
		{
			if (diffSyncDoc == null)
				return;

			long serverVersion;
			if (docAction is IEdit editAction)
			{
				serverVersion = editAction.ServerVersion;
			}
			else if (docAction is IServerChangeAck clientAck)
			{
				serverVersion = clientAck.ServerVersion;
			}
			else
			{
				throw new ArgumentException("Unsupported IDocumentAction for RemoveAcknowledgedEdits");
			}

			var docActions = diffSyncDoc.DocActions.ToList();
			diffSyncDoc.DocActions.Clear();
			for (var i = 0; i < docActions.Count;)
			{
				var edit = docActions[i] as IEdit;
				var reset = docActions[i] as Reset;
				// remove any previous ServerAck from the queue as we'll likely be sending a new one
				// also remove any edits which have been acknowledged
				if (docActions[i] is ServerAck || edit != null && edit.ServerVersion <= serverVersion || reset != null && reset.ServerVersion <= serverVersion)
				{
					docActions.RemoveAt(i);
					continue;
				}
				diffSyncDoc.DocActions.Enqueue(docActions[i]);
				break;
			}
		}

		public void InitializeServer(JObject contents, IServerToClientCommunicator changeCommunicator, Guid myId)
		{
			Content = (JObject)contents.DeepClone();
			_localVersion = 0;
			_localId = myId;
			_changeCommunicator = changeCommunicator;
		}

		// Used to add a new client
		public void SyncClient(Guid clientGuid)
		{
			var dumpStack = new Queue<IDocumentAction>();
			dumpStack.Enqueue(new Reset(clientGuid, _localId, 0, _localVersion, (JObject)Content.DeepClone()));
			// Create the backup and shadow for this client from the current content
			clientDocuments[clientGuid] = new DiffSyncDocument(
				new VersionedDocument
					{ ClientVersion = 0, ServerVersion = _localVersion, Document = (JObject)Content.DeepClone() },
				new VersionedDocument
					{ ClientVersion = 0, ServerVersion = _localVersion, Document = (JObject)Content.DeepClone() },
				dumpStack);
	
			_changeCommunicator.SendServerEdits(dumpStack);
			OnContentChanged?.Invoke();
		}

		private void BackupShadow(Guid shadowId)
		{
			clientDocuments[shadowId].Backup =(VersionedDocument)clientDocuments[shadowId].Shadow.Clone();
		}
	}
}
