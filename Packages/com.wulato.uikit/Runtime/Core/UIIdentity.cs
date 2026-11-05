using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;

namespace Wulato.UIKit
{
    /// <summary>Value identity, independent of runtime type names.</summary>
    [Serializable]
    public struct UIViewTypeKey : IEquatable<UIViewTypeKey>
    {
        [SerializeField] private string value;
        public string Value => value;
        public UIViewTypeKey(string value) { if (string.IsNullOrWhiteSpace(value)) throw new ArgumentException("A stable type key is required.",nameof(value)); this.value=value; }
        public bool Equals(UIViewTypeKey other) => EqualityComparer<string>.Default.Equals(Value,other.Value);
        public override bool Equals(object obj) => obj is UIViewTypeKey other && Equals(other);
        public override int GetHashCode() => EqualityComparer<string>.Default.GetHashCode(Value);
        public override string ToString() => Value ?? string.Empty;
        public static bool operator ==(UIViewTypeKey left,UIViewTypeKey right) => left.Equals(right);
        public static bool operator !=(UIViewTypeKey left,UIViewTypeKey right) => !left.Equals(right);
    }
    /// <summary>Value identity, independent of runtime type names.</summary>
    public readonly struct UIContextId : IEquatable<UIContextId>
    {
        public Guid Value { get; }
        public UIContextId(Guid value) { Value=value; }
        public bool Equals(UIContextId other) => EqualityComparer<Guid>.Default.Equals(Value,other.Value);
        public override bool Equals(object obj) => obj is UIContextId other && Equals(other);
        public override int GetHashCode() => EqualityComparer<Guid>.Default.GetHashCode(Value);
        public override string ToString() => Value.ToString();
        public static bool operator ==(UIContextId left,UIContextId right) => left.Equals(right);
        public static bool operator !=(UIContextId left,UIContextId right) => !left.Equals(right);
    }
    /// <summary>Value identity, independent of runtime type names.</summary>
    public readonly struct UISessionId : IEquatable<UISessionId>
    {
        public long Value { get; }
        public UISessionId(long value) { Value=value; }
        public bool Equals(UISessionId other) => EqualityComparer<long>.Default.Equals(Value,other.Value);
        public override bool Equals(object obj) => obj is UISessionId other && Equals(other);
        public override int GetHashCode() => EqualityComparer<long>.Default.GetHashCode(Value);
        public override string ToString() => Value.ToString();
        public static bool operator ==(UISessionId left,UISessionId right) => left.Equals(right);
        public static bool operator !=(UISessionId left,UISessionId right) => !left.Equals(right);
    }

}
