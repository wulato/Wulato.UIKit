using System;
namespace Wulato.UIKit
{
    // Shared with independent cleanup without retaining the owning Context.
    internal sealed class UICallbackScope
    {
        internal int Depth { get; private set; }
        internal T Invoke<T>(Func<T> callback)
        {
            Depth++;
            try { return callback(); } finally { Depth--; }
        }
        internal void Invoke(Action callback) => Invoke(() => { callback(); return true; });
    }
}
