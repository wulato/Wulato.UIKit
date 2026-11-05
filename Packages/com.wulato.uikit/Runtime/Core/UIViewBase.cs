using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;

namespace Wulato.UIKit
{
    /// <summary>Common lifecycle surface. Instance and Session execution are provided by the runtime engine.</summary>
    public abstract class UIViewBase : MonoBehaviour
    {
        protected virtual void OnCreate() { }
        protected virtual bool CanClose(UICloseReason reason) => true;
        protected virtual UniTask OnCloseAsync(UICloseReason reason,CancellationToken cancellationToken) => UniTask.CompletedTask;
        protected virtual void OnForegroundEntered(UIForegroundScope foreground) { }
        protected virtual void OnForegroundExited() { }
        protected virtual void OnClosed(UICloseReason reason) { }
        protected virtual void OnDispose() { }
        // Bound by the engine to one current Session; null outside a Session.
        internal Action UserCloseRequest { get; set; }
        protected void RequestClose() => UserCloseRequest?.Invoke();
        internal void InvokeCreate() => OnCreate();
        internal bool InvokeCanClose(UICloseReason reason) => CanClose(reason);
        internal UniTask InvokeCloseAsync(UICloseReason reason,CancellationToken token) => OnCloseAsync(reason,token);
        internal void InvokeForegroundEntered(UIForegroundScope scope) => OnForegroundEntered(scope);
        internal void InvokeForegroundExited() => OnForegroundExited();
        internal void InvokeClosed(UICloseReason reason) => OnClosed(reason);
        internal void InvokeDispose() => OnDispose();
        internal abstract UniTask InvokeOpenAsync(UIArguments args,UISessionScope session,CancellationToken cancellationToken);
    }
}
