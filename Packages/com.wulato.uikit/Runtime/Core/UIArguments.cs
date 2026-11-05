using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;

namespace Wulato.UIKit
{
    /// <summary>Explicit argument type and value. Typed null differs from None; values are not deep copied.</summary>
    public readonly struct UIArguments
    {
        public static UIArguments None => default;
        public bool HasValue => ArgumentType != null;
        public Type ArgumentType { get; }
        public object Value { get; }
        private UIArguments(Type type, object value) { ArgumentType=type; Value=value; }
        public static UIArguments From<TArgs>(TArgs args) => new UIArguments(typeof(TArgs),args);
        public TArgs Get<TArgs>()
        {
            if (ArgumentType != typeof(TArgs)) throw new InvalidOperationException("UI argument type does not match.");
            return (TArgs)Value;
        }
    }
}
