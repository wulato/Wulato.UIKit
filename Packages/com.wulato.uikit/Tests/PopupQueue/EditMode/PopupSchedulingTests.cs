using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using NUnit.Framework;
using Wulato.UIKit.Tests.Support;
namespace Wulato.UIKit.PopupQueue.Tests
{
    public class PopupView:UIView<int>
    {
        public static readonly List<int> Opened=new List<int>();
        public static UniTaskCompletionSource OpenGate;public static Action Closing;
        protected override UniTask OnOpenAsync(int args,UISessionScope scope,CancellationToken ct){Opened.Add(args);return args==99&&OpenGate!=null?OpenGate.Task:UniTask.CompletedTask;}
        protected override UniTask OnCloseAsync(UICloseReason reason,CancellationToken ct){Closing?.Invoke();return UniTask.CompletedTask;}
        public void Dismiss()=>RequestClose();
    }
    [Category("popup_queue")]
    public class PopupSchedulingTests
    {
        sealed class Request:IPopupRequest
        {
            public UIViewTypeKey ViewType{get;}
            public UIArguments Arguments{get;}
            internal Request(int value,bool invalid=false){ViewType=new UIViewTypeKey(invalid?"missing":"popup");Arguments=UIArguments.From(value);}
        }
        UIFlowTestHarness Fixture()
        {
            PopupView.Opened.Clear();PopupView.OpenGate=null;PopupView.Closing=null;var registry=new UIRegistryBuilder();registry.Add<PopupView,int>(new UIViewTypeKey("popup"),UIViewAsset.Resources("popup"),UILayer.Modal,closePolicy:UIClosePolicy.CacheOne);
            return new UIFlowTestHarness(registry.Build());
        }
        void Close(UIFlowTestHarness f)=>((PopupView)f.LastView).Dismiss();
        sealed class StoppingRequest:IPopupRequest
        {
            readonly Action read;
            internal StoppingRequest(Action read){this.read=read;}
            public UIViewTypeKey ViewType{get{read();return new UIViewTypeKey("popup");}}
            public UIArguments Arguments=>UIArguments.From(1);
        }
        [Test] public void StopWhileReadingDescriptor_DoesNotLeaveUnfinishedRequest()
        {
            using(var f=Fixture())using(var queue=new UIPopupQueue(f.UI))
            {
                var item=queue.Enqueue(new StoppingRequest(()=>queue.StopAsync().GetAwaiter().GetResult()));
                Assert.That(item.Status.IsCompleted(),Is.True,"an item may not be added after stop completed");
                Assert.That(item.GetAwaiter().GetResult().Status,Is.EqualTo(PopupQueueStatus.QueueStopped));Assert.That(f.Creates,Is.Zero);
            }
        }
        [Test] public void WorkerThreadAsyncEntries_ReturnFaultedOperationsWithoutSynchronousThrow()
        {
            using(var f=Fixture())using(var queue=new UIPopupQueue(f.UI))
            {
                UniTask<PopupQueueResult> item=default;UniTask stop=default;Exception enqueueError=null,stopError=null;
                System.Threading.Tasks.Task.Run(()=>
                {
                    try{item=queue.Enqueue(new Request(1));}catch(Exception error){enqueueError=error;}
                    try{stop=queue.StopAsync();}catch(Exception error){stopError=error;}
                }).GetAwaiter().GetResult();
                Assert.That(enqueueError,Is.Null);Assert.That(stopError,Is.Null);
                Assert.That(Assert.Throws<UIException>(()=>item.GetAwaiter().GetResult()).Code,Is.EqualTo(UIErrorCode.InvalidRequest));
                Assert.That(Assert.Throws<UIException>(()=>stop.GetAwaiter().GetResult()).Code,Is.EqualTo(UIErrorCode.InvalidRequest));Assert.That(f.Creates,Is.Zero);
            }
        }
        [Test] public void StopFromClosingHook_DoesNotSubmitNestedClose()
        {
            using(var f=Fixture())using(var queue=new UIPopupQueue(f.UI))
            {
                var item=queue.Enqueue(new Request(1));UniTask stop=default;
                PopupView.Closing=()=>{stop=queue.StopAsync();Assert.That(stop.Status.IsCompleted(),Is.False);};
                Close(f);stop.GetAwaiter().GetResult();Assert.That(item.GetAwaiter().GetResult().Status,Is.EqualTo(PopupQueueStatus.QueueStopped));
            }
        }
        [Test] public void PriorityAndFIFO_RespectAllPauseLeases()
        {
            using(var f=Fixture())using(var queue=new UIPopupQueue(f.UI))
            {
                var first=queue.AcquirePause();var second=queue.AcquirePause();
                var low=queue.Enqueue(new Request(1),int.MinValue);var high=queue.Enqueue(new Request(2),int.MaxValue);var same=queue.Enqueue(new Request(3),int.MaxValue);
                first.Dispose();first.Dispose();Assert.That(PopupView.Opened,Is.Empty);second.Dispose();Assert.That(PopupView.Opened,Is.EqualTo(new[]{2}));
                Close(f);Assert.That(high.GetAwaiter().GetResult().Status,Is.EqualTo(PopupQueueStatus.Completed));Assert.That(PopupView.Opened,Is.EqualTo(new[]{2,3}));
                Close(f);same.GetAwaiter().GetResult();Close(f);low.GetAwaiter().GetResult();Assert.That(PopupView.Opened,Is.EqualTo(new[]{2,3,1}));
            }
        }
        [Test] public void ActiveItemIsNotPreempted_KeyReleasedAfterClosed()
        {
            using(var f=Fixture())using(var queue=new UIPopupQueue(f.UI))
            {
                var first=queue.Enqueue(new Request(1),0,"");Assert.That(queue.Enqueue(new Request(9),100,"").GetAwaiter().GetResult().Status,Is.EqualTo(PopupQueueStatus.Duplicate));
                var second=queue.Enqueue(new Request(2),100,"next");Assert.That(PopupView.Opened,Is.EqualTo(new[]{1}));Close(f);first.GetAwaiter().GetResult();
                var third=queue.Enqueue(new Request(3),0,"");Close(f);second.GetAwaiter().GetResult();Close(f);third.GetAwaiter().GetResult();Assert.That(f.Creates,Is.EqualTo(1));
            }
        }
        [Test] public void StopWhilePaused_ReleasesWaitersAndDoesNotRestart()
        {
            using(var f=Fixture())using(var queue=new UIPopupQueue(f.UI))
            {
                var pause=queue.AcquirePause();var item=queue.Enqueue(new Request(1),deduplicationKey:"one");var stop=queue.StopAsync();stop.GetAwaiter().GetResult();
                Assert.That(item.GetAwaiter().GetResult().Status,Is.EqualTo(PopupQueueStatus.QueueStopped));pause.Dispose();queue.StopAsync().GetAwaiter().GetResult();
                Assert.That(PopupView.Opened,Is.Empty);Assert.That(queue.Enqueue(new Request(2),deduplicationKey:"one").GetAwaiter().GetResult().Status,Is.EqualTo(PopupQueueStatus.QueueStopped));
            }
        }
        [Test] public void ContextShutdown_EndsActiveAndPausedPendingRequests()
        {
            using(var f=Fixture())using(var queue=new UIPopupQueue(f.UI))
            {
                var active=queue.Enqueue(new Request(1));var pause=queue.AcquirePause();var pending=queue.Enqueue(new Request(2),deduplicationKey:"pending");
                f.UI.ShutdownAsync().GetAwaiter().GetResult();Assert.That(active.GetAwaiter().GetResult().Status,Is.EqualTo(PopupQueueStatus.ContextStopped));
                Assert.That(pending.GetAwaiter().GetResult().Status,Is.EqualTo(PopupQueueStatus.ContextStopped));pause.Dispose();
            }
        }
        [Test] public void PauseDuringOpen_DoesNotCancelCurrentAndBlocksNext()
        {
            using(var f=Fixture())using(var queue=new UIPopupQueue(f.UI))
            {
                PopupView.OpenGate=new UniTaskCompletionSource();var current=queue.Enqueue(new Request(99));var pause=queue.AcquirePause();var next=queue.Enqueue(new Request(2));
                Assert.That(current.Status.IsCompleted(),Is.False);PopupView.OpenGate.TrySetResult();Close(f);current.GetAwaiter().GetResult();
                Assert.That(PopupView.Opened,Is.EqualTo(new[]{99}));pause.Dispose();Close(f);next.GetAwaiter().GetResult();Assert.That(PopupView.Opened,Is.EqualTo(new[]{99,2}));
            }
        }
        [Test] public void StopDuringUncooperativeOpen_ReleasesLateLease()
        {
            using(var f=Fixture())using(var queue=new UIPopupQueue(f.UI))
            {
                PopupView.OpenGate=new UniTaskCompletionSource();var current=queue.Enqueue(new Request(99),deduplicationKey:"opening");var pause=queue.AcquirePause();
                var pending=queue.Enqueue(new Request(2),deduplicationKey:"pending");queue.StopAsync().GetAwaiter().GetResult();
                Assert.That(current.GetAwaiter().GetResult().Status,Is.EqualTo(PopupQueueStatus.QueueStopped));Assert.That(pending.GetAwaiter().GetResult().Status,Is.EqualTo(PopupQueueStatus.QueueStopped));
                Assert.That(f.Releases,Is.Zero);PopupView.OpenGate.TrySetResult();Assert.That(f.Releases,Is.EqualTo(1));pause.Dispose();
            }
        }
        [Test] public void ShutdownDuringOpen_WithMultiplePausesTerminatesAllRequests()
        {
            using(var f=Fixture())using(var queue=new UIPopupQueue(f.UI))
            {
                PopupView.OpenGate=new UniTaskCompletionSource();var active=queue.Enqueue(new Request(99),deduplicationKey:"active");
                var a=queue.AcquirePause();var b=queue.AcquirePause();var waiting=queue.Enqueue(new Request(1),deduplicationKey:"waiting");
                f.UI.ShutdownAsync().GetAwaiter().GetResult();Assert.That(active.GetAwaiter().GetResult().Status,Is.EqualTo(PopupQueueStatus.ContextStopped));
                Assert.That(waiting.GetAwaiter().GetResult().Status,Is.EqualTo(PopupQueueStatus.ContextStopped));
                a.Dispose();b.Dispose();PopupView.OpenGate.TrySetResult();Assert.That(f.Releases,Is.EqualTo(1));queue.StopAsync().GetAwaiter().GetResult();
            }
        }
        [Test] public void Contexts_DoNotSharePauseOrDeduplicationKeys()
        {
            using(var first=Fixture())using(var second=Fixture())using(var a=new UIPopupQueue(first.UI))using(var b=new UIPopupQueue(second.UI))
            {
                var pause=a.AcquirePause();var left=a.Enqueue(new Request(1),deduplicationKey:"same");var right=b.Enqueue(new Request(2),deduplicationKey:"same");
                Close(second);Assert.That(right.GetAwaiter().GetResult().Status,Is.EqualTo(PopupQueueStatus.Completed));Assert.That(left.Status.IsCompleted(),Is.False);
                pause.Dispose();Close(first);Assert.That(left.GetAwaiter().GetResult().Status,Is.EqualTo(PopupQueueStatus.Completed));
            }
        }
        [Test] public void FailedOpen_ReleasesKeyAndContinues()
        {
            using(var f=Fixture())using(var queue=new UIPopupQueue(f.UI))
            {
                Assert.That(queue.Enqueue(new Request(0,true),deduplicationKey:"key").GetAwaiter().GetResult().Status,Is.EqualTo(PopupQueueStatus.Failed));
                var next=queue.Enqueue(new Request(1),deduplicationKey:"key");Close(f);Assert.That(next.GetAwaiter().GetResult().Status,Is.EqualTo(PopupQueueStatus.Completed));
            }
        }
    }
}
