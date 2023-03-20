using System;
using System.Collections.Generic;
using Newtonsoft.Json.Linq;

namespace DiffSync
{
	public interface IChangeCommunicator
	{
		Guid RemoteGuid { get; }
		void SendEdits(Queue<IDocumentAction> edits);
		void RequestDump(Guid clientGuid);
	}
}