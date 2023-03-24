using System;
using System.Net.Sockets;
using Newtonsoft.Json.Linq;

namespace DiffSync
{
	public interface IEdit : IDocumentAction
	{
		public long ClientVersion { get; }
		public long ServerVersion { get; }
		public Diff Diff { get; }
	}
}