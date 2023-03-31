using System;
using System.Collections.Generic;
using System.Linq;
using JsonDiffPatchDotNet;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Ycs;

namespace DiffSync
{
    public class Document : IEquatable<Document>
    {
        private readonly YDoc _yDoc = new YDoc();
        private readonly YMap _yMap;
        public Document(string json)
        {
            _yMap = _yDoc.GetMap();
            var jsonObject = JObject.Parse(json);
            PopulateMap(_yMap, jsonObject);
        }

        private Document(Document? document)
        {
            if (document != null)
            {
                var state = document._yDoc.EncodeStateAsUpdateV2();
                _yDoc.ApplyUpdateV2(state);
            }

            _yMap = _yDoc.GetMap();
        }

        private static void PopulateMap(YMap map, JObject jsonObject)
        {
            foreach (var (key, value) in jsonObject)
            {
                map.Set(key, JTokenToYValue(value));
            }
        }

        private static object? JTokenToYValue(JToken? value)
        {
            switch (value)
            {
                case null:
                    return null;
                case JValue jValue:
                    return jValue.Value;
                case JObject jObject:
                    var subMap = new YMap();
                    PopulateMap(subMap, jObject);
                    return subMap;
                    break;
                case JArray jArray:
                    return new YArray(jArray.Select(JTokenToYValue));
                default:
                    throw new ArgumentOutOfRangeException();
            }
        }


        public void SetString(string field, string value)
        {
            _yMap.Set(field, value);
        }

        public string GetString(string field)
        {
            return _yMap.Get(field)?.ToString() ?? "";
        }

        public YText GetText(string field)
        {
            var text = _yMap.Get(field) as YText;
            if (text is null)
            {
                text = new YText();
                _yMap.Set(field, text);
            }

            return text;
        }

        public Document Clone()
        {
            return new Document(this);
        }

        public static Diff? Diff(Document? left, Document? right)
        {
            if (right is null) return null;
            if (left == right) return null;
            var diff = right._yDoc.EncodeStateAsUpdateV2(left?._yDoc.EncodeStateVectorV2());
            return new Diff(diff);
        }

        public Diff? Diff(Document other)
        {
            return Diff(this, other);
        }

        public static Document? Patch(Document? left, Diff? diff)
        {
            if (diff == null) return left;
            var newDoc = new Document(left);
            newDoc._yDoc.ApplyUpdateV2(diff.Update);
            return newDoc;
        }

        public Document Patch(Diff diff)
        {
            return Patch(this, diff)!;
        }


        public bool Equals(Document? other)
        {
            if (ReferenceEquals(null, other)) return false;
            if (ReferenceEquals(this, other)) return true;
            return _yDoc.CreateSnapshot().Equals(other._yDoc.CreateSnapshot());
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
            return _yDoc.Guid.GetHashCode();
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
            return _yMap.ToDictionary(kvp => kvp.Key, kvp => kvp.Value).ToString();
        }
    }

    public class Diff
    {
        public byte[] Update { get; }

        public Diff(byte[] update)
        {
            Update = update;
        }

        public Diff Clone()
        {
            return new Diff(Update);
        }
    }
}