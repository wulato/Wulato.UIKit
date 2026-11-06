using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
namespace Wulato.UIKit.PopupQueue
{
    /// <summary>Context-local non-preemptive priority queue with independent pause and cancellation leases.</summary>
    public sealed class UIPopupQueue:IUIPopupQueue
    {
        sealed class Item
        {
            internal UIViewTypeKey Type;internal UIArguments Args;internal int Priority;internal long Sequence;
            internal string Key;internal CancellationToken Caller;internal CancellationTokenRegistration Registration;
            internal readonly CancellationTokenSource Opening=new CancellationTokenSource();
            internal readonly UniTaskCompletionSource<PopupQueueResult> Completion=new UniTaskCompletionSource<PopupQueueResult>();
            internal IUISession Session;internal int Canceled;internal bool Ended;
            internal bool IsCanceled=>Volatile.Read(ref Canceled)!=0;
        }
        readonly IUIContext context;readonly IUIContextLifetime lifetime;readonly UIRegistrySnapshot registry;
        readonly List<Item> waiting=new List<Item>();readonly HashSet<string> keys=new HashSet<string>(StringComparer.Ordinal);
        readonly UniTaskCompletionSource stoppedCompletion=new UniTaskCompletionSource();
        CancellationTokenRegistration stopping;
        Item active;bool running,stopped,contextStopped,finished;int pauses;long sequence;
        /// <inheritdoc/>
        public UIPopupQueue(IUIContext context)
        {
            this.context=context??throw new ArgumentNullException(nameof(context));lifetime=context.GetService<IUIContextLifetime>();registry=context.GetService<UIRegistrySnapshot>();
            stopping=lifetime.Stopping.Register(()=>Stop(true));
        }
        static void MainThread(){if(!PlayerLoopHelper.IsMainThread)throw new InvalidOperationException("Popup queue operations require the main thread.");}
        UniTask<PopupQueueResult> WrongThread()=>UniTask.FromException<PopupQueueResult>(new UIException(UIErrorCode.InvalidRequest,"popup-queue-main-thread",context.ContextId));
        PopupQueueStatus StoppedStatus=>contextStopped||lifetime.Stopping.IsCancellationRequested?PopupQueueStatus.ContextStopped:PopupQueueStatus.QueueStopped;
        /// <inheritdoc/>
        public UniTask<PopupQueueResult> Enqueue(IPopupRequest request,int priority=0,string deduplicationKey=null,CancellationToken cancellationToken=default)
        {
            if(!PlayerLoopHelper.IsMainThread)return WrongThread();
            if(stopped)return UniTask.FromResult(new PopupQueueResult(StoppedStatus));
            if(cancellationToken.IsCancellationRequested)return UniTask.FromCanceled<PopupQueueResult>(cancellationToken);
            if(deduplicationKey!=null&&keys.Contains(deduplicationKey))return UniTask.FromResult(new PopupQueueResult(PopupQueueStatus.Duplicate));
            try
            {
                if(request==null)throw new ArgumentNullException(nameof(request));
                // Snapshot getters before publishing; reentrant Stop is rechecked by the descriptor overload.
                var type=request.ViewType;var args=request.Arguments;
                return Enqueue(type,args,priority,deduplicationKey,cancellationToken);
            }
            catch(Exception error){return UniTask.FromResult(new PopupQueueResult(PopupQueueStatus.Failed,error:error));}
        }
        /// <inheritdoc/>
        public UniTask<PopupQueueResult> Enqueue(UIViewTypeKey type,UIArguments arguments,int priority=0,string deduplicationKey=null,CancellationToken cancellationToken=default)
        {
            if(!PlayerLoopHelper.IsMainThread)return WrongThread();
            if(stopped)return UniTask.FromResult(new PopupQueueResult(StoppedStatus));
            if(cancellationToken.IsCancellationRequested)return UniTask.FromCanceled<PopupQueueResult>(cancellationToken);
            if(deduplicationKey!=null&&keys.Contains(deduplicationKey))return UniTask.FromResult(new PopupQueueResult(PopupQueueStatus.Duplicate));
            try
            {
                var entry=registry.Get(type);
                if(entry.Layer==UILayer.Page||!entry.Accepts(arguments))throw new UIException(UIErrorCode.InvalidRequest,"popup-registration",context.ContextId);
            }
            catch(Exception error){return UniTask.FromResult(new PopupQueueResult(PopupQueueStatus.Failed,error:error));}
            var item=new Item{Type=type,Args=arguments,Priority=priority,Sequence=sequence++,Key=deduplicationKey,Caller=cancellationToken};
            if(item.Key!=null)keys.Add(item.Key);waiting.Add(item);
            var registration=cancellationToken.Register(()=>
            {
                Interlocked.Exchange(ref item.Canceled,1);
                CancelAsync(item).Forget(error=>UnityEngine.Debug.LogException(error));
            });
            // Already-canceled registration may have synchronously finalized the waiting item.
            if(item.Ended)registration.Dispose();else item.Registration=registration;
            Start();return item.Completion.Task;
        }
        async UniTask CancelAsync(Item item)
        {
            await UniTask.SwitchToMainThread();
            await UIContextFacade.WaitForCallbackExitAsync(context);
            if(item.Ended)return;
            if(waiting.Remove(item)){Finish(item,null);Start();return;}
            if(active!=item)return;
            item.Opening.Cancel();
            if(!item.Ended)CloseActive(item);
        }
        void CloseActive(Item item)
        {
            if(item.Session!=null&&item.Session.State==UIState.Open)
                item.Session.CloseAsync().Forget(error=>UnityEngine.Debug.LogException(error));
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
                    var item=waiting[0];waiting.RemoveAt(0);active=item;PopupQueueResult result=null;
                    try
                    {
                        if(!item.IsCanceled)
                        {
                            item.Session=await context.OpenAsync(item.Type,item.Args,item.Opening.Token);
                            UICompletion completion;
                            if((stopped||item.IsCanceled)&&item.Session.State==UIState.Open)completion=await item.Session.CloseAsync();
                            else completion=await item.Session.Closed;
                            if(lifetime.Stopping.IsCancellationRequested)Stop(true);
                            result=new PopupQueueResult(stopped?StoppedStatus:completion.Errors.Count>0?PopupQueueStatus.Failed:PopupQueueStatus.Completed,completion);
                        }
                    }
                    catch(Exception error)
                    {
                        if(lifetime.Stopping.IsCancellationRequested)Stop(true);
                        result=new PopupQueueResult(stopped?StoppedStatus:PopupQueueStatus.Failed,error:stopped?null:error);
                    }
                    finally{active=null;}
                    Finish(item,result);
                }
            }
            finally{running=false;if(stopped)FinishStop();else Start();}
        }
        void Finish(Item item,PopupQueueResult result)
        {
            if(item.Ended)return;item.Ended=true;item.Registration.Dispose();item.Opening.Dispose();
            // Logical rollback / Closed has settled. Release the key before publishing to reentrant callers.
            if(item.Key!=null)keys.Remove(item.Key);
            if(stopped)item.Completion.TrySetResult(new PopupQueueResult(StoppedStatus,result?.Completion,result?.Error));
            else if(item.IsCanceled)item.Completion.TrySetCanceled(item.Caller);
            else item.Completion.TrySetResult(result);
        }
        void Stop(bool fromContext)
        {
            if(fromContext)contextStopped=true;
            if(stopped)return;stopped=true;
            var copy=waiting.ToArray();waiting.Clear();
            foreach(var item in copy)Finish(item,new PopupQueueResult(StoppedStatus));
            var itemActive=active;
            if(itemActive!=null)
            {
                try{itemActive.Opening.Cancel();}catch(Exception error){UnityEngine.Debug.LogException(error);}
                if(!fromContext&&!itemActive.Ended)CloseActive(itemActive);
            }
            if(!running)FinishStop();
        }
        void FinishStop(){if(finished)return;finished=true;keys.Clear();stopping.Dispose();stoppedCompletion.TrySetResult();}
        /// <inheritdoc/>
        public UniTask StopAsync()
        {
            if(!PlayerLoopHelper.IsMainThread)return UniTask.FromException(new UIException(UIErrorCode.InvalidRequest,"popup-queue-main-thread",context.ContextId));
            Stop(false);return stoppedCompletion.Task;
        }
        /// <inheritdoc/>
        public void Dispose(){StopAsync().Forget(error=>UnityEngine.Debug.LogException(error));}
    }
}
