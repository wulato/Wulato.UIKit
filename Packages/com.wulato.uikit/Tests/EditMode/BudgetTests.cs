using System;
using System.Linq;
using System.Threading;
using Cysharp.Threading.Tasks;
using NUnit.Framework;
using Wulato.UIKit.Tests.Support;
namespace Wulato.UIKit.Tests
{
    [Category("cleanup")]
    public class BudgetTests : SessionTestBase
    {
        [Test] public void MaxShutdownBudget_RemainingDoesNotOverflow()
        {
            var clock = new ManualScheduler();
            using (var budget = new UIShutdownBudget(clock, TimeSpan.MaxValue))
            {
                budget.Start(); Assert.That(budget.Remaining, Is.EqualTo(TimeSpan.MaxValue));
                clock.Advance(1); Assert.That(budget.Remaining, Is.GreaterThan(TimeSpan.Zero).And.LessThan(TimeSpan.MaxValue));
            }
        }
        [Test] public void MaxOpenBudget_DoesNotOverflowBeforeLifecycle()
        {
            using (var f = new SessionFixture(new UIContextSettings { OpenTimeout = TimeSpan.MaxValue }, null))
                Assert.That(f.UI.OpenAsync<ProbeView>().GetAwaiter().GetResult().State, Is.EqualTo(UIState.Open));
        }
        [Test] public void CompletedLoadFaultRacingCancellation_IsStillObserved() => CompletedFaultRace(true);
        [Test] public void CompletedHookFaultRacingCancellation_IsStillObserved() => CompletedFaultRace(false);
        void CompletedFaultRace(bool loading)
        {
            using (var f = new SessionFixture()) using (var ct = new CancellationTokenSource())
            {
                var marker = new Exception("completed-fault");
                var rawLoad = new SingleAwaitSource<IUIViewLease>(); var rawHook = new SingleAwaitSource<bool>();
                if (loading) f.Loader.Create = parent => { rawLoad.Fail(marker); ct.Cancel(); return rawLoad.Task; };
                else ProbeView.Open = (scope, token) => { rawHook.Fail(marker); ct.Cancel(); return rawHook.Task.AsUniTask(); };
                var open = f.UI.OpenAsync<ProbeView>(ct.Token);
                Assert.Throws<OperationCanceledException>(() => open.GetAwaiter().GetResult());
                Assert.That(loading ? rawLoad.GetResultCount : rawHook.GetResultCount, Is.EqualTo(1));
                Assert.That(f.Errors.Exists(e => e.Stage == (loading ? "late-load" : "late-open") && e.InnerException == marker), Is.True);
                Assert.That(f.UI.GetDiagnostics().PendingCleanupCount, Is.Zero);
            }
        }
        [Test] public void ShutdownBudget_StartIsIdempotentAndUsesRemainingTime()
        {
            var clock = new ManualScheduler(); var budget = new UIShutdownBudget(clock, TimeSpan.FromSeconds(5));
            budget.Start(); clock.Advance(3); budget.Start(); Assert.That(budget.Remaining.TotalSeconds, Is.EqualTo(2));
            clock.Advance(2); Assert.That(budget.Remaining, Is.EqualTo(TimeSpan.Zero)); Assert.That(budget.Token.IsCancellationRequested, Is.True);
            budget.Dispose();
        }
        [Test] public void LoadTimeout_ReleasesQueueWithoutConsumingSourceTwice()
        {
            using (var f = new SessionFixture())
            {
                var raw = new SingleAwaitSource<IUIViewLease>(); UnityEngine.Transform parent = null;
                f.Loader.Create = p => { parent = p; return raw.Task; }; var open = f.UI.OpenAsync<ProbeView>();
                f.Scheduler.Advance(30); Assert.That(open.Status, Is.EqualTo(UniTaskStatus.Faulted));
                Assert.That(Assert.Throws<UIException>(() => open.GetAwaiter().GetResult()).Code, Is.EqualTo(UIErrorCode.Timeout));
                var stop = f.UI.ShutdownAsync().GetAwaiter().GetResult(); Assert.That(stop.PendingCleanupCount, Is.EqualTo(1));
                raw.Succeed(f.Loader.NewLease(parent)); Assert.That(raw.GetResultCount, Is.EqualTo(1)); Assert.That(f.Loader.ReleaseCount, Is.EqualTo(1));
            }
        }
        [Test] public void OpenAndLayout_ShareOneLifecycleBudget()
        {
            using (var f = new SessionFixture())
            {
                var openSource = new SingleAwaitSource<bool>(); var layoutSource = new SingleAwaitSource<bool>();
                ProbeView.Open = (s, ct) => openSource.Task.AsUniTask(); f.Layout = ct => layoutSource.Task.AsUniTask();
                var open = f.UI.OpenAsync<ProbeView>(); f.Scheduler.Advance(7); openSource.Succeed(true); f.Scheduler.Advance(3);
                Assert.That(open.Status, Is.EqualTo(UniTaskStatus.Faulted)); Assert.That(Assert.Throws<UIException>(() => open.GetAwaiter().GetResult()).Code, Is.EqualTo(UIErrorCode.Timeout));
                Assert.That(f.Loader.ReleaseCount, Is.Zero); layoutSource.Succeed(true);
                Assert.That(f.Loader.ReleaseCount, Is.EqualTo(1)); Assert.That(openSource.GetResultCount, Is.EqualTo(1)); Assert.That(layoutSource.GetResultCount, Is.EqualTo(1));
                ProbeView.Measurements.Add("open-layout:clock=" + f.Scheduler.UnscaledTime + ":open-consumed=" + openSource.GetResultCount + ":layout-consumed=" + layoutSource.GetResultCount);
            }
        }
        [Test] public void ShutdownTotalBudget_IsNotResetForEachSession()
        {
            var settings = new UIContextSettings { CloseTimeout = TimeSpan.FromSeconds(10), ExitTimeout = TimeSpan.FromSeconds(10), ShutdownBudget = TimeSpan.FromSeconds(5) };
            using (var f = new SessionFixture(settings, b => b.Add<ProbeView>(new UIViewTypeKey("p"), UIViewAsset.Resources("p"), multiplicity: UIMultiplicity.Multi)))
            {
                for (int i = 0; i < 3; i++) f.UI.OpenAsync<ProbeView>().GetAwaiter().GetResult();
                var first = new SingleAwaitSource<bool>(); var second = new SingleAwaitSource<bool>(); int calls = 0;
                ProbeView.Close = (r, ct) => { calls++; ProbeView.Measurements.Add("close:call=" + calls + ":clock=" + f.Scheduler.UnscaledTime); return (calls == 1 ? first.Task : second.Task).AsUniTask(); };
                var stop = f.UI.ShutdownAsync(); f.Scheduler.Advance(3); first.Succeed(true); f.Scheduler.Advance(2);
                Assert.That(stop.Status, Is.EqualTo(UniTaskStatus.Succeeded)); var result = stop.GetAwaiter().GetResult();
                Assert.That(result.Completions.Count, Is.EqualTo(3)); Assert.That(calls, Is.EqualTo(2));
                Assert.That(result.Completions.All(c => c.Reason == UICloseReason.ContextShutdown), Is.True);
                Assert.That(result.PendingCleanupCount, Is.EqualTo(1));
                ProbeView.Measurements.Add("shutdown:clock=" + f.Scheduler.UnscaledTime + ":calls=" + calls + ":terminal=" + result.Completions.Count + ":pending=" + result.PendingCleanupCount);
                second.Succeed(true);
                Assert.That(f.Loader.ReleaseCount, Is.EqualTo(3)); Assert.That(first.GetResultCount, Is.EqualTo(1)); Assert.That(second.GetResultCount, Is.EqualTo(1));
            }
        }
        [Test] public void ShutdownInterruptsAlreadyRunningClose_UsingNewTotalDeadline()
        {
            using (var f = new SessionFixture(new UIContextSettings { CloseTimeout = TimeSpan.FromSeconds(20) }, null))
            {
                var session = f.UI.OpenAsync<ProbeView>().GetAwaiter().GetResult(); var raw = new SingleAwaitSource<bool>();
                ProbeView.Close = (r, ct) => raw.Task.AsUniTask(); var close = session.CloseAsync(); f.Scheduler.Advance(2);
                var stop = f.UI.ShutdownAsync(); f.Scheduler.Advance(5); Assert.That(stop.Status, Is.EqualTo(UniTaskStatus.Succeeded));
                Assert.That(close.GetAwaiter().GetResult().Reason, Is.EqualTo(UICloseReason.ContextShutdown)); Assert.That(close.GetAwaiter().GetResult().HasPendingCleanup, Is.True);
                raw.Succeed(true); Assert.That(raw.GetResultCount, Is.EqualTo(1));
            }
        }
    }
}
