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
        public static UniTaskCompletionSource OpenGate,CloseGate;public static Action Closing;public static bool FailClose;
        protected override UniTask OnOpenAsync(int args,UISessionScope scope,CancellationToken ct){Opened.Add(args);return args==99&&OpenGate!=null?OpenGate.Task:UniTask.CompletedTask;}
        protected override UniTask OnCloseAsync(UICloseReason reason,CancellationToken ct){Closing?.Invoke();if(FailClose)throw new InvalidOperationException("close");return CloseGate?.Task??UniTask.CompletedTask;}
        public void Dismiss()=>RequestClose();
    }
    public sealed class PopupRoot : UIView { }
    public sealed class EmptyPopup : UIView { public void Dismiss()=>RequestClose(); }
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
            PopupView.Opened.Clear();PopupView.OpenGate=null;PopupView.CloseGate=null;PopupView.Closing=null;PopupView.FailClose=false;var registry=new UIRegistryBuilder();registry.Add<PopupView,int>(new UIViewTypeKey("popup"),UIViewAsset.Resources("popup"),UILayer.Modal,closePolicy:UIClosePolicy.CacheOne);
            registry.Add<PopupRoot>(new UIViewTypeKey("root"),UIViewAsset.Resources("root"));
            registry.Add<EmptyPopup>(new UIViewTypeKey("empty"),UIViewAsset.Resources("empty"),UILayer.Modal);
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
        [Test] public void InvalidPopupRegistration_DoesNotLoadOrCreatePageHistory()
        {
            using(var f=Fixture())
            {
                Assert.That(f.UI.PopupAsync<PopupRoot>().GetAwaiter().GetResult().Status,Is.EqualTo(PopupQueueStatus.Failed));
                var queue=f.UI.GetService<IUIPopupQueue>();Assert.That(queue.Enqueue(new UIViewTypeKey("popup"),UIArguments.None).GetAwaiter().GetResult().Status,Is.EqualTo(PopupQueueStatus.Failed));Assert.That(f.Creates,Is.Zero);
                var next=f.UI.PopupAsync<PopupView,int>(1);Close(f);Assert.That(next.GetAwaiter().GetResult().Status,Is.EqualTo(PopupQueueStatus.Completed));
            }
        }
        [Test] public void GenericFacade_UsesTheDefaultQueueForTypedAndParameterlessViews()
        {
            using(var f=Fixture())
            {
                var first=f.UI.PopupAsync<PopupView,int>(7);var next=f.UI.PopupAsync<EmptyPopup>();
                Assert.That(PopupView.Opened,Is.EqualTo(new[]{7}));Close(f);Assert.That(first.GetAwaiter().GetResult().Status,Is.EqualTo(PopupQueueStatus.Completed));
                Assert.That(f.LastView,Is.TypeOf<EmptyPopup>());((EmptyPopup)f.LastView).Dismiss();
                Assert.That(next.GetAwaiter().GetResult().Status,Is.EqualTo(PopupQueueStatus.Completed));
            }
        }
        [Test] public void WaitingCancellation_RemovesOnlyItsItemAndReleasesTheKey()
        {
            using(var f=Fixture())using(var caller=new CancellationTokenSource())
            {
                var queue=f.UI.GetService<IUIPopupQueue>();var a=queue.AcquirePause();var b=queue.AcquirePause();
                var canceled=f.UI.PopupAsync<PopupView,int>(1,deduplicationKey:"one",cancellationToken:caller.Token);
                var next=f.UI.PopupAsync<PopupView,int>(2);caller.Cancel();Assert.Throws<OperationCanceledException>(()=>canceled.GetAwaiter().GetResult());
                var reused=f.UI.PopupAsync<PopupView,int>(3,deduplicationKey:"one");a.Dispose();Assert.That(PopupView.Opened,Is.Empty);b.Dispose();
                Assert.That(PopupView.Opened,Is.EqualTo(new[]{2}));Close(f);next.GetAwaiter().GetResult();Close(f);reused.GetAwaiter().GetResult();
                Assert.That(PopupView.Opened,Is.EqualTo(new[]{2,3}));
            }
        }
        [Test] public void OpeningCancellation_RollsBackAndAdvancesWhileLateCleanupRemainsIsolated()
        {
            using(var f=Fixture())using(var caller=new CancellationTokenSource())
            {
                PopupView.OpenGate=new UniTaskCompletionSource();var canceled=f.UI.PopupAsync<PopupView,int>(99,deduplicationKey:"same",cancellationToken:caller.Token);
                var next=f.UI.PopupAsync<PopupView,int>(2);caller.Cancel();Assert.Throws<OperationCanceledException>(()=>canceled.GetAwaiter().GetResult());
                Assert.That(PopupView.Opened,Is.EqualTo(new[]{99,2}));Assert.That(f.UI.GetDiagnostics().PendingCleanupCount,Is.GreaterThan(0));
                var reused=f.UI.PopupAsync<PopupView,int>(3,deduplicationKey:"same");PopupView.OpenGate.TrySetResult();Assert.That(f.Releases,Is.EqualTo(1));
                Close(f);next.GetAwaiter().GetResult();Close(f);reused.GetAwaiter().GetResult();
            }
        }
        [Test] public void DisplayedCancellation_HoldsTheClosedAndDeduplicationBarriers()
        {
            using(var f=Fixture())using(var caller=new CancellationTokenSource())
            {
                var gate=new UniTaskCompletionSource();PopupView.CloseGate=gate;
                var canceled=f.UI.PopupAsync<PopupView,int>(1,deduplicationKey:"same",cancellationToken:caller.Token);var next=f.UI.PopupAsync<PopupView,int>(2);
                caller.Cancel();Assert.That(canceled.Status.IsCompleted(),Is.False);Assert.That(next.Status.IsCompleted(),Is.False);
                Assert.That(f.UI.PopupAsync<PopupView,int>(3,deduplicationKey:"same").GetAwaiter().GetResult().Status,Is.EqualTo(PopupQueueStatus.Duplicate));
                PopupView.CloseGate=null;gate.TrySetResult();Assert.Throws<OperationCanceledException>(()=>canceled.GetAwaiter().GetResult());
                var reused=f.UI.PopupAsync<PopupView,int>(4,deduplicationKey:"same");Close(f);next.GetAwaiter().GetResult();Close(f);reused.GetAwaiter().GetResult();
                Assert.That(PopupView.Opened,Is.EqualTo(new[]{1,2,4}));
            }
        }
        [Test] public void CancelFromCloseHook_DoesNotNestACommandOrReleaseTheKeyEarly()
        {
            using(var f=Fixture())using(var caller=new CancellationTokenSource())
            {
                var canceled=f.UI.PopupAsync<PopupView,int>(1,deduplicationKey:"key",cancellationToken:caller.Token);
                PopupView.Closing=()=>{caller.Cancel();Assert.That(f.UI.PopupAsync<PopupView,int>(3,deduplicationKey:"key").GetAwaiter().GetResult().Status,Is.EqualTo(PopupQueueStatus.Duplicate));};
                Close(f);Assert.Throws<OperationCanceledException>(()=>canceled.GetAwaiter().GetResult());Assert.That(f.Errors,Is.Empty);
            }
        }
        [Test] public void PreCanceledPopup_DoesNotLoadAndDoesNotStopTheQueue()
        {
            using(var f=Fixture())using(var caller=new CancellationTokenSource())
            {
                caller.Cancel();Assert.Throws<OperationCanceledException>(()=>f.UI.PopupAsync<PopupView,int>(1,cancellationToken:caller.Token).GetAwaiter().GetResult());Assert.That(f.Creates,Is.Zero);
                var next=f.UI.PopupAsync<PopupView,int>(2);Close(f);Assert.That(next.GetAwaiter().GetResult().Status,Is.EqualTo(PopupQueueStatus.Completed));
            }
        }
        [Test] public void CloseErrorsAndPendingCleanup_RemainVisibleInTheResult()
        {
            using(var f=Fixture())
            {
                PopupView.FailClose=true;var failed=f.UI.PopupAsync<PopupView,int>(1);Close(f);var result=failed.GetAwaiter().GetResult();Assert.That(result.Status,Is.EqualTo(PopupQueueStatus.Failed));Assert.That(result.Completion.Errors,Is.Not.Empty);
                PopupView.FailClose=false;PopupView.CloseGate=new UniTaskCompletionSource();var pending=f.UI.PopupAsync<PopupView,int>(2);Close(f);f.Clock.Advance(3);
                result=pending.GetAwaiter().GetResult();Assert.That(result.Status,Is.EqualTo(PopupQueueStatus.Failed));Assert.That(result.Completion.HasPendingCleanup,Is.True);PopupView.CloseGate.TrySetResult();
            }
        }
        [Test] public void Shutdown_OverridesCancellationWhileTheClosedBarrierIsPending()
        {
            using(var f=Fixture())using(var caller=new CancellationTokenSource())
            {
                PopupView.CloseGate=new UniTaskCompletionSource();var item=f.UI.PopupAsync<PopupView,int>(1,cancellationToken:caller.Token);caller.Cancel();
                var stop=f.UI.ShutdownAsync();f.Clock.Advance(3);stop.GetAwaiter().GetResult();
                Assert.That(item.GetAwaiter().GetResult().Status,Is.EqualTo(PopupQueueStatus.ContextStopped));PopupView.CloseGate.TrySetResult();
            }
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
