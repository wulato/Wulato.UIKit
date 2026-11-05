using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;

namespace Wulato.UIKit
{
    /// <summary>A single successful open; never exposes a reusable View instance.</summary>
    public interface IUISession
    {
        UIContextId ContextId { get; }
        UISessionId Id { get; }
        UIState State { get; }
        UniTask<UICompletion> Closed { get; }
        UniTask<UICompletion> CloseAsync(UICloseReason reason = UICloseReason.Owner);
    }
    public interface IUISession<out TView> : IUISession where TView : UIViewBase { }
}
