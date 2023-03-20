using System;
using System.Collections.Generic;
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
		private Dictionary<Guid, DiffSyncDocument> clientDocuments = new Dictionary<Guid, DiffSyncDocument>();
		private IChangeCommunicator _changeCommunicator;
		private Guid _localId;
		private long _localVersion;
		public JObject Content { get; set; }

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
				if (remoteEdit is IEdit docEdit)
				{
					var diffSyncDoc = clientDocuments[remoteEdit.ClientId];
					if (docEdit.ServerVersion != diffSyncDoc.Shadow.ServerVersion &&
					    docEdit.ServerVersion == diffSyncDoc.Backup.ServerVersion)
					{
						// Client did not receive last response, roll back the shadow
						diffSyncDoc.Shadow.Document = (JObject?)diffSyncDoc.Backup.Document?.DeepClone();
						diffSyncDoc.Shadow.ServerVersion = diffSyncDoc.Backup.ServerVersion;
						diffSyncDoc.Edits.Clear();
					}
					if (diffSyncDoc.Shadow.ClientVersion == docEdit.ClientVersion &&
					    diffSyncDoc.Shadow.ServerVersion == docEdit.ServerVersion)
					{
						ApplyClientEditToShadows(docEdit.ClientId, docEdit);
						Content = (JObject)differPatcher.Patch(Content, docEdit.Diff); // Should be fuzzy patch
						var editToSendToClient = new Edit
						{
							ClientId = docEdit.ClientId, ServerId = _localId, ClientVersion = docEdit.ClientVersion + 1,
							ServerVersion = docEdit.ServerVersion,
							Diff = (JObject)differPatcher.Diff(clientDocuments[docEdit.ClientId].Shadow.Document, Content)
						};
						diffSyncDoc.Edits.Enqueue(editToSendToClient);
						if (!clientEditsProcessed.TryGetValue(remoteEdit.ClientId, out var hasDiff))
							hasDiff = false;
						clientEditsProcessed[remoteEdit.ClientId] = hasDiff || editToSendToClient.Diff != null;
					}
				}
			}

			foreach (var clientId in clientEditsProcessed)
			{
				_changeCommunicator.SendEdits(clientDocuments[clientId.Key].Edits);
				// There have been changes to the server text - so update our shadow
				if (clientId.Value)
				{
					clientDocuments[clientId.Key].Shadow.Document = (JObject)Content.DeepClone();
					clientDocuments[clientId.Key].Shadow.ServerVersion++;
				}
			}
		}

		private void RemoveAcknowledgedEdit(int clientVersionFromServer)
		{

		}

		public void InitializeServer(JObject contents, IChangeCommunicator changeCommunicator, Guid myId)
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
			dumpStack.Enqueue(new Edit
			{
				ClientId = clientGuid,
				ClientVersion = 0,
				ServerId = _localId,
				Diff = differPatcher.Diff(null, Content),
				ServerVersion = _localVersion
			});
			// Create the backup and shadow for this client from the current content
			clientDocuments[clientGuid] = new DiffSyncDocument(
				new VersionedDocument
					{ ClientVersion = 0, ServerVersion = _localVersion, Document = (JObject)Content.DeepClone() },
				new VersionedDocument
					{ ClientVersion = 0, ServerVersion = _localVersion, Document = (JObject)Content.DeepClone() },
				dumpStack);
	
			_changeCommunicator.SendEdits(dumpStack);
		}

		private void BackupShadow(Guid shadowId)
		{
			clientDocuments[shadowId].Backup =(VersionedDocument)clientDocuments[shadowId].Shadow.Clone();
		}
	}
}
