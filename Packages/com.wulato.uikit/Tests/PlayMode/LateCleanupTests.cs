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
    public class LateCleanupTests : SessionTestBase
    {
        [UnityTest] public IEnumerator ActualHostCacheReuse_CreatesNewScopeWithoutReload() => UniTask.ToCoroutine(async () =>
        {
            await using (var f = new CleanupRuntimeFixture(true))
            {
                var first = await f.UI.OpenAsync<ProbeView>(); var view = f.Loader.LastView; var oldScope = ProbeView.Scope;
                var closed = await first.CloseAsync(); Assert.That(closed.Errors, Is.Empty);
                Assert.That(f.Loader.ReleaseCount, Is.Zero); Assert.That(view.gameObject.activeInHierarchy, Is.False);
                Assert.That(f.UI.GetDiagnostics().CacheLru.Count, Is.EqualTo(1));
                var second = await f.UI.OpenAsync<ProbeView>();
                Assert.That(f.Loader.LastView, Is.SameAs(view)); Assert.That(view.gameObject.activeInHierarchy, Is.True);
                Assert.That(second.Id, Is.Not.EqualTo(first.Id)); Assert.That(oldScope.IsCurrent, Is.False);
                Assert.That(f.Loader.CreateCount, Is.EqualTo(1)); Assert.That(await first.CloseAsync(), Is.SameAs(closed));
                Assert.That(second.State, Is.EqualTo(UIState.Open));
                ProbeView.Measurements.Add("native-cache:old=" + first.Id + ":new=" + second.Id + ":creates=" + f.Loader.CreateCount + ":releases=" + f.Loader.ReleaseCount + ":old-current=" + oldScope.IsCurrent);
                await second.CloseAsync(); await f.UI.ClearCacheAsync(); Assert.That(f.Loader.ReleaseCount, Is.EqualTo(1));
            }
        });
        [UnityTest] public IEnumerator LateErrorSink_CannotReenterContext_AndCleanupCompletes() => UniTask.ToCoroutine(async () =>
        {
            await using (var f = new CleanupRuntimeFixture())
            {
                var raw = new SingleAwaitSource<bool>(); ProbeView.Open = (scope, ct) => raw.Task.AsUniTask();
                var open = f.UI.OpenAsync<ProbeView>(); UnityEngine.Object.DestroyImmediate(f.RootObject);
                try { await open; } catch (OperationCanceledException) { }
                await f.UI.ShutdownAsync(); UniTask nested = default;
                f.Reporting = error => nested = f.UI.ClearCacheAsync(); f.SinkThrows = true;
                raw.Fail(new Exception("late"));
                Assert.That(nested.Status, Is.EqualTo(UniTaskStatus.Faulted));
                Assert.That(Assert.Throws<UIException>(() => nested.GetAwaiter().GetResult()).Code, Is.EqualTo(UIErrorCode.InvalidRequest));
                Assert.That(f.Loader.ReleaseCount, Is.EqualTo(1)); Assert.That(raw.GetResultCount, Is.EqualTo(1));
            }
        });
        [UnityTest] public IEnumerator CancelAndLeaseHandoffSameFrame_ReleasesOnce() => UniTask.ToCoroutine(async () =>
        {
            await using (var f = new CleanupRuntimeFixture()) using (var ct = new CancellationTokenSource())
            {
                var raw = new SingleAwaitSource<IUIViewLease>(); Transform parent = null;
                f.Loader.Create = p => { parent = p; return raw.Task; }; var open = f.UI.OpenAsync<ProbeView>(ct.Token);
                var lease = f.Loader.NewLease(parent); ct.Cancel(); raw.Succeed(lease);
                bool canceled = false; try { await open; } catch (OperationCanceledException) { canceled = true; }
                Assert.That(canceled, Is.True); Assert.That(f.Loader.ReleaseCount, Is.EqualTo(1)); Assert.That(raw.GetResultCount, Is.EqualTo(1));
                Assert.That(f.UI.GetDiagnostics().Sessions, Is.Empty);
            }
        });
        [UnityTest] public IEnumerator HookFaultsAfterRootDestroyed_IsObserved() => UniTask.ToCoroutine(async () =>
        {
            await using (var f = new CleanupRuntimeFixture())
            {
                var raw = new SingleAwaitSource<bool>(); ProbeView.Open = (s, ct) => raw.Task.AsUniTask();
                var open = f.UI.OpenAsync<ProbeView>(); var view = f.Loader.LastView; var runtime = f.Runtime;
                UnityEngine.Object.DestroyImmediate(f.RootObject);
                Assert.That(view != null, Is.True, "Root destruction must evacuate the loader-owned running View");
                bool canceled = false; try { await open; } catch (OperationCanceledException) { canceled = true; }
                Assert.That(canceled, Is.True); var stop = await f.UI.ShutdownAsync();
                Assert.That(stop.PendingCleanupCount, Is.EqualTo(1)); Assert.That(runtime != null, Is.True);
                Assert.That(f.Loader.ReleaseCount, Is.Zero);
                var finalSnapshot = f.UI.GetDiagnostics();
                ProbeView.Measurements.Add("root-destroyed:runtime-alive=" + (runtime != null) + ":pending=" + stop.PendingCleanupCount + ":releases=" + f.Loader.ReleaseCount);
                raw.Fail(new Exception("late-after-root"));
                Assert.That(raw.GetResultCount, Is.EqualTo(1)); Assert.That(f.Loader.ReleaseCount, Is.EqualTo(1));
                Assert.That(f.Errors.Exists(e => e.Stage == "late-open" && e.InnerException?.Message == "late-after-root"), Is.True);
                Assert.That(f.UI.GetDiagnostics(), Is.SameAs(finalSnapshot)); Assert.That(finalSnapshot.PendingCleanupCount, Is.EqualTo(1));
                ProbeView.Measurements.Add("late-open:consumed=" + raw.GetResultCount + ":releases=" + f.Loader.ReleaseCount + ":final-snapshot-pending=" + finalSnapshot.PendingCleanupCount);
            }
        });
        [UnityTest] public IEnumerator LateLoadAfterRootDestroyed_UsesRetainedInactiveParent() => UniTask.ToCoroutine(async () =>
        {
            await using (var f = new CleanupRuntimeFixture())
            {
                var raw = new SingleAwaitSource<IUIViewLease>(); Transform parent = null;
                f.Loader.Create = p => { parent = p; return raw.Task; }; var open = f.UI.OpenAsync<ProbeView>();
                UnityEngine.Object.DestroyImmediate(f.RootObject);
                try { await open; } catch (OperationCanceledException) { }
                Assert.That(parent != null, Is.True); Assert.That(parent.gameObject.activeInHierarchy, Is.False);
                var stop = await f.UI.ShutdownAsync(); Assert.That(stop.PendingCleanupCount, Is.EqualTo(1));
                raw.Succeed(f.Loader.NewLease(parent)); Assert.That(raw.GetResultCount, Is.EqualTo(1)); Assert.That(f.Loader.ReleaseCount, Is.EqualTo(1));
            }
        });
        [UnityTest] public IEnumerator ClosedWithTimedOutHook_ReportsPendingCleanup() => UniTask.ToCoroutine(async () =>
        {
            await using (var f = new CleanupRuntimeFixture())
            {
                var session = await f.UI.OpenAsync<ProbeView>(); var raw = new SingleAwaitSource<bool>();
                ProbeView.Close = (r, ct) => raw.Task.AsUniTask(); var close = session.CloseAsync(); await UniTask.NextFrame(); f.Clock.Advance(2);
                Assert.That(close.Status, Is.EqualTo(UniTaskStatus.Succeeded)); var result = await close;
                Assert.That(result.HasPendingCleanup, Is.True); Assert.That(result.Errors.ExistsTimeout(), Is.True);
                Assert.That(f.Loader.ReleaseCount, Is.Zero); Assert.That(f.UI.GetDiagnostics().Sessions, Is.Empty);
                raw.Succeed(true); Assert.That(raw.GetResultCount, Is.EqualTo(1)); Assert.That(f.Loader.ReleaseCount, Is.EqualTo(1));
            }
        });
        [UnityTest] public IEnumerator PersistentRuntime_ReleasesAfterLastLateOwner() => UniTask.ToCoroutine(async () =>
        {
            UIRuntimeHost runtime;
            await using (var f = new CleanupRuntimeFixture())
            {
                var raw = new SingleAwaitSource<IUIViewLease>(); Transform parent = null;
                f.Loader.Create = p => { parent = p; return raw.Task; }; var open = f.UI.OpenAsync<ProbeView>(); runtime = f.Runtime;
                var stop = f.UI.ShutdownAsync(); try { await open; } catch (OperationCanceledException) { }
                await stop; f.Hosts.Dispose(); Assert.That(runtime != null, Is.True);
                raw.Succeed(f.Loader.NewLease(parent));
            }
            await UniTask.NextFrame(); await UniTask.NextFrame();
            Assert.That(runtime == null, Is.True);
        });
    }
    internal static class CleanupAssertions
    {
        internal static bool ExistsTimeout(this System.Collections.Generic.IReadOnlyList<Exception> errors)
        { foreach (var error in errors) if (error is UIException ui && ui.Code == UIErrorCode.Timeout) return true; return false; }
    }
}
