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
		private long _localVersion;
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
			var edit = new Edit(_localId, new Guid(), shadowCopy.ClientVersion, shadowCopy.ServerVersion, diff);
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
				if (remoteEdit is IClientChangeAck)
				{
					RemoveAcknowledgedEdits(_serverDocument, remoteEdit);
					continue;
				}
				if (remoteEdit is IEdit { Diff: { } } docEdit)
				{
					RemoveAcknowledgedEdits(_serverDocument, remoteEdit);
					ApplyEditToShadows(docEdit.ServerId, docEdit);
					Content = Document.Patch(Content, docEdit.Diff.Clone());
					var diff = _serverDocument.Shadow.Document?.Diff(Content);
					if (diff == null)
					{
						_serverDocument.DocActions.Enqueue(new ServerAck(_localId, Guid.Empty, _serverDocument.Shadow.ServerVersion));
					}
					else
					{
						var editToSendToClient = new Edit(docEdit.ClientId, _localId, _serverDocument.Shadow.ClientVersion,
							_serverDocument.Shadow.ServerVersion, diff
						);
						_serverDocument.DocActions.Enqueue(editToSendToClient);
					}
					OnContentChanged?.Invoke();
				}
				if (remoteEdit is Reset resetEvent)
				{
					_serverDocument.Shadow.ServerVersion = _serverDocument.Backup.ServerVersion = resetEvent.ServerVersion;
					_serverDocument.Shadow.ClientVersion = _serverDocument.Backup.ClientVersion = resetEvent.ClientVersion;
					Content = resetEvent.Content.Clone();
					_serverDocument.Shadow.Document = resetEvent.Content.Clone();
					_serverDocument.Backup.Document = resetEvent.Content.Clone();
					_serverDocument.DocActions.Enqueue(new ServerAck(_localId, Guid.Empty, _serverDocument.Shadow.ServerVersion));
					OnContentChanged?.Invoke();
				}
			}
			_changeCommunicator.SendClientEdits(_serverDocument.DocActions);
		}
		private static void RemoveAcknowledgedEdits(DiffSyncDocument diffSyncDoc, IDocumentAction docAction)
		{
			if (diffSyncDoc == null)
				return;
			long clientVersion;
			if (docAction is IEdit editAction)
			{
				clientVersion = editAction.ClientVersion;
			}
			else if (docAction is IClientChangeAck clientAck)
			{
				clientVersion = clientAck.ClientVersion;
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
				// remove any previous ServerAck from the queue as we'll likely be sending a new one
				// also remove any edits which have been acknowledged
				if (docActions[i] is ServerAck || edit!.ClientVersion <= clientVersion)
				{
					docActions.RemoveAt(i);
					continue;
				}
				diffSyncDoc.DocActions.Enqueue(docActions[i]);
				break;
			}
		}
		private void ApplyEditToShadows(Guid shadowId, IEdit docEdit)
		{
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

	public class ServerAck : IServerChangeAck
	{
		public ServerAck(Guid clientId, Guid serverId, long shadowServerVersion)
		{
			ClientId = clientId;
			ServerId = serverId;
			ServerVersion = shadowServerVersion;
		}

		public Guid ClientId { get; }
		public Guid ServerId { get; }
		public long ServerVersion { get; }
	}
}