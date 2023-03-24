using System;
using System.Collections.Generic;
using Newtonsoft.Json.Linq;

namespace DiffSync
{
	public interface IClientToServerCommunicator
	{
		void SendClientEdits(Queue<IDocumentAction> edits);
		void RequestDump(Guid clientGuid);
	}

	public interface IServerToClientCommunicator
	{
		void SendServerEdits(Guid clientId, Queue<IDocumentAction> edits);
	}
}