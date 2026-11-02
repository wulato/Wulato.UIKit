using System;
using Cysharp.Threading.Tasks;
namespace Wulato.UIKit.PopupQueue
{
    /// <summary>Immutable request description. The queue alone opens the requested Session.</summary>
    public interface IPopupRequest
    {
        /// <summary>Stable registered View key; the queue opens it through the core.</summary>
        UIViewTypeKey ViewType{get;}
        /// <summary>Caller-owned immutable parameters, snapshotted at enqueue.</summary>
        UIArguments Arguments{get;}
    }
    /// <summary>Non-preemptive Context-local scheduling; Dispose initiates a nonblocking stop.</summary>
    public interface IUIPopupQueue:IDisposable
    {
        /// <summary>Enqueue one independent request; duplicate waiting/active keys return Duplicate.</summary>
        UniTask<PopupQueueResult> Enqueue(IPopupRequest request,int priority=0,string deduplicationKey=null);
        /// <summary>Acquire an independent idempotent lease preventing the next item from starting.</summary>
        IDisposable AcquirePause();
        /// <summary>Stop permanently and await active cleanup; repeated calls share completion.</summary>
        UniTask StopAsync();
    }
    /// <summary>Terminal scheduling outcome, separate from the underlying UI close reason.</summary>
    public enum PopupQueueStatus
    {
        /// <summary>The Session reached Closed.</summary>
        Completed,
        /// <summary>A waiting or active item already owns the key.</summary>
        Duplicate,
        /// <summary>Opening or closing reported an error.</summary>
        Failed,
        /// <summary>The queue was explicitly stopped.</summary>
        QueueStopped,
        /// <summary>The Context stopped.</summary>
        ContextStopped
    }
    /// <summary>Immutable outcome; Completion may contain errors or pending isolated cleanup.</summary>
    public sealed class PopupQueueResult
    {
        /// <summary>Scheduling outcome.</summary>
        public PopupQueueStatus Status{get;}
        /// <summary>Actual Closed result when a Session was opened.</summary>
        public UICompletion Completion{get;}
        /// <summary>Opening or request failure, when present.</summary>
        public Exception Error{get;}
        internal PopupQueueResult(PopupQueueStatus status,UICompletion completion=null,Exception error=null){Status=status;Completion=completion;Error=error;}
    }
}
