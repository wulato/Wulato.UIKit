using System;
using Wulato.UIKit.Dialogs;
using Wulato.UIKit.PopupQueue;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;

namespace Wulato.UIKit
{
    /// <summary>One isolated UI command domain. Async errors are returned through the operation.</summary>
    public interface IUIContext
    {
        UIContextId ContextId { get; }
        UniTask<UICompletion> CloseAsync(IUISession session,UICloseReason reason=UICloseReason.Owner);
        UniTask<IUISession<TView>> OpenAsync<TView>(CancellationToken cancellationToken = default) where TView : UIView;
        UniTask<IUISession<TView>> OpenAsync<TView,TArgs>(TArgs args, CancellationToken cancellationToken = default) where TView : UIView<TArgs>;
        UniTask<IUISession> OpenAsync(UIViewTypeKey type, UIArguments args, CancellationToken cancellationToken = default);
        UniTask<IUISession> PushAsync<TView>(CancellationToken cancellationToken=default) where TView:UIView;
        UniTask<IUISession> PushAsync<TView,TArgs>(TArgs arguments,CancellationToken cancellationToken=default) where TView:UIView<TArgs>;
        UniTask<IUISession> PushAsync(UIViewTypeKey type,UIArguments arguments,CancellationToken cancellationToken=default);
        UniTask<IUISession> ReplaceAsync<TView>(CancellationToken cancellationToken=default) where TView:UIView;
        UniTask<IUISession> ReplaceAsync<TView,TArgs>(TArgs arguments,CancellationToken cancellationToken=default) where TView:UIView<TArgs>;
        UniTask<IUISession> ReplaceAsync(UIViewTypeKey type,UIArguments arguments,CancellationToken cancellationToken=default);
        UniTask<bool> PopToAsync<TView>(CancellationToken cancellationToken=default) where TView:UIViewBase;
        UniTask<bool> PopToAsync(UIViewTypeKey type,CancellationToken cancellationToken=default);
        /// <summary>FIFO Back: one local input popup first, otherwise the previous page; veto or root returns false.</summary>
        UniTask<bool> BackAsync(CancellationToken cancellationToken=default);
        /// <summary>Open a registered dialog and publish its business result after Closed; caller cancellation covers the entire wait.</summary>
        UniTask<DialogResult<TResult>> DialogAsync<TResult>(IDialogRequest<TResult> request,CancellationToken cancellationToken=default);
        /// <summary>Queue a registered non-Page View; caller cancellation is independent and waits for rollback/Closed.</summary>
        UniTask<PopupQueueResult> PopupAsync<TView>(int priority=0,string deduplicationKey=null,CancellationToken cancellationToken=default) where TView:UIView;
        UniTask<PopupQueueResult> PopupAsync<TView,TArgs>(TArgs args,int priority=0,string deduplicationKey=null,CancellationToken cancellationToken=default) where TView:UIView<TArgs>;
        UniTask<UIBatchCompletion> CloseAllAsync(UICloseReason reason = UICloseReason.Owner);
        UniTask<UIBatchCompletion> CloseAllOfTypeAsync<TView>(UICloseReason reason = UICloseReason.Owner) where TView : UIViewBase;
        UniTask ClearCacheAsync();
        UniTask<UIShutdownCompletion> ShutdownAsync();
        TService GetService<TService>() where TService : class;
        UIDiagnosticsSnapshot GetDiagnostics();
    }
}
