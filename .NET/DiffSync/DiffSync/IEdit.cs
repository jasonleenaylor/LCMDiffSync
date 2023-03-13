using System;
using Newtonsoft.Json.Linq;

namespace DiffSync
{
	public interface IEdit : ISyncPacket
	{
		public JToken Diff { get; }
		public long ClientVersion { get; }

		public Guid ClientId { get; }
	}

	public interface ISyncPacket
	{
		public long ServerVersion { get; }
	}
}