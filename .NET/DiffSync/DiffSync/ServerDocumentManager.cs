using System;
using System.Collections.Generic;
using JsonDiffPatchDotNet;
using Newtonsoft.Json.Linq;

namespace DiffSync
{
	/// <summary>
	/// For use on a client to manage the server document synchronization and edit stacks
	/// </summary>
	public class ServerDocumentManager
	{
		private JsonDiffPatch _differPatcher = new JsonDiffPatch(new Options { MinEfficientTextDiffLength = 2 });
		private DiffSyncDocument? _serverDocument;
		private IChangeCommunicator _changeCommunicator;
		private Guid _localId;
		private long _localVersion;
		public JObject Content { get; set; }

		public VersionedDocument GetShadow(Guid client)
		{
			return _serverDocument.Shadow;
		}

		public void ApplyLocalChange()
		{
			VersionedDocument shadowCopy = _serverDocument.Shadow; // Deal with locking later
			var diff = _differPatcher.Diff(Content, shadowCopy.Document);
			var edit = new Edit { ClientVersion = shadowCopy.ClientVersion, ServerVersion = shadowCopy.ServerVersion, Diff = diff, ClientId = _localId };
			_serverDocument.Edits.Enqueue(edit);
			_serverDocument.Shadow.Document = (JObject)Content.DeepClone();
			_serverDocument.Shadow.ClientVersion++;
			_changeCommunicator.SendEdits(_serverDocument.Edits);
		}

		public void ApplyRemoteChangesToClient(Queue<IDocumentAction> remoteEdits)
		{
			foreach (var remoteEdit in remoteEdits)
			{
				if (remoteEdit is IEdit docEdit)
				{
					Content = (JObject)_differPatcher.Patch(Content, docEdit.Diff.DeepClone());
					ApplyEditToShadows(docEdit.ServerId, docEdit);
				}
			}
		}

		private void ApplyEditToShadows(Guid shadowId, IEdit docEdit)
		{
			if (_serverDocument == null)
			{
				_serverDocument = new DiffSyncDocument();
			}
			_serverDocument.Shadow.ClientVersion = docEdit.ClientVersion;
			_serverDocument.Shadow.ServerVersion = docEdit.ServerVersion;
			_serverDocument.Shadow.Document = (JObject)_differPatcher.Patch(_serverDocument.Shadow.Document, docEdit.Diff.DeepClone());
			while (_serverDocument.Edits.TryPeek(out var top))
			{
				if (top.ClientVersion <= docEdit.ClientVersion)
				{
					_serverDocument.Edits.Dequeue();
				}
			}
			BackupShadow(shadowId);
		}

		private void RemoveAcknowledgedEdit(int clientVersionFromServer)
		{

		}

		public void InitFromServer(IChangeCommunicator communicator, Guid clientGuid)
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