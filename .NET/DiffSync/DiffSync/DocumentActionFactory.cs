using System;
using System.Collections.Generic;
using System.Text;

namespace DiffSync
{
	public class DocumentActionFactory
	{
		public static IDocumentAction CreateClientAck(Guid clientId, Guid serverId, long clientVersion)
		{
			return new DocumentAction
			{
				Type = DocActionType.ClientAck,
				ClientId = clientId,
				ServerId = serverId,
				ClientVersion = clientVersion
			};
		}
		public static IDocumentAction CreateServerAck(Guid clientId, Guid serverId, long serverVersion)
		{
			return new DocumentAction
			{
				Type = DocActionType.ServerAck,
				ClientId = clientId,
				ServerId = serverId,
				ServerVersion = serverVersion
			};
		}
		public static IDocumentAction CreateReset(Guid clientGuid, Guid serverGuid, long clientVersion, long serverVersion, Document doc)
		{
			return new DocumentAction
			{
				Type = DocActionType.Reset,
				ClientId = clientGuid,
				ServerId = serverGuid,
				ClientVersion = clientVersion,
				ServerVersion = serverVersion,
				Content = doc
			};
		}
		public static IDocumentAction CreateEdit(Guid clientId, Guid serverId, long clientVersion, long serverVersion, Diff diff)
		{
			return new DocumentAction
			{
				Type = DocActionType.Edit,
				ClientId = clientId,
				ServerId = serverId,
				ClientVersion = clientVersion,
				ServerVersion = serverVersion,
				Diff = diff
			};
		}

		private class DocumentAction: IDocumentAction
		{
			public DocActionType Type { get; set; }
			public Guid ClientId { get; set; }
			public Guid ServerId { get; set; }
			public long ClientVersion { get; set; }
			public long ServerVersion { get; set; }
			public Diff? Diff { get; set; }

			public Document Content { get; set; }
		}
	}
}
