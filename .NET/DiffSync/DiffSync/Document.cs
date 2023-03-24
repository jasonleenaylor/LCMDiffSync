using System;
using System.Collections.Generic;
using JsonDiffPatchDotNet;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace DiffSync
{
    public class Document : IEquatable<Document>
    {
        private static readonly JsonDiffPatch _differPatcher = new JsonDiffPatch(new Options { MinEfficientTextDiffLength = 2 });

        public Document(string json)
        {
            _jObject = JObject.Parse(json);
        }

        private readonly JObject _jObject;

        public void SetString(string field, string value)
        {
            _jObject[field] = value;
        }

        public string GetString(string field)
        {
            return _jObject[field]?.ToString() ?? "";
        }

        public Document Clone()
        {
            return new Document(_jObject.ToString(Formatting.None));
        }

        public static Diff? Diff(Document? left, Document? right)
        {
            var diff = _differPatcher.Diff(left?._jObject, right?._jObject);
            if (diff == null) return null;
            return new Diff(diff);
        }

        public Diff? Diff(Document other)
        {
            return Diff(this, other);
        }

        public static Document? Patch(Document? left, Diff? diff)
        {
            if (diff == null) return left;

            var patched = _differPatcher.Patch(left?._jObject, diff.JsonDiff);
            return new Document(patched.ToString(Formatting.None));
        }

        public Document Patch(Diff diff)
        {
            var patched = _differPatcher.Patch(_jObject, diff.JsonDiff);
            return new Document(patched.ToString(Formatting.None));
        }


        public bool Equals(Document? other)
        {
            if (ReferenceEquals(null, other)) return false;
            if (ReferenceEquals(this, other)) return true;
            return _jObject.ToString(Formatting.None).Equals(other._jObject.ToString(Formatting.None));
        }

        public override bool Equals(object? obj)
        {
            if (ReferenceEquals(null, obj)) return false;
            if (ReferenceEquals(this, obj)) return true;
            if (obj.GetType() != this.GetType()) return false;
            return Equals((Document)obj);
        }

        public override int GetHashCode()
        {
            return _jObject.GetHashCode();
        }

        public static bool operator ==(Document? left, Document? right)
        {
            return Equals(left, right);
        }

        public static bool operator !=(Document? left, Document? right)
        {
            return !Equals(left, right);
        }

        public override string ToString()
        {
            return _jObject.ToString(Formatting.Indented);
        }
    }

    public class Diff
    {
        public readonly JToken JsonDiff;

        public Diff(JToken jsonDiff)
        {
            JsonDiff = jsonDiff;
        }

        public Diff Clone()
        {
            return new Diff(JsonDiff.DeepClone());
        }
    }
}