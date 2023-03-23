using System;

namespace DiffSync
{
	public interface IDocumentAction
	{
		Guid ClientId { get; }

		Guid ServerId { get; }
	}

	public interface IClientChangeAck : IDocumentAction
	{
		long ClientVersion { get; }
	}
	public interface IServerChangeAck : IDocumentAction
	{
		long ServerVersion { get; }
	}
}