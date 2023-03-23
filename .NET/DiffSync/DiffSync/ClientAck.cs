using System;

namespace DiffSync
{
	public class ClientAck : IClientChangeAck
	{
		public ClientAck(Guid clientId, Guid serverId, long clientVersion)
		{
			ClientId = clientId;
			ServerId = serverId;
			ClientVersion = clientVersion;
		}
		public Guid ClientId { get; }
		public Guid ServerId { get; }
		public long ClientVersion { get; }
	}
}