using System;

namespace DiffSync
{
	public interface IDocumentAction
	{
		DocActionType Type { get; }
		Guid ClientId { get; }

		Guid ServerId { get; }
		long ClientVersion { get; }
		long ServerVersion { get; }
		public Diff Diff { get; }
		public Document Content { get; }
	}

	public enum DocActionType
	{
		ClientAck,
		ServerAck,
		Reset,
		Edit
	}
}