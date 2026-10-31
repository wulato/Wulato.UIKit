using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;

namespace Wulato.UIKit
{
    /// <summary>No-argument View branch.</summary>
    public abstract class UIView : UIViewBase
    {
        protected virtual UniTask OnOpenAsync(UISessionScope session,CancellationToken cancellationToken) => UniTask.CompletedTask;
        internal sealed override UniTask InvokeOpenAsync(UIArguments args,UISessionScope session,CancellationToken cancellationToken) => OnOpenAsync(session,cancellationToken);
    }
}
