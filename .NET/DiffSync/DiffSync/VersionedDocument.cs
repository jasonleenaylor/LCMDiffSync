using System;
using Newtonsoft.Json.Linq;

namespace DiffSync
{
	public class VersionedDocument : ICloneable, IEquatable<VersionedDocument>
	{
		public long ClientVersion { get; set; }
		public long ServerVersion { get; set; }
		public Document? Document { get; set; }
		public object Clone()
		{
			return new VersionedDocument
			{
				Document = Document?.Clone(),
				ClientVersion = ClientVersion,
				ServerVersion = ServerVersion
			};
		}

		public bool Equals(VersionedDocument? other)
		{
			if (ReferenceEquals(null, other)) return false;
			if (ReferenceEquals(this, other)) return true;
			return ClientVersion == other.ClientVersion && ServerVersion == other.ServerVersion && Document.Equals(other.Document);
		}

		public override bool Equals(object? obj)
		{
			if (ReferenceEquals(null, obj)) return false;
			if (ReferenceEquals(this, obj)) return true;
			if (obj.GetType() != this.GetType()) return false;
			return Equals((VersionedDocument)obj);
		}

		public override int GetHashCode()
		{
			return HashCode.Combine(ClientVersion, ServerVersion, Document);
		}
	}
}