using System;

namespace DiffSync
{
	public interface IDocumentAction
	{
		long ServerVersion { get; }
		Guid ClientId { get; }

		Guid ServerId { get; }
		long ClientVersion { get; }
	}
}