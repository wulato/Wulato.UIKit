using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;

namespace Wulato.UIKit
{
    public enum UIState { Opening, Open, Closing, Closed }
    public enum UIContextState { Running, Stopping, Stopped }
    public enum UICloseReason { Owner, UserRequest, Back, Mask, OpenFailed, OpenCanceled, ContextShutdown, RootDestroyed }
    public enum UIErrorCode { AlreadyOpen, InvalidRequest, LoadFailed, LifecycleFailed, TransitionFailed, Timeout, ContextStopped }
    public enum UILayer { Page, Overlay, Modal, Toast, System }
    public enum UIMultiplicity { Single, Multi }
    public enum UIClosePolicy { Release, CacheOne }
    public enum UIBackAction { None, RequestClose }
    public enum UIDiagnosticsLevel { Off, Basic, Detailed }
    /// <summary>Operation failure with stage, identity and original cause. Cancellation stays OperationCanceledException.</summary>
    public sealed class UIException : Exception
    {
        public UIErrorCode Code { get; }
        public string Stage { get; }
        public UIContextId ContextId { get; }
        public UISessionId? SessionId { get; }
        public UIException(UIErrorCode code,string stage,UIContextId contextId,UISessionId? sessionId=null,Exception innerException=null)
            : base(code+" at "+stage,innerException) { Code=code;Stage=stage;ContextId=contextId;SessionId=sessionId; }
    }
    public interface IUIErrorSink { void Report(UIException error); }
    public interface IUIAsyncScheduler
    {
        UniTask SwitchToMainThreadAsync(CancellationToken cancellationToken);
        UniTask NextFrameAsync(CancellationToken cancellationToken);
        UniTask DelayAsync(TimeSpan duration,bool ignoreTimeScale,CancellationToken cancellationToken);
        double UnscaledTime { get; }
    }
    public interface IUILayoutReady { UniTask WaitLayoutAsync(UIViewBase view,CancellationToken cancellationToken); }
    public interface IUIViewLoader
    {
        UniTask<IUIViewLease> CreateAsync(UIViewAsset asset,Transform inactiveParent,CancellationToken cancellationToken);
    }
    public interface IUIViewLease : IDisposable { UIViewBase View { get; } }
    // Only first-party backends implement this seam. Core never references the Canvas backend.
    internal interface IUIViewHostFactory { IUIViewHost Create(UILayer layer); }
    internal interface IUIViewHost : IDisposable
    {
        Transform InactiveParent { get; }
        RectTransform AnimationRoot { get; }
        CanvasGroup AnimationOpacity { get; }
        void AttachHidden(UIViewBase view);
        void SetVisible(bool visible);
        void SetInteractive(bool interactive);
        void MoveToCache();
        void DetachView();
    }
}
