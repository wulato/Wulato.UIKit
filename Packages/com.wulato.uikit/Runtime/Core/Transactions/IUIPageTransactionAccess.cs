using System;
using System.Threading;
using Cysharp.Threading.Tasks;
namespace Wulato.UIKit
{
    /// <summary>Opt-in page transactions. Caller cancellation applies only before replacement commit.</summary>
    internal interface IUIPageTransactionAccess
    {
        /// <summary>Own the Context FIFO until commit/rollback and bounded cleanup; caller cancellation ends at commit.</summary>
        UniTask ExecuteAsync(Func<IUIPageTransactionSteps,CancellationToken,UniTask> transaction,
            TimeSpan pairTimeout, CancellationToken cancellationToken = default);
    }
    /// <summary>Page steps are valid only during their owning transaction.</summary>
    internal interface IUIPageTransactionSteps : IUITransactionSteps
    {
        /// <summary>In this FIFO turn, close one eligible non-page owner or authorize the current page; null means veto/no action.</summary>
        UniTask<IUISession> RouteBackAsync(IUISession page,bool hasPrevious,UISessionId? inputSource=null);
        /// <summary>Show the prepared page under the input lock and observe one paired animation.</summary>
        UniTask AnimatePairAsync(IUISession from,IUISession to,INavigationTransition transition,CancellationToken cancellationToken=default);
        /// <summary>Atomically replace the page and prepared extension state before publishing notifications.</summary>
        void CommitReplacement(IUISession from,IUISession to,IUITransactionParticipant participant);
    }
    /// <summary>First-party state publication: Commit must only swap prepared state, Rollback must be idempotent.</summary>
    internal interface IUITransactionParticipant
    {
        /// <summary>Validate prepared state without modifying the published state.</summary>
        void Prepare();
        /// <summary>Publish prepared state synchronously without business callbacks or awaits.</summary>
        void Commit();
        /// <summary>Idempotently restore the previous state after a failed commit.</summary>
        void Rollback();
    }
    /// <summary>One paired animation; Stop must synchronously end all writes and settle PlayAsync.</summary>
    public interface INavigationTransition
    {
        /// <summary>Animation properties written on the outgoing page.</summary>
        UITransitionWrites FromWrites { get; }
        /// <summary>Animation properties written on the prepared page.</summary>
        UITransitionWrites ToWrites { get; }
        /// <summary>Run one paired animation; a first page has no outgoing surface.</summary>
        UniTask PlayAsync(UINavigationTransitionContext context,CancellationToken cancellationToken);
        /// <summary>Synchronously end all writes and settle the active animation operation.</summary>
        void Stop(UINavigationTransitionContext context);
        /// <summary>Idempotently reset adapter state; the core restores captured baselines.</summary>
        void Reset(UINavigationTransitionContext context);
    }
    /// <summary>Animation surfaces only; a first page has no From surface.</summary>
    public sealed class UINavigationTransitionContext
    {
        /// <summary>Outgoing animation surface, or null for first open.</summary>
        public UITransitionContext From { get; }
        /// <summary>Prepared incoming animation surface.</summary>
        public UITransitionContext To { get; }
        /// <summary>Identity of the owning transaction.</summary>
        public Guid TransactionId { get; }
        /// <summary>Create a surface pair; the incoming surface is required.</summary>
        public UINavigationTransitionContext(UITransitionContext from,UITransitionContext to,Guid transactionId)
        {From=from;To=to??throw new ArgumentNullException(nameof(to));TransactionId=transactionId;}
    }
}
