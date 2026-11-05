using System;
using System.Threading;
using Cysharp.Threading.Tasks;
namespace Wulato.UIKit
{
    // Exactly one observer owns the original (possibly pooled, single-await) task.
    // All competing consumers observe the non-pooled record, never the original.
    internal sealed class UIOperationRecord<T>
    {
        readonly UniTaskCompletionSource<T> completion=new UniTaskCompletionSource<T>();
        public UniTask<T> Task => completion.Task;
        // Cancellation races the stable record. UniTask 2.5.11 AttachExternalCancellation
        // calls Forget on an already-canceled token, which publishes a completed fault globally.
        // This forwarding observer always contains that error and never touches the original again.
        internal async UniTask<T> WaitAsync(CancellationToken cancellationToken)
        {
            if (!cancellationToken.CanBeCanceled) return await completion.Task;
            var winner = new UniTaskCompletionSource<T>();
            using (cancellationToken.Register(() => winner.TrySetCanceled(cancellationToken)))
            {
                Forward(winner).Forget();
                return await winner.Task;
            }
        }
        async UniTask Forward(UniTaskCompletionSource<T> winner)
        {
            try { winner.TrySetResult(await completion.Task); }
            catch (OperationCanceledException e) { winner.TrySetCanceled(e.CancellationToken); }
            catch (Exception e) { winner.TrySetException(e); }
        }
        // Observe completed failure for identity/reporting even when cancellation won.
        internal Exception ObserveCompletedFailure()
        {
            var task = completion.Task;
            if (!task.Status.IsCompleted() || task.Status == UniTaskStatus.Succeeded) return null;
            try { task.GetAwaiter().GetResult(); }
            catch (OperationCanceledException) { }
            catch (Exception e) { return e; }
            return null;
        }
        public UIOperationRecord(UniTask<T> operation) { Observe(operation).Forget(); }
        async UniTask Observe(UniTask<T> operation)
        {
            try { completion.TrySetResult(await operation); }
            catch(OperationCanceledException e) { completion.TrySetCanceled(e.CancellationToken); }
            catch(Exception e) { completion.TrySetException(e); }
        }
    }
}
