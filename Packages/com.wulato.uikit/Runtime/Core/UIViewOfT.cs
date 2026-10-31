using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;

namespace Wulato.UIKit
{
    /// <summary>Typed View branch; cannot satisfy the no-argument Open constraint.</summary>
    public abstract class UIView<TArgs> : UIViewBase
    {
        protected virtual UniTask OnOpenAsync(TArgs args,UISessionScope session,CancellationToken cancellationToken) => UniTask.CompletedTask;
        internal sealed override UniTask InvokeOpenAsync(UIArguments args,UISessionScope session,CancellationToken cancellationToken) => OnOpenAsync(args.Get<TArgs>(),session,cancellationToken);
    }
}
