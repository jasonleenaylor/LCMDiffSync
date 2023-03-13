using System;
using System.Collections.Generic;
using JsonDiffPatchDotNet;
using Newtonsoft.Json.Linq;

namespace DiffSync
{
	public class DocumentManager
	{
		private JsonDiffPatch differPatcher = new JsonDiffPatch();
		public VersionedDocument Shadow; // internal for unit testing
		private VersionedDocument _backup;

		private Stack<IEdit> _edits;
		private IChangeCommunicator _changeCommunicator;
		private Guid _clientId;

		public void ApplyLocalChange(JObject updatedDoc)
		{
			VersionedDocument shadowCopy = Shadow; // Deal with locking later
			var diff = differPatcher.Diff(updatedDoc, shadowCopy.Document);
			var edit = new Edit { ClientVersion = shadowCopy.ClientVersion, ServerVersion = shadowCopy.ServerVersion, Diff = diff, ClientId = _clientId};
			_edits.Push(edit);
			Shadow.Document = updatedDoc;
			Shadow.ClientVersion++;
			_changeCommunicator.SendEdits(_edits);
		}
		public void ApplyRemoteChanges(Stack<IEdit> remoteEdits)
		{
		}

		private void RemoveAcknowledgedEdit(int clientVersionFromServer)
		{

		}

		public void Initialize(JObject contents, IChangeCommunicator changeCommunicator, Guid clientId)
		{
			var contentsClone = (JObject)contents.DeepClone();
			Shadow = new VersionedDocument { ClientVersion = 0, ServerVersion = -1, Document = contentsClone };
			_backup = new VersionedDocument { ClientVersion = 0, ServerVersion = -1, Document = contentsClone };
			_edits = new Stack<IEdit>();
			_changeCommunicator = changeCommunicator;
			_clientId = clientId;
		}
	}

	public class VersionedDocument : ICloneable
	{
		public long ClientVersion { get; set; }
		public long ServerVersion { get; set; }
		public JObject Document { get; set; }
		public object Clone()
		{
			return new VersionedDocument
			{
				Document = Document?.DeepClone() as JObject,
				ClientVersion = ClientVersion,
				ServerVersion = ServerVersion
			};
		}
	}
}
