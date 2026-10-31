using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;

namespace Wulato.UIKit
{
    /// <summary>Lifetime capability; concrete ownership and cleanup belong to the runtime engine.</summary>
    public abstract class UISessionScope : UIResourceScope
    {
        public abstract UISessionId Id { get; }
        public abstract UIContextId ContextId { get; }
        public CancellationToken SessionToken => Token;
        public abstract bool IsCurrent { get; }
        public abstract IUIAsyncScheduler Scheduler { get; }
        public abstract UniTask WaitLayoutAsync(CancellationToken cancellationToken);
    }
}
