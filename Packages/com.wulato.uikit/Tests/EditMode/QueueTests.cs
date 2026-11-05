using System;
using System.Linq;
using System.Threading;
using Cysharp.Threading.Tasks;
using NUnit.Framework;
using Wulato.UIKit.Tests.Support;
namespace Wulato.UIKit.Tests
{
    [Category("sessions")] public class QueueTests : SessionTestBase
    {
        [Test] public void DiagnosticsAreSnapshots_AndServicesRejectAfterStop()
        {
            using(var f=new SessionFixture()) { Assert.That(f.UI.GetService<SessionFixture>(),Is.SameAs(f));Assert.Throws<InvalidOperationException>(()=>f.UI.GetService<IDisposable>());var session=f.UI.OpenAsync<ProbeView>().GetAwaiter().GetResult();var snapshot=f.UI.GetDiagnostics();session.CloseAsync().GetAwaiter().GetResult();Assert.That(snapshot.Sessions.Count,Is.EqualTo(1));Assert.That(snapshot.Sessions[0].State,Is.EqualTo(UIState.Open));Assert.That(f.UI.GetDiagnostics().Sessions,Is.Empty);f.UI.ShutdownAsync().GetAwaiter().GetResult();Assert.That(Assert.Throws<UIException>(()=>f.UI.GetService<SessionFixture>()).Code,Is.EqualTo(UIErrorCode.ContextStopped)); }
        }
        [Test] public void SessionCancellationCallback_NestedCommandFailsWithoutDeadlock()
        {
            using(var f=new SessionFixture()) { var session=f.UI.OpenAsync<ProbeView>().GetAwaiter().GetResult();UniTask<IUISession<ProbeView>> nested=default;ProbeView.Scope.Token.Register(()=>nested=f.UI.OpenAsync<ProbeView>());session.CloseAsync().GetAwaiter().GetResult();Assert.That(Assert.Throws<UIException>(()=>nested.GetAwaiter().GetResult()).Code,Is.EqualTo(UIErrorCode.InvalidRequest)); }
        }
        [Test] public void QueuedCancellation_DoesNotStartAnotherLoad()
        {
            using(var f=new SessionFixture())using(var ct=new CancellationTokenSource()) { var gate=new UniTaskCompletionSource();ProbeView.Open=(s,t)=>gate.Task;var first=f.UI.OpenAsync<ProbeView>();var queued=f.UI.OpenAsync<ProbeView>(ct.Token);ct.Cancel();gate.TrySetResult();first.GetAwaiter().GetResult();Assert.Throws<OperationCanceledException>(()=>queued.GetAwaiter().GetResult());Assert.That(f.Loader.CreateCount,Is.EqualTo(1)); }
        }
        [Test] public void CloseThenOpen_UsesFifo()
        {
            using(var f=new SessionFixture())
            {
                var first=f.UI.OpenAsync<ProbeView>().GetAwaiter().GetResult();var gate=new UniTaskCompletionSource();ProbeView.Close=(r,ct)=>gate.Task;
                var close=first.CloseAsync();var open=f.UI.OpenAsync<ProbeView>();Assert.That(f.Loader.CreateCount,Is.EqualTo(1));gate.TrySetResult();
                Assert.That(close.GetAwaiter().GetResult().SessionId,Is.EqualTo(first.Id));var second=open.GetAwaiter().GetResult();Assert.That(second.Id,Is.Not.EqualTo(first.Id));Assert.That(f.Loader.CreateCount,Is.EqualTo(2));
            }
        }
        [Test] public void FirstOpenFails_SecondMayRetry()
        {
            using(var f=new SessionFixture()) { var gate=new UniTaskCompletionSource();ProbeView.Open=(s,ct)=>gate.Task;var first=f.UI.OpenAsync<ProbeView>();var second=f.UI.OpenAsync<ProbeView>();ProbeView.Open=null;gate.TrySetException(new Exception("open"));Assert.Throws<UIException>(()=>first.GetAwaiter().GetResult());Assert.That(second.GetAwaiter().GetResult().State,Is.EqualTo(UIState.Open)); }
        }
        [Test] public void ExternalOpenDuringAwait_IsQueuedButSynchronousNestedOpenFails()
        {
            using(var f=new SessionFixture()) { var gate=new UniTaskCompletionSource();UniTask<IUISession<ProbeView>> nested=default;ProbeView.Open=(s,ct)=> { nested=f.UI.OpenAsync<ProbeView>();return gate.Task; };var first=f.UI.OpenAsync<ProbeView>();Assert.That(Assert.Throws<UIException>(()=>nested.GetAwaiter().GetResult()).Code,Is.EqualTo(UIErrorCode.InvalidRequest));var external=f.UI.OpenAsync<ProbeView>();Assert.That(external.Status,Is.EqualTo(UniTaskStatus.Pending));gate.TrySetResult();first.GetAwaiter().GetResult();Assert.That(Assert.Throws<UIException>(()=>external.GetAwaiter().GetResult()).Code,Is.EqualTo(UIErrorCode.AlreadyOpen)); }
        }
        [Test] public void CompletionQueue_ContainsThrowingNotificationAndContinues()
        {
            var q=new UICompletionQueue();int n=0;q.Post(()=>throw new Exception("notification"));q.Post(()=>n++);Assert.DoesNotThrow(()=>q.Drain());Assert.That(n,Is.EqualTo(1));
        }
        [Test] public void ClosedContinuation_ReopensWithoutReentry()
        {
            using(var f=new SessionFixture()) { var first=f.UI.OpenAsync<ProbeView>().GetAwaiter().GetResult();UniTask<IUISession<ProbeView>> reopened=default;bool clean=false;var awaiter=first.Closed.GetAwaiter();awaiter.OnCompleted(()=> { clean=f.UI.GetDiagnostics().Sessions.Count==0;reopened=f.UI.OpenAsync<ProbeView>();Assert.That(reopened.Status,Is.EqualTo(UniTaskStatus.Pending)); });first.CloseAsync().GetAwaiter().GetResult();Assert.That(clean,Is.True);Assert.That(reopened.GetAwaiter().GetResult().Id,Is.Not.EqualTo(first.Id)); }
        }
        [Test] public void BatchSnapshot_DoesNotIncludeLaterOpen_AndRejectsBaseType()
        {
            using(var f=new SessionFixture()) { f.UI.OpenAsync<ProbeView>().GetAwaiter().GetResult();var gate=new UniTaskCompletionSource();ProbeView.Close=(r,ct)=>gate.Task;var batch=f.UI.CloseAllAsync();var next=f.UI.OpenAsync<ProbeView>();gate.TrySetResult();Assert.That(batch.GetAwaiter().GetResult().Completions.Count,Is.EqualTo(1));Assert.That(next.GetAwaiter().GetResult().State,Is.EqualTo(UIState.Open));Assert.That(Assert.Throws<UIException>(()=>f.UI.CloseAllOfTypeAsync<UIView>().GetAwaiter().GetResult()).Code,Is.EqualTo(UIErrorCode.InvalidRequest)); }
        }
    }
}
