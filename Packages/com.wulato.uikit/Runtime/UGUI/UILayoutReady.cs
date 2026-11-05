using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;

namespace Wulato.UIKit
{
    /// <summary>One subsequent Update followed by the registered uGUI layout/graphic rebuild.</summary>
    public sealed class UILayoutReady : IUILayoutReady
    {
        public async UniTask WaitLayoutAsync(UIViewBase view, CancellationToken cancellationToken)
        {
            if (!PlayerLoopHelper.IsMainThread) throw new InvalidOperationException("UI layout requires the main thread.");
            cancellationToken.ThrowIfCancellationRequested();
            if (view != null) view.GetComponentInParent<UIViewGateGuard>(true)?.ValidateNow();
            if (view == null || !view.gameObject.activeInHierarchy) throw new InvalidOperationException("Layout requires an active, hidden prepared View.");
            await UniTask.NextFrame(PlayerLoopTiming.Update, cancellationToken);
            cancellationToken.ThrowIfCancellationRequested();
            if (view != null) view.GetComponentInParent<UIViewGateGuard>(true)?.ValidateNow();
            if (view == null || !view.gameObject.activeInHierarchy) throw new InvalidOperationException("View became unavailable before the layout barrier.");
            using(UIProfiler.Layout.Auto())Canvas.ForceUpdateCanvases();
        }
    }
}
