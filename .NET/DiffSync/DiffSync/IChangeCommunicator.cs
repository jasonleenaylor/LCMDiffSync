using System.Collections.Generic;

namespace DiffSync
{
	public interface IChangeCommunicator
	{
		void SendEdits(Stack<IEdit> edits);
	}
}