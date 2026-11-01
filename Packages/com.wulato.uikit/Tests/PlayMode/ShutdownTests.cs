using System;
using System.Collections;
using System.Threading;
using Cysharp.Threading.Tasks;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using Wulato.UIKit.Tests.Support;
namespace Wulato.UIKit.Tests.PlayMode
{
    [Category("cleanup")]
    public class ShutdownTests : SessionTestBase
    {
        [UnityTest] public IEnumerator ShutdownReleasesInternalRuntime_WhenRootRemainsAlive() => UniTask.ToCoroutine(async () =>
        {
            await using (var f = new CleanupRuntimeFixture())
            {
                await f.UI.OpenAsync<ProbeView>(); var runtime = f.Runtime;
                await f.UI.ShutdownAsync(); await UniTask.NextFrame(); await UniTask.NextFrame();
                Assert.That(f.RootObject != null, Is.True, "The project-owned Canvas must remain alive");
                Assert.That(runtime == null, Is.True, "Context-owned runtime roots must not wait for Canvas destruction");
            }
        });
        [UnityTest] public IEnumerator RootDisableCallback_CannotStartAnotherOpen() => UniTask.ToCoroutine(async () =>
        {
            await using (var f = new CleanupRuntimeFixture())
            {
                await f.UI.OpenAsync<ProbeView>(); UniTask<IUISession<ProbeView>> nested = default;
                ProbeView.Disabled = () => nested = f.UI.OpenAsync<ProbeView>();
                UnityEngine.Object.DestroyImmediate(f.RootObject);
                Assert.That(nested.Status, Is.EqualTo(UniTaskStatus.Faulted));
                Assert.That(Assert.Throws<UIException>(() => nested.GetAwaiter().GetResult()).Code, Is.EqualTo(UIErrorCode.ContextStopped));
                ProbeView.Disabled = null;
            }
        });
        [UnityTest] public IEnumerator TemporaryRootDisable_DoesNotReportRootDestroyed_AndCanRestore() => UniTask.ToCoroutine(async () =>
        {
            await using (var f = new CleanupRuntimeFixture())
            {
                var session = await f.UI.OpenAsync<ProbeView>();
                f.RootObject.SetActive(false);
                Assert.That(session.State, Is.EqualTo(UIState.Open)); Assert.That(f.Loader.ReleaseCount, Is.Zero);
                Assert.That(f.UI.GetDiagnostics().State, Is.EqualTo(UIContextState.Running));
                f.RootObject.SetActive(true); await UniTask.NextFrame();
                Assert.That(f.Loader.LastView.gameObject.activeInHierarchy, Is.True);
                Assert.That((await session.CloseAsync()).Reason, Is.EqualTo(UICloseReason.Owner));
            }
        });
        [UnityTest] public IEnumerator RealtimeScheduler_WaitsAndCancelsWhileTimeScaleZero() => UniTask.ToCoroutine(async () =>
        {
            float old = Time.timeScale; Time.timeScale = 0;
            try
            {
                var scheduler = new UniTaskUIAsyncScheduler(); var started = scheduler.UnscaledTime;
                using (var guard = new CancellationTokenSource(TimeSpan.FromSeconds(2)))
                    await scheduler.DelayAsync(TimeSpan.FromMilliseconds(1), true, guard.Token);
                ProbeView.Measurements.Add("realtime:timescale=" + Time.timeScale + ":elapsed=" + (scheduler.UnscaledTime - started).ToString("R"));
                using (var ct = new CancellationTokenSource())
                {
                    var pending = scheduler.DelayAsync(TimeSpan.FromHours(1), true, ct.Token); ct.Cancel();
                    bool canceled = false; try { await pending; } catch (OperationCanceledException) { canceled = true; }
                    Assert.That(canceled, Is.True);
                }
            }
            finally { Time.timeScale = old; }
        });
        [UnityTest] public IEnumerator NeverEndingLoader_ShutdownDoesNotWait() => UniTask.ToCoroutine(async () =>
        {
            await using (var f = new CleanupRuntimeFixture())
            {
                var raw = new SingleAwaitSource<IUIViewLease>(); Transform parent = null;
                f.Loader.Create = p => { parent = p; return raw.Task; }; var open = f.UI.OpenAsync<ProbeView>();
                var first = f.UI.ShutdownAsync(); var second = f.UI.ShutdownAsync();
                Assert.That(first.Status, Is.EqualTo(UniTaskStatus.Succeeded)); var result = await first;
                Assert.That(result, Is.SameAs(await second)); Assert.That(result.PendingCleanupCount, Is.EqualTo(1));
                Assert.That(raw.GetResultCount, Is.Zero); try { await open; } catch (OperationCanceledException) { }
                raw.Succeed(f.Loader.NewLease(parent)); Assert.That(raw.GetResultCount, Is.EqualTo(1)); Assert.That(f.Loader.ReleaseCount, Is.EqualTo(1));
            }
        });
        [UnityTest] public IEnumerator NotificationPump_SurvivesRootDestruction() => UniTask.ToCoroutine(async () =>
        {
            await using (var f = new CleanupRuntimeFixture())
            {
                var session = await f.UI.OpenAsync<ProbeView>(); var raw = new SingleAwaitSource<bool>();
                ProbeView.Close = (r, ct) => raw.Task.AsUniTask(); var closed = session.Closed;
                UnityEngine.Object.DestroyImmediate(f.RootObject); await UniTask.NextFrame(); f.Clock.Advance(2);
                Assert.That(closed.Status, Is.EqualTo(UniTaskStatus.Succeeded)); var result = await closed;
                Assert.That(result.Reason, Is.EqualTo(UICloseReason.RootDestroyed)); Assert.That(result.HasPendingCleanup, Is.True);
                ProbeView.Measurements.Add("closed:reason=" + result.Reason + ":pending=" + result.HasPendingCleanup + ":clock=" + f.Clock.UnscaledTime);
                Assert.That((await f.UI.ShutdownAsync()).Completions[0], Is.SameAs(result));
                raw.Succeed(true); Assert.That(f.Loader.ReleaseCount, Is.EqualTo(1));
            }
        });
        [UnityTest] public IEnumerator ErrorSinkThrows_CleanupCompletes() => UniTask.ToCoroutine(async () =>
        {
            await using (var f = new CleanupRuntimeFixture())
            {
                var session = await f.UI.OpenAsync<ProbeView>(); f.SinkThrows = true;
                ProbeView.Close = (r, ct) => UniTask.FromException(new Exception("close-fault"));
                var closed = await session.CloseAsync(); Assert.That(closed.Errors.Count, Is.GreaterThan(0));
                Assert.That(f.Loader.ReleaseCount, Is.EqualTo(1)); Assert.That((await f.UI.ShutdownAsync()).PendingCleanupCount, Is.Zero);
            }
        });
        [UnityTest] public IEnumerator TimeScaleZero_DoesNotStopShutdownBudget() => UniTask.ToCoroutine(async () =>
        {
            float old = Time.timeScale; Time.timeScale = 0;
            try
            {
                await using (var f = new CleanupRuntimeFixture())
                {
                    var session = await f.UI.OpenAsync<ProbeView>(); var raw = new SingleAwaitSource<bool>();
                    ProbeView.Close = (r, ct) => raw.Task.AsUniTask(); var stop = f.UI.ShutdownAsync();
                    await UniTask.NextFrame(); f.Clock.Advance(5); Assert.That(stop.Status, Is.EqualTo(UniTaskStatus.Succeeded)); Assert.That((await stop).PendingCleanupCount, Is.EqualTo(1));
                    raw.Succeed(true); Assert.That(raw.GetResultCount, Is.EqualTo(1));
                }
            }
            finally { Time.timeScale = old; }
        });
    }
}
