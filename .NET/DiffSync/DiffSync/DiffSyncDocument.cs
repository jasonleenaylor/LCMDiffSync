using System.Collections.Generic;

namespace DiffSync
{
	public class DiffSyncDocument
	{
		public VersionedDocument Shadow;
		public VersionedDocument Backup;
		public Queue<IDocumentAction> DocActions;

		public DiffSyncDocument(): this(new VersionedDocument(), new VersionedDocument(), new Queue<IDocumentAction>())
		{
		}

		public DiffSyncDocument(VersionedDocument shadow, VersionedDocument backup, Queue<IDocumentAction> docActions)
		{
			Shadow = shadow;
			Backup = backup;
			DocActions = docActions;
		}
	}
}