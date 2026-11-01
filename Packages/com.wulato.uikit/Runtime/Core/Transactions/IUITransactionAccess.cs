using System;
using System.Threading;
using Cysharp.Threading.Tasks;
namespace Wulato.UIKit
{
    /// <summary>Explicitly registered first-party extension capability. One callback owns the Context FIFO until completion.</summary>
    public interface IUITransactionAccess
    {
        UniTask ExecuteAsync(Func<IUITransactionSteps, CancellationToken, UniTask> transaction, CancellationToken cancellationToken = default);
    }
    /// <summary>Valid only inside ExecuteAsync. Steps execute directly and never enqueue behind their transaction.</summary>
    public interface IUITransactionSteps
    {
        UniTask<IUISession> PrepareAsync(UIViewTypeKey type, UIArguments arguments, UITransitionOwner owner, CancellationToken cancellationToken = default);
        void Commit(IUISession session);
        UniTask RollbackAsync(IUISession session);
        void SetVisible(IUISession session, bool visible);
        UniTask AnimateAsync(IUISession session, IUITransition transition, bool entering, CancellationToken cancellationToken = default);
        UniTask<UICompletion> CloseAsync(IUISession session, UITransitionOwner owner, UICloseReason reason = UICloseReason.Owner);
    }
}
