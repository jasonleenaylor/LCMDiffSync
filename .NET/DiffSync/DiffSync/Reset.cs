using System;
using Newtonsoft.Json.Linq;

namespace DiffSync
{
	public class Reset : IDocumentAction
	{
		public Reset(Guid clientId, Guid serverId, long clientVersion, long serverVersion, Document contents)
		{
			ClientId = clientId;
			ServerId = serverId;
			ClientVersion = clientVersion;
			ServerVersion = serverVersion;
			Content = contents;
		}
		public Document Content { get; set; }

		public long ClientVersion { get; }

		public long ServerVersion { get; }

		public Guid ClientId { get; }
		public Guid ServerId { get; }
	}
}