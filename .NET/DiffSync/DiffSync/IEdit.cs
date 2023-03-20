using System;
using Newtonsoft.Json.Linq;

namespace DiffSync
{
	public interface IEdit : IDocumentAction
	{
		public JToken? Diff { get; }
	}
}