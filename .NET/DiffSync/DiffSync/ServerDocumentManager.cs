using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;

namespace DiffSync
{
	/// <summary>
	/// For use on a client to manage the server document synchronization and edit stacks
	/// </summary>
	public class ServerDocumentManager
	{
		public DiffSyncDocument? _serverDocument;
		private IClientToServerCommunicator _changeCommunicator;
		private Guid _localId = Guid.NewGuid();
		public Document? Content { get; set; }

		public delegate void ContentChanged();
		public event ContentChanged OnContentChanged;

		public VersionedDocument GetShadow(Guid client)
		{
			return _serverDocument.Shadow;
		}

		public void ApplyLocalChange()
		{
			VersionedDocument shadowCopy = _serverDocument.Shadow; // Deal with locking later
			var diff = shadowCopy.Document?.Diff(Content);
			var edit = DocumentActionFactory.CreateEdit(_localId, new Guid(), shadowCopy.ClientVersion, shadowCopy.ServerVersion, diff);
			_serverDocument.DocActions.Enqueue(edit);
			_serverDocument.Shadow.Document = Content.Clone();
			_serverDocument.Shadow.ClientVersion++;
			_changeCommunicator.SendClientEdits(_serverDocument.DocActions);
			OnContentChanged?.Invoke();
		}

		public void ApplyRemoteChangesToClient(Queue<IDocumentAction> remoteEdits)
		{
			_serverDocument ??= new DiffSyncDocument();
			foreach (var remoteEdit in remoteEdits)
			{
				switch (remoteEdit.Type)
				{
					case DocActionType.ClientAck:
					{
						RemoveAcknowledgedEdits(_serverDocument, remoteEdit);
						continue;
					}
					case DocActionType.Edit:
					{
						RemoveAcknowledgedEdits(_serverDocument, remoteEdit);
						ApplyEditToShadows(remoteEdit.ServerId, remoteEdit);
						Content = Document.Patch(Content, remoteEdit.Diff.Clone());
						var diff = _serverDocument.Shadow.Document?.Diff(Content);
						// REVIEW: Is there a better way to handle this situation?
						// Right now it covers both situations where we have made no edits, or where we both made the same edit
						if (diff == null)
						{
							_serverDocument.DocActions.Enqueue(DocumentActionFactory.CreateServerAck(_localId, Guid.Empty, _serverDocument.Shadow.ServerVersion));
						}
						else
						{
							var editToSendToClient = DocumentActionFactory.CreateEdit(remoteEdit.ClientId, _localId, _serverDocument.Shadow.ClientVersion,
								_serverDocument.Shadow.ServerVersion, diff
							);
							_serverDocument.DocActions.Enqueue(editToSendToClient);
							OnContentChanged?.Invoke();
						}
						break;
					}
					case DocActionType.Reset:
					{
						_serverDocument.Shadow.ServerVersion = _serverDocument.Backup.ServerVersion = remoteEdit.ServerVersion;
						_serverDocument.Shadow.ClientVersion = _serverDocument.Backup.ClientVersion = remoteEdit.ClientVersion;
						Content = remoteEdit.Content.Clone();
						_serverDocument.Shadow.Document = remoteEdit.Content.Clone();
						_serverDocument.Backup.Document = remoteEdit.Content.Clone();
						_serverDocument.DocActions.Enqueue(DocumentActionFactory.CreateServerAck(_localId, remoteEdit.ServerId, _serverDocument.Shadow.ServerVersion));
						OnContentChanged?.Invoke();
						break;
					}

				}
			}
			_changeCommunicator.SendClientEdits(_serverDocument.DocActions);
		}
		private static void RemoveAcknowledgedEdits(DiffSyncDocument diffSyncDoc, IDocumentAction docAction)
		{
			if (diffSyncDoc == null)
				return;
			long clientVersion;
			switch (docAction.Type)
			{
				case DocActionType.Edit:
				case DocActionType.ClientAck:
				{
					clientVersion = docAction.ClientVersion;
					break;
				}
				default:
					throw new ArgumentException("Unsupported IDocumentAction for RemoveAcknowledgedEdits");
			}

			var docActions = diffSyncDoc.DocActions.ToList();
			diffSyncDoc.DocActions.Clear();
			for (var i = 0; i < docActions.Count;)
			{
				var edit = docActions[i];
				// remove any previous ServerAck from the queue as we'll likely be sending a new one
				// also remove any edits which have been acknowledged
				if (docActions[i].Type == DocActionType.ServerAck || docAction.Type == DocActionType.Edit && docAction.ClientVersion <= clientVersion)
				{
					docActions.RemoveAt(i);
					continue;
				}
				diffSyncDoc.DocActions.Enqueue(docActions[i]);
				break;
			}
		}
		private void ApplyEditToShadows(Guid shadowId, IDocumentAction docEdit)
		{
			if (docEdit.Type != DocActionType.Edit)
			{
				throw new ArgumentException("ApplyEdit called with something that isn't an edit", nameof(docEdit));
			}
			_serverDocument.Shadow.ClientVersion = docEdit.ClientVersion;
			_serverDocument.Shadow.ServerVersion = docEdit.ServerVersion + 1;
			_serverDocument.Shadow.Document = Document.Patch(_serverDocument.Shadow.Document, docEdit.Diff.Clone());
			BackupShadow(shadowId);
		}

		public Guid Guid => _localId;

		public void InitFromServer(IClientToServerCommunicator communicator, Guid clientGuid)
		{
			_changeCommunicator = communicator;
			communicator.RequestDump(clientGuid);
		}

		private void BackupShadow(Guid shadowId)
		{
			_serverDocument.Backup = (VersionedDocument)_serverDocument.Shadow.Clone();
		}
	}
}