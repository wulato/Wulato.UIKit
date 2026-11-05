using System;
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
        UniTask<UIBatchCompletion> CloseAllAsync(UICloseReason reason = UICloseReason.Owner);
        UniTask<UIBatchCompletion> CloseAllOfTypeAsync<TView>(UICloseReason reason = UICloseReason.Owner) where TView : UIViewBase;
        UniTask ClearCacheAsync();
        UniTask<UIShutdownCompletion> ShutdownAsync();
        TService GetService<TService>() where TService : class;
        UIDiagnosticsSnapshot GetDiagnostics();
    }
}
