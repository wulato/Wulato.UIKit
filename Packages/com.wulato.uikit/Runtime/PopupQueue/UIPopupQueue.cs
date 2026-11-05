using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
namespace Wulato.UIKit.PopupQueue
{
    /// <summary>Context-local non-preemptive priority queue with independent pause leases.</summary>
    public sealed class UIPopupQueue:IUIPopupQueue
    {
        sealed class Item
        {
            internal UIViewTypeKey Type;internal UIArguments Args;internal int Priority;internal long Sequence;
            internal string Key;internal readonly UniTaskCompletionSource<PopupQueueResult> Completion=new UniTaskCompletionSource<PopupQueueResult>();
        }
        readonly IUIContext context;readonly IUIContextLifetime lifetime;
        readonly List<Item> waiting=new List<Item>();readonly HashSet<string> keys=new HashSet<string>(StringComparer.Ordinal);
        readonly CancellationTokenSource opening=new CancellationTokenSource();
        readonly UniTaskCompletionSource stoppedCompletion=new UniTaskCompletionSource();
        CancellationTokenRegistration stopping;
        IUISession current;bool running,stopped,contextStopped,finished;int pauses;long sequence;
        /// <inheritdoc/>
        public UIPopupQueue(IUIContext context)
        {
            this.context=context??throw new ArgumentNullException(nameof(context));lifetime=context.GetService<IUIContextLifetime>();
            stopping=lifetime.Stopping.Register(()=>Stop(true));
        }
        static void MainThread(){if(!PlayerLoopHelper.IsMainThread)throw new InvalidOperationException("Popup queue operations require the main thread.");}
        PopupQueueStatus StoppedStatus=>contextStopped||lifetime.Stopping.IsCancellationRequested?PopupQueueStatus.ContextStopped:PopupQueueStatus.QueueStopped;
        /// <inheritdoc/>
        public UniTask<PopupQueueResult> Enqueue(IPopupRequest request,int priority=0,string deduplicationKey=null)
        {
            if(!PlayerLoopHelper.IsMainThread)return UniTask.FromException<PopupQueueResult>(new UIException(UIErrorCode.InvalidRequest,"popup-queue-main-thread",default));
            if(stopped)return UniTask.FromResult(new PopupQueueResult(StoppedStatus));
            if(deduplicationKey!=null&&keys.Contains(deduplicationKey))return UniTask.FromResult(new PopupQueueResult(PopupQueueStatus.Duplicate));
            Item item;
            try
            {
                if(request==null)throw new ArgumentNullException(nameof(request));
                item=new Item{Type=request.ViewType,Args=request.Arguments,Priority=priority,Sequence=sequence++,Key=deduplicationKey};
            }
            catch(Exception error){return UniTask.FromResult(new PopupQueueResult(PopupQueueStatus.Failed,error:error));}
            // Request getters may synchronously reenter the service; never publish after StopAsync completed.
            if(stopped)return UniTask.FromResult(new PopupQueueResult(StoppedStatus));
            if(item.Key!=null&&keys.Contains(item.Key))return UniTask.FromResult(new PopupQueueResult(PopupQueueStatus.Duplicate));
            if(item.Key!=null)keys.Add(item.Key);waiting.Add(item);Start();return item.Completion.Task;
        }
        /// <inheritdoc/>
        public IDisposable AcquirePause()
        {
            MainThread();if(stopped)return new Pause(null);pauses++;return new Pause(this);
        }
        sealed class Pause:IDisposable
        {
            UIPopupQueue queue;internal Pause(UIPopupQueue queue){this.queue=queue;}
            public void Dispose(){MainThread();var value=queue;queue=null;if(value==null||value.stopped)return;value.pauses--;value.Start();}
        }
        void Start(){if(!running&&!stopped&&pauses==0&&waiting.Count>0)Pump().Forget(error=>UnityEngine.Debug.LogException(error));}
        async UniTask Pump()
        {
            running=true;
            try
            {
                while(!stopped&&pauses==0&&waiting.Count>0)
                {
                    waiting.Sort((a,b)=>{var priority=b.Priority.CompareTo(a.Priority);return priority!=0?priority:a.Sequence.CompareTo(b.Sequence);});
                    var item=waiting[0];waiting.RemoveAt(0);PopupQueueResult result;
                    try
                    {
                        current=await context.OpenAsync(item.Type,item.Args,opening.Token);
                        var completion=await current.Closed;
                        if(lifetime.Stopping.IsCancellationRequested)Stop(true);
                        result=new PopupQueueResult(stopped?StoppedStatus:completion.Errors.Count>0?PopupQueueStatus.Failed:PopupQueueStatus.Completed,completion);
                    }
                    catch(Exception error){if(lifetime.Stopping.IsCancellationRequested)Stop(true);result=new PopupQueueResult(stopped?StoppedStatus:PopupQueueStatus.Failed,error:stopped?null:error);}
                    finally{current=null;if(item.Key!=null)keys.Remove(item.Key);}
                    item.Completion.TrySetResult(result);
                }
            }
            finally{running=false;if(stopped)FinishStop();else Start();}
        }
        void Stop(bool fromContext)
        {
            if(fromContext)contextStopped=true;
            if(stopped)return;stopped=true;
            var copy=waiting.ToArray();waiting.Clear();
            foreach(var item in copy)if(item.Key!=null)keys.Remove(item.Key);
            foreach(var item in copy)item.Completion.TrySetResult(new PopupQueueResult(StoppedStatus));
            try{opening.Cancel();}catch(Exception error){UnityEngine.Debug.LogException(error);}
            if(!fromContext&&current!=null&&current.State==UIState.Open)
                current.CloseAsync().Forget(error=>UnityEngine.Debug.LogException(error));
            if(!running)FinishStop();
        }
        void FinishStop(){if(finished)return;finished=true;keys.Clear();stopping.Dispose();opening.Dispose();stoppedCompletion.TrySetResult();}
        /// <inheritdoc/>
        public UniTask StopAsync()
        {
            if(!PlayerLoopHelper.IsMainThread)return UniTask.FromException(new UIException(UIErrorCode.InvalidRequest,"popup-queue-main-thread",default));
            Stop(false);return stoppedCompletion.Task;
        }
        /// <inheritdoc/>
        public void Dispose(){StopAsync().Forget(error=>UnityEngine.Debug.LogException(error));}
    }
}
