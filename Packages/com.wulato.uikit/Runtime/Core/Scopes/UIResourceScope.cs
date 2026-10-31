using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;

namespace Wulato.UIKit
{
    /// <summary>Lifetime capability; concrete ownership and cleanup belong to the runtime engine.</summary>
    public abstract class UIResourceScope : IDisposable
    {
        public abstract CancellationToken Token { get; }
        public abstract void Track(IDisposable resource);
        public abstract void Dispose();
    }
}
