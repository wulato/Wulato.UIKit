using System;
using System.Linq;
using System.Threading;
using Cysharp.Threading.Tasks;
using NUnit.Framework;
using Wulato.UIKit.Tests.Support;
namespace Wulato.UIKit.Tests.PlayMode
{
    [Category("sessions")] public class SessionLifecycleTests : SessionTestBase
    {
        sealed class InvalidLease : IUIViewLease
        {
            public int Releases;public UIViewBase View => null;
            public void Dispose() => Releases++;
        }
        [Test] public void InvalidLoadedView_StillReleasesLeaseAndCompletesShutdown()
        {
            using(var f=new SessionFixture()) { var lease=new InvalidLease();f.Loader.Create=p=>UniTask.FromResult<IUIViewLease>(lease);var error=Assert.Throws<UIException>(()=>f.UI.OpenAsync<ProbeView>().GetAwaiter().GetResult());Assert.That(error.Code,Is.EqualTo(UIErrorCode.LoadFailed));Assert.That(lease.Releases,Is.EqualTo(1));Assert.That(f.UI.GetDiagnostics().Sessions,Is.Empty);Assert.That(f.UI.ShutdownAsync().Status,Is.EqualTo(UniTaskStatus.Succeeded)); }
        }
        [Test] public void DisposedSessionScope_CancelsOpeningAndTerminatesObservers()
        {
            var observer=new ProbeObserver();using(var f=new SessionFixture(null,observer)) { var raw=new SingleAwaitSource<bool>();CancellationToken stageToken=default;ProbeView.Open=(scope,ct)=> { stageToken=ct;scope.Dispose();return raw.Task.AsUniTask(); };var open=f.UI.OpenAsync<ProbeView>();Assert.That(stageToken.IsCancellationRequested,Is.True);Assert.That(open.Status,Is.EqualTo(UniTaskStatus.Canceled));Assert.Throws<OperationCanceledException>(()=>open.GetAwaiter().GetResult());Assert.That(observer.Terminates,Is.EqualTo(1));raw.Succeed(true);Assert.That(f.Loader.ReleaseCount,Is.EqualTo(1));Assert.That(raw.GetResultCount,Is.EqualTo(1)); }
        }
        [Test] public void ForegroundLoss_UpdatesDiagnostics()
        {
            using(var f=new SessionFixture()) { var session=f.UI.OpenAsync<ProbeView>().GetAwaiter().GetResult();f.Input.Registration.ForegroundChanged(session.Id,false);Assert.That(f.UI.GetDiagnostics().InputOwner,Is.Null);f.Input.Registration.ForegroundChanged(session.Id,true);Assert.That(f.UI.GetDiagnostics().InputOwner.Value.SessionId,Is.EqualTo(session.Id)); }
        }
        [Test] public void LayoutFails_RollsBackEnteredOpen() => StageFailure(false);
        [Test] public void EnterFails_RollsBackEnteredOpen() => StageFailure(true);
        void StageFailure(bool enter)
        {
            using(var f=new SessionFixture()) { Func<CancellationToken,UniTask> fail=ct=>UniTask.FromException(new Exception("stage"));if(enter)f.Enter=fail;else f.Layout=fail;var e=Assert.Throws<UIException>(()=>f.UI.OpenAsync<ProbeView>().GetAwaiter().GetResult());Assert.That(e.Code,Is.EqualTo(enter?UIErrorCode.TransitionFailed:UIErrorCode.LifecycleFailed));Assert.That(f.Loader.ReleaseCount,Is.EqualTo(1));Assert.That(ProbeView.Events.Count(x=>x=="closed"),Is.EqualTo(1));Assert.That(ProbeView.Events,Does.Not.Contain("close")); }
        }
        [Test] public void LayoutCancellation_DoesNotPublishOrDisposeRunningView() => StageCancellation(false);
        [Test] public void EnterCancellation_DoesNotPublishOrDisposeRunningView() => StageCancellation(true);
        void StageCancellation(bool enter)
        {
            using(var f=new SessionFixture())using(var ct=new CancellationTokenSource()) { var raw=new SingleAwaitSource<bool>();Func<CancellationToken,UniTask> wait=t=>raw.Task.AsUniTask();if(enter)f.Enter=wait;else f.Layout=wait;var open=f.UI.OpenAsync<ProbeView>(ct.Token);ct.Cancel();Assert.Throws<OperationCanceledException>(()=>open.GetAwaiter().GetResult());Assert.That(f.Loader.ReleaseCount,Is.Zero);Assert.That(f.UI.GetDiagnostics().IsolatedLeaseCount,Is.EqualTo(1));raw.Succeed(true);Assert.That(f.Loader.ReleaseCount,Is.EqualTo(1));Assert.That(raw.GetResultCount,Is.EqualTo(1)); }
        }
        [Test] public void OnOpenMayAwaitSchedulerAndLayout_WithoutNestedCommand()
        {
            using(var f=new SessionFixture()) { ProbeView.Open=async (scope,ct)=> { await scope.Scheduler.NextFrameAsync(ct);await scope.WaitLayoutAsync(ct); };var open=f.UI.OpenAsync<ProbeView>();Assert.That(open.Status,Is.EqualTo(UniTaskStatus.Pending));f.Scheduler.Advance();Assert.That(open.GetAwaiter().GetResult().State,Is.EqualTo(UIState.Open));Assert.That(f.LayoutCalls,Is.EqualTo(2)); }
        }
        [Test] public void ForegroundRestoration_UsesFreshScope_AndFailureTerminates()
        {
            using(var f=new SessionFixture()) { var session=f.UI.OpenAsync<ProbeView>().GetAwaiter().GetResult();var first=ProbeView.ForegroundScope;int released=0;first.Track(new ActionResource(()=>released++));f.Input.Registration.ForegroundChanged(session.Id,false);Assert.That(first.Token.IsCancellationRequested,Is.True);Assert.That(released,Is.EqualTo(1));f.Input.Registration.ForegroundChanged(session.Id,true);Assert.That(ProbeView.ForegroundScope,Is.Not.SameAs(first));Assert.That(ProbeView.ForegroundScope.Token.IsCancellationRequested,Is.False);f.Input.Registration.ForegroundChanged(session.Id,false);ProbeView.Foreground=()=>throw new Exception("restore");f.Input.Registration.ForegroundChanged(session.Id,true);Assert.That(session.State,Is.EqualTo(UIState.Closed));Assert.That(session.Closed.GetAwaiter().GetResult().Errors.Count,Is.GreaterThan(0)); }
        }
        [Test] public void NoForegroundGrant_SkipsForegroundButOpens()
        {
            using(var f=new SessionFixture()) { f.Input.Eligible=false;Assert.That(f.UI.OpenAsync<ProbeView>().GetAwaiter().GetResult().State,Is.EqualTo(UIState.Open));Assert.That(ProbeView.Events,Does.Not.Contain("foreground"));Assert.That(f.UI.GetDiagnostics().InputOwner,Is.Null); }
        }
        [Test] public void LoadFailure_HasNoViewOrObserverHooks()
        {
            var observer=new ProbeObserver();using(var f=new SessionFixture(null,observer)) { f.Loader.Create=p=>UniTask.FromException<IUIViewLease>(new Exception("load"));Assert.That(Assert.Throws<UIException>(()=>f.UI.OpenAsync<ProbeView>().GetAwaiter().GetResult()).Code,Is.EqualTo(UIErrorCode.LoadFailed));Assert.That(ProbeView.Events,Is.Empty);Assert.That(observer.Prepares,Is.Zero);Assert.That(f.UI.GetDiagnostics().Sessions,Is.Empty); }
        }
        [Test] public void CancelUncooperativeOpen_QuarantinesUntilSingleObservationCompletes()
        {
            using(var f=new SessionFixture())using(var ct=new CancellationTokenSource())
            {
                var raw=new SingleAwaitSource<bool>();ProbeView.Open=(scope,token)=>raw.Task.AsUniTask();var pending=f.UI.OpenAsync<ProbeView>(ct.Token);ct.Cancel();
                Assert.Throws<OperationCanceledException>(()=>pending.GetAwaiter().GetResult());Assert.That(f.Loader.ReleaseCount,Is.Zero);Assert.That(ProbeView.Events,Does.Not.Contain("closed"));
                Assert.That(f.UI.GetDiagnostics().Sessions,Is.Empty);Assert.That(f.UI.GetDiagnostics().IsolatedLeaseCount,Is.EqualTo(1));
                raw.Succeed(true);Assert.That(raw.GetResultCount,Is.EqualTo(1));Assert.That(f.Loader.ReleaseCount,Is.EqualTo(1));Assert.That(f.UI.GetDiagnostics().PendingCleanupCount,Is.Zero);
            }
        }
        [Test] public void Shutdown_InterruptsBlockedOpen_RejectsQueuedAndNewRequests()
        {
            using(var f=new SessionFixture())
            {
                var raw=new SingleAwaitSource<IUIViewLease>();UnityEngine.Transform parent=null;f.Loader.Create=p=> {parent=p;return raw.Task;};
                var open=f.UI.OpenAsync<ProbeView>();var queued=f.UI.OpenAsync<ProbeView>();var lease=f.Loader.NewLease(parent);var stop=f.UI.ShutdownAsync();
                Assert.Throws<OperationCanceledException>(()=>open.GetAwaiter().GetResult());Assert.That(Assert.Throws<UIException>(()=>queued.GetAwaiter().GetResult()).Code,Is.EqualTo(UIErrorCode.ContextStopped));
                Assert.That(stop.GetAwaiter().GetResult().PendingCleanupCount,Is.EqualTo(1));
                Assert.That(Assert.Throws<UIException>(()=>f.UI.OpenAsync<ProbeView>().GetAwaiter().GetResult()).Code,Is.EqualTo(UIErrorCode.ContextStopped));raw.Succeed(lease);Assert.That(f.Loader.ReleaseCount,Is.EqualTo(1));Assert.That(raw.GetResultCount,Is.EqualTo(1));
            }
        }
        [Test] public void PreparingAndTermination_UseRegisteredAndReverseOrder()
        {
            var events=new System.Collections.Generic.List<string>();var a=new OrderedObserver("a",events);var b=new OrderedObserver("b",events);
            using(var f=new SessionFixture(null,a,b)) { var session=f.UI.OpenAsync<ProbeView>().GetAwaiter().GetResult();session.CloseAsync().GetAwaiter().GetResult();Assert.That(events,Is.EqualTo(new[]{"prepare-a","prepare-b","terminate-b","terminate-a","release-b","release-a","end-b","end-a"})); }
        }
        sealed class OrderedObserver : IUISessionLifecycleObserver
        {
            readonly string name;readonly System.Collections.Generic.List<string> events;
            public OrderedObserver(string name,System.Collections.Generic.List<string> events) { this.name=name;this.events=events; }
            public IDisposable OnPreparing(UIViewBase view,UIArguments args,UISessionScope scope) { events.Add("prepare-"+name);return new ActionResource(()=>events.Add("release-"+name)); }
            public void OnTerminating(UISessionScope scope,UICloseReason reason) { Assert.That(scope.Token.IsCancellationRequested,Is.True);events.Add("terminate-"+name); }
            public void OnTerminated(UICompletion result) => events.Add("end-"+name);
        }
        [Test] public void OpeningTokenCanceledAfterCommit_DoesNotClose()
        {
            using(var f=new SessionFixture())using(var ct=new CancellationTokenSource()) { var s=f.UI.OpenAsync<ProbeView>(ct.Token).GetAwaiter().GetResult();ct.Cancel();Assert.That(s.State,Is.EqualTo(UIState.Open));Assert.That(ProbeView.Scope.Token.IsCancellationRequested,Is.False);Assert.That(ProbeView.Events.Take(4),Is.EqualTo(new[]{"create","awake","open","foreground"})); }
        }
        [Test] public void PreparingFails_ReleasesEarlierBindings()
        {
            int released=0;var first=new ProbeObserver{Preparing=()=>new ActionResource(()=>released++)};var second=new ProbeObserver{Preparing=()=>throw new Exception("preparing")};
            using(var f=new SessionFixture(null,first,second)) { Assert.Throws<UIException>(()=>f.UI.OpenAsync<ProbeView>().GetAwaiter().GetResult());Assert.That(released,Is.EqualTo(1));Assert.That(first.Terminates,Is.EqualTo(1));Assert.That(first.Terminated,Is.EqualTo(1));Assert.That(second.Terminates+second.Terminated,Is.Zero);Assert.That(ProbeView.Events,Does.Not.Contain("closed"));Assert.That(f.Loader.ReleaseCount,Is.EqualTo(1)); }
        }
        [Test] public void ForegroundEntryFails_OpenFails()
        {
            using(var f=new SessionFixture()) { ProbeView.Foreground=()=>throw new Exception("foreground");Assert.Throws<UIException>(()=>f.UI.OpenAsync<ProbeView>().GetAwaiter().GetResult());Assert.That(ProbeView.Events.Count(x=>x=="closed"),Is.EqualTo(1));Assert.That(ProbeView.Events,Does.Not.Contain("close"));Assert.That(f.Loader.ReleaseCount,Is.EqualTo(1));Assert.That(f.UI.GetDiagnostics().Sessions,Is.Empty); }
        }
        [Test] public void CreateFails_DoesNotCallSessionCloseHooks()
        {
            using(var f=new SessionFixture()) { ProbeView.Create=()=>throw new Exception("create");Assert.Throws<UIException>(()=>f.UI.OpenAsync<ProbeView>().GetAwaiter().GetResult());Assert.That(ProbeView.Events,Is.EqualTo(new[]{"create","dispose"}));Assert.That(f.Loader.ReleaseCount,Is.EqualTo(1)); }
        }
        [Test] public void OpenFails_ClosedAndDisposeRunOnce_ObserverErrorsAreRetained()
        {
            var observer=new ProbeObserver{OnEnd=()=>throw new Exception("terminated")};using(var f=new SessionFixture(null,observer)) { ProbeView.Open=(s,ct)=>UniTask.FromException(new Exception("open"));Assert.Throws<UIException>(()=>f.UI.OpenAsync<ProbeView>().GetAwaiter().GetResult());Assert.That(ProbeView.Events.Count(x=>x=="closed"),Is.EqualTo(1));Assert.That(ProbeView.Events.Count(x=>x=="dispose"),Is.EqualTo(1));Assert.That(observer.Terminates,Is.EqualTo(1));Assert.That(observer.Terminated,Is.EqualTo(1));Assert.That(f.Errors.Count,Is.GreaterThanOrEqualTo(1)); }
        }
    }
}
