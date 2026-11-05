using System;
using System.Linq;
using System.Threading;
using Cysharp.Threading.Tasks;
using NUnit.Framework;
using Wulato.UIKit.Tests.Support;
namespace Wulato.UIKit.Tests.PlayMode
{
    [Category("sessions")] public class ClosePolicyTests : SessionTestBase
    {
        static async UniTask<UICompletion> Observe(UniTask<UICompletion> result) => await result;
        [Test] public void BatchInterruptedByShutdown_PreservesForcedReasonForEveryItem()
        {
            using(var f=new SessionFixture(b=>b.Add<ProbeView>(new UIViewTypeKey("probe"),UIViewAsset.Resources("probe"),multiplicity:UIMultiplicity.Multi))) { var a=f.UI.OpenAsync<ProbeView>().GetAwaiter().GetResult();var b=f.UI.OpenAsync<ProbeView>().GetAwaiter().GetResult();var gate=new UniTaskCompletionSource();int calls=0;ProbeView.Close=(r,ct)=>++calls==1?gate.Task:UniTask.CompletedTask;var batch=f.UI.CloseAllAsync();var stop=f.UI.ShutdownAsync();gate.TrySetResult();var results=batch.GetAwaiter().GetResult().Completions;Assert.That(results.Select(r=>r.Reason),Is.All.EqualTo(UICloseReason.ContextShutdown));Assert.That(a.Closed.GetAwaiter().GetResult().Reason,Is.EqualTo(UICloseReason.ContextShutdown));Assert.That(b.Closed.GetAwaiter().GetResult().Reason,Is.EqualTo(UICloseReason.ContextShutdown));Assert.That(stop.GetAwaiter().GetResult().Completions.Count,Is.EqualTo(2)); }
        }
        [Test] public void CloseLock_CoversLifecycleAndExit_AndReleasesAfterFailure()
        {
            using(var f=new SessionFixture()) { var session=f.UI.OpenAsync<ProbeView>().GetAwaiter().GetResult();var closeGate=new UniTaskCompletionSource();var exitGate=new UniTaskCompletionSource();ProbeView.Close=(r,ct)=>closeGate.Task;f.Exit=ct=>exitGate.Task;var closed=session.CloseAsync();int duringClose=f.Input.Locks;closeGate.TrySetResult();int duringExit=f.Input.Locks;exitGate.TrySetException(new Exception("exit"));closed.GetAwaiter().GetResult();Assert.That(duringClose,Is.EqualTo(1));Assert.That(duringExit,Is.EqualTo(1));Assert.That(f.Input.Locks,Is.Zero); }
        }
        [Test] public void UserVeto_CanRetryWhenBusinessStateChanges()
        {
            using(var f=new SessionFixture()) { var session=f.UI.OpenAsync<ProbeView>().GetAwaiter().GetResult();var view=(ProbeView)f.Loader.LastView;ProbeView.AllowClose=false;view.ClickClose();ProbeView.AllowClose=true;view.ClickClose();Assert.That(session.State,Is.EqualTo(UIState.Closed));Assert.That(ProbeView.Events.Count(x=>x=="can-close"),Is.EqualTo(2)); }
        }
        [Test] public void CanCloseException_DoesNotDisableLaterRequests()
        {
            using(var f=new SessionFixture()) { var session=f.UI.OpenAsync<ProbeView>().GetAwaiter().GetResult();var view=(ProbeView)f.Loader.LastView;ProbeView.CloseDecision=()=>throw new Exception("decision");view.ClickClose();ProbeView.CloseDecision=null;view.ClickClose();Assert.That(session.State,Is.EqualTo(UIState.Closed)); }
        }
        [Test] public void MultiAndStaleHandle_ReleaseOnlyTheirOwnSessions()
        {
            using(var f=new SessionFixture(b=>b.Add<ProbeView>(new UIViewTypeKey("probe"),UIViewAsset.Resources("probe"),multiplicity:UIMultiplicity.Multi))) { var a=f.UI.OpenAsync<ProbeView>().GetAwaiter().GetResult();var b=f.UI.OpenAsync<ProbeView>().GetAwaiter().GetResult();Assert.That(a.Id,Is.Not.EqualTo(b.Id));a.CloseAsync().GetAwaiter().GetResult();a.CloseAsync().GetAwaiter().GetResult();Assert.That(b.State,Is.EqualTo(UIState.Open));Assert.That(f.Loader.ReleaseCount,Is.EqualTo(1));Assert.That(f.UI.CloseAllOfTypeAsync<ProbeView>().GetAwaiter().GetResult().Completions.Count,Is.EqualTo(1));Assert.That(f.Loader.ReleaseCount,Is.EqualTo(2)); }
        }
        [Test] public void CloseAndExitErrors_AccumulateBeforeClosed()
        {
            var observer=new ProbeObserver{OnEnd=()=>throw new Exception("end")};using(var f=new SessionFixture(null,observer)) { var session=f.UI.OpenAsync<ProbeView>().GetAwaiter().GetResult();ProbeView.Close=(r,ct)=>UniTask.FromException(new Exception("close"));f.Exit=ct=>UniTask.FromException(new Exception("exit"));ProbeView.ClosedHook=()=>throw new Exception("closed");var result=session.CloseAsync().GetAwaiter().GetResult();Assert.That(result.Errors.Select(e=>e.Message),Is.EquivalentTo(new[]{"close","exit","closed","end"}));Assert.That(f.Loader.ReleaseCount,Is.EqualTo(1));Assert.That(f.UI.GetDiagnostics().Sessions,Is.Empty); }
        }
        [Test] public void TwoClosedWaiters_ReceiveSameResult()
        {
            using(var f=new SessionFixture()) { var s=f.UI.OpenAsync<ProbeView>().GetAwaiter().GetResult();var gate=new UniTaskCompletionSource();ProbeView.Close=(r,ct)=>gate.Task;var a=Observe(s.Closed);var b=Observe(s.Closed);var c=s.CloseAsync(UICloseReason.Owner);var d=s.CloseAsync(UICloseReason.Back);gate.TrySetResult();var result=a.GetAwaiter().GetResult();Assert.That(b.GetAwaiter().GetResult(),Is.SameAs(result));Assert.That(c.GetAwaiter().GetResult(),Is.SameAs(result));Assert.That(d.GetAwaiter().GetResult(),Is.SameAs(result));Assert.That(result.Reason,Is.EqualTo(UICloseReason.Owner));Assert.That(ProbeView.Events.Count(x=>x=="close"),Is.EqualTo(1)); }
        }
        [Test] public void CanCloseVeto_DeduplicatesUserRequests_OwnerBypasses()
        {
            using(var f=new SessionFixture()) { var s=f.UI.OpenAsync<ProbeView>().GetAwaiter().GetResult();int confirmations=0;bool pending=false;ProbeView.CloseDecision=()=> { if(!pending) { pending=true;confirmations++; }return false; };var view=(ProbeView)f.Loader.LastView;view.ClickClose();view.ClickClose();Assert.That(s.State,Is.EqualTo(UIState.Open));Assert.That(confirmations,Is.EqualTo(1));s.CloseAsync().GetAwaiter().GetResult();Assert.That(s.State,Is.EqualTo(UIState.Closed)); }
        }
        [Test] public void Shutdown_OverridesAcceptedCloseAndSharesResult()
        {
            using(var f=new SessionFixture()) { var s=f.UI.OpenAsync<ProbeView>().GetAwaiter().GetResult();var gate=new UniTaskCompletionSource();ProbeView.Close=(r,ct)=>gate.Task;var close=s.CloseAsync(UICloseReason.Back);var shutdown=f.UI.ShutdownAsync();var again=f.UI.ShutdownAsync();gate.TrySetResult();Assert.That(close.GetAwaiter().GetResult().Reason,Is.EqualTo(UICloseReason.ContextShutdown));var result=shutdown.GetAwaiter().GetResult();Assert.That(result,Is.SameAs(again.GetAwaiter().GetResult()));Assert.That(result.Completions.Count,Is.EqualTo(1));Assert.That(result.Completions[0],Is.SameAs(close.GetAwaiter().GetResult()));Assert.That(f.UI.GetDiagnostics().State,Is.EqualTo(UIContextState.Stopped)); }
        }
        [Test] public void ThrowingSink_DoesNotStrandClosedOrShutdownContinuation()
        {
            using(var f=new SessionFixture()) { var s=f.UI.OpenAsync<ProbeView>().GetAwaiter().GetResult();f.SinkThrows=true;ProbeView.Close=(r,ct)=>UniTask.FromException(new Exception("close"));UniTask<UIShutdownCompletion> shutdown=default;var awaiter=s.Closed.GetAwaiter();awaiter.OnCompleted(()=>shutdown=f.UI.ShutdownAsync());Assert.That(s.CloseAsync().GetAwaiter().GetResult().Errors.Count,Is.GreaterThan(0));Assert.That(shutdown.GetAwaiter().GetResult().PendingCleanupCount,Is.Zero); }
        }
    }
}
