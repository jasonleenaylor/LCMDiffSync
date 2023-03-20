using System.Collections.Generic;

namespace DiffSync
{
	internal class DiffSyncDocument
	{
		public VersionedDocument Shadow;
		public VersionedDocument Backup;
		public Queue<IDocumentAction> Edits;

		public DiffSyncDocument(): this(new VersionedDocument(), new VersionedDocument(), new Queue<IDocumentAction>())
		{
		}

		public DiffSyncDocument(VersionedDocument shadow, VersionedDocument backup, Queue<IDocumentAction> edits)
		{
			Shadow = shadow;
			Backup = backup;
			Edits = edits;
		}
	}
}