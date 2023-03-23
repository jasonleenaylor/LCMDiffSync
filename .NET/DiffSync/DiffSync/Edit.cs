using System;
using Newtonsoft.Json.Linq;

namespace DiffSync
{
	public class Edit : IEdit
	{
		public Edit(Guid clientId, Guid serverId, long clientVersion, long serverVersion, JToken diff)
		{
			ClientId = clientId;
			ServerId = serverId;
			ClientVersion = clientVersion;
			ServerVersion = serverVersion;
			Diff = diff;
		}

		public long ServerVersion { get; set; }
		public JToken? Diff { get; set; }
		public long ClientVersion { get; set; }
		public Guid ClientId { get; set; }
		public Guid ServerId { get; set; }
	}
}