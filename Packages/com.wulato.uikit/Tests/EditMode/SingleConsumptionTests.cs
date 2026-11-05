using System;
using System.Linq;
using System.Threading;
using Cysharp.Threading.Tasks;
using NUnit.Framework;
using Wulato.UIKit.Tests.Support;
namespace Wulato.UIKit.Tests
{
    [Category("sessions")] public class SingleConsumptionTests : SessionTestBase
    {
        [Test] public void LateOpenFault_ReportsOnlyAfterMainThreadHandoff() => LateFault(false);
        [Test] public void LateLoadFault_ReportsOnlyAfterMainThreadHandoff() => LateFault(true);
        void LateFault(bool load)
        {
            using(var f=new SessionFixture())using(var ct=new CancellationTokenSource())
            {
                var hook=new SingleAwaitSource<bool>();var loader=new SingleAwaitSource<IUIViewLease>();
                if(load)f.Loader.Create=p=>loader.Task;else ProbeView.Open=(s,t)=>hook.Task.AsUniTask();
                var open=f.UI.OpenAsync<ProbeView>(ct.Token);ct.Cancel();Assert.Throws<OperationCanceledException>(()=>open.GetAwaiter().GetResult());
                var worker=new Thread(()=> { if(load)loader.Fail(new Exception("late"));else hook.Fail(new Exception("late")); });worker.Start();worker.Join();
                int before=f.ReportThreads.Count;f.Scheduler.Advance();Assert.That(before,Is.Zero);Assert.That(f.ReportThreads,Is.All.EqualTo(Thread.CurrentThread.ManagedThreadId));Assert.That(f.UI.GetDiagnostics().PendingCleanupCount,Is.Zero);
            }
        }
        [Test] public void RawLifecycleSources_OpenCloseAndTransitionAreEachConsumedOnce()
        {
            using(var f=new SessionFixture()) { var open=new SingleAwaitSource<bool>();var enter=new SingleAwaitSource<bool>();ProbeView.Open=(s,t)=>open.Task.AsUniTask();f.Enter=ct=>enter.Task.AsUniTask();var pending=f.UI.OpenAsync<ProbeView>();open.Succeed(true);enter.Succeed(true);var session=pending.GetAwaiter().GetResult();var close=new SingleAwaitSource<bool>();var exit=new SingleAwaitSource<bool>();ProbeView.Close=(r,t)=>close.Task.AsUniTask();f.Exit=ct=>exit.Task.AsUniTask();var closed=session.CloseAsync();close.Succeed(true);exit.Succeed(true);closed.GetAwaiter().GetResult();Assert.That(new[]{open.GetResultCount,enter.GetResultCount,close.GetResultCount,exit.GetResultCount},Is.EqualTo(new[]{1,1,1,1})); }
        }
        [Test] public void RawSource_IsConsumedOnceWithMultipleRecordWaiters()
        {
            var raw=new SingleAwaitSource<int>();var record=new UIOperationRecord<int>(raw.Task);
            var a=record.Task;var b=record.Task;raw.Succeed(17);
            Assert.That(a.GetAwaiter().GetResult(),Is.EqualTo(17));Assert.That(b.GetAwaiter().GetResult(),Is.EqualTo(17));Assert.That(raw.GetResultCount,Is.EqualTo(1));
        }
        [Test] public void CancellationAndDeliverySameTurn_OnlyReleasesOnce()
        {
            using(var f=new SessionFixture())using(var ct=new CancellationTokenSource())
            {
                var raw=new SingleAwaitSource<IUIViewLease>();UnityEngine.Transform parent=null;
                f.Loader.Create=p=> {parent=p;return raw.Task;};var pending=f.UI.OpenAsync<ProbeView>(ct.Token);
                var lease=f.Loader.NewLease(parent);ct.Cancel();raw.Succeed(lease);
                Assert.Throws<OperationCanceledException>(()=>pending.GetAwaiter().GetResult());Assert.That(raw.GetResultCount,Is.EqualTo(1));Assert.That(f.Loader.ReleaseCount,Is.EqualTo(1));Assert.That(f.UI.GetDiagnostics().Sessions,Is.Empty);
            }
        }
        [Test] public void DeliveryThenCancellationBeforeCommit_DoesNotPublishSession()
        {
            using(var f=new SessionFixture())using(var ct=new CancellationTokenSource())
            {
                var raw=new SingleAwaitSource<IUIViewLease>();UnityEngine.Transform parent=null;
                f.Loader.Create=p=> {parent=p;return raw.Task;};ProbeView.Create=()=>ct.Cancel();var pending=f.UI.OpenAsync<ProbeView>(ct.Token);raw.Succeed(f.Loader.NewLease(parent));
                Assert.Throws<OperationCanceledException>(()=>pending.GetAwaiter().GetResult());Assert.That(raw.GetResultCount,Is.EqualTo(1));Assert.That(f.Loader.ReleaseCount,Is.EqualTo(1));
            }
        }
    }
}
