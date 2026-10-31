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
        public UIOperationRecord(UniTask<T> operation) { Observe(operation).Forget(); }
        async UniTask Observe(UniTask<T> operation)
        {
            try { completion.TrySetResult(await operation); }
            catch(OperationCanceledException e) { completion.TrySetCanceled(e.CancellationToken); }
            catch(Exception e) { completion.TrySetException(e); }
        }
    }
}
