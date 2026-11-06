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
    [Category("transitions")]
    public class TransitionTests : SessionTestBase
    {
        internal sealed class ControlledTransition : IUITransition
        {
            public UITransitionContext Context;
            public UniTaskCompletionSource Completion;
            public bool BlockEnter = true, BlockExit, IgnoreStop, ThrowReset;
            public int Stops, Enters, Exits, WritesCount;
            public UITransitionWrites Writes => UITransitionWrites.Scale | UITransitionWrites.Position;
            public UniTask EnterAsync(UITransitionContext c, CancellationToken ct) { Enters++; return Begin(c, BlockEnter); }
            public UniTask ExitAsync(UITransitionContext c, CancellationToken ct) { Exits++; return Begin(c, BlockExit); }
            UniTask Begin(UITransitionContext c, bool block)
            {
                Context = c;
                if (!block) return UniTask.CompletedTask;
                Completion = new UniTaskCompletionSource(); Tick(); return Completion.Task;
            }
            public void Tick() { if (Completion == null || Completion.Task.Status.IsCompleted()) return; WritesCount++; Context.AnimationRoot.localScale = Vector3.one * 7; Context.AnimationRoot.anchoredPosition = Vector2.one * 99; }
            public void Stop(UITransitionContext c) { Stops++; if (!IgnoreStop) Completion?.TrySetCanceled(); }
            public void Reset(UITransitionContext c) { if (ThrowReset) throw new Exception("reset"); }
        }
        [UnityTest] public IEnumerator TransitionCancel_DoesNotWaitTwoSecondLimit() => UniTask.ToCoroutine(async () =>
        {
            var t = new ControlledTransition();
            await using (var f = new CleanupRuntimeFixture(transition: t)) using (var ct = new CancellationTokenSource())
            {
                var open = f.UI.OpenAsync<ProbeView>(ct.Token); await UniTask.NextFrame(); await UniTask.NextFrame();
                Assert.That(t.Enters, Is.EqualTo(1)); ct.Cancel();
                try { await open; Assert.Fail("must cancel"); } catch (OperationCanceledException) { }
                Assert.That(t.Stops, Is.GreaterThan(0)); Assert.That(t.Completion.Task.Status, Is.EqualTo(UniTaskStatus.Canceled));
                Assert.That(f.Loader.ReleaseCount, Is.EqualTo(1)); Assert.That(f.UI.GetDiagnostics().PendingCleanupCount, Is.Zero);
            }
        });


        [UnityTest] public IEnumerator OnOpenLayoutChange_IsBaseline() => UniTask.ToCoroutine(async () =>
        {
            var t = new ControlledTransition { BlockEnter = false };
            await using (var f = new CleanupRuntimeFixture(true, t))
            {
                ProbeView.Open = (scope, ct) =>
                {
                    var root = (RectTransform)f.Loader.LastView.transform.parent;
                    root.localScale = new Vector3(2, 3, 4); root.anchoredPosition = new Vector2(11, 12);
                    ((RectTransform)f.Loader.LastView.transform).sizeDelta = new Vector2(123, 456);
                    return UniTask.CompletedTask;
                };
                var first = await f.UI.OpenAsync<ProbeView>();
                var root = t.Context.AnimationRoot; t.BlockExit = true;
                ProbeView.Close = (r, ct) => { root.localScale = new Vector3(4, 5, 6); return UniTask.CompletedTask; };
                var close = first.CloseAsync(); await UniTask.NextFrame();
                t.Completion.TrySetResult(); await close;
                Assert.That(root.localScale, Is.EqualTo(new Vector3(4, 5, 6)));
                Assert.That(root.anchoredPosition, Is.EqualTo(new Vector2(11, 12)));
                Assert.That(((RectTransform)f.Loader.LastView.transform).sizeDelta, Is.EqualTo(new Vector2(123, 456)));
                ProbeView.Close = null; t.BlockExit = false;
                var second = await f.UI.OpenAsync<ProbeView>();
                Assert.That(root.localScale, Is.EqualTo(new Vector3(2, 3, 4))); await second.CloseAsync();
            }
        });
        [UnityTest] public IEnumerator UnstoppableTransition_IsHiddenAndIsolated() => UniTask.ToCoroutine(async () =>
        {
            var t = new ControlledTransition { IgnoreStop = true };
            await using (var f = new CleanupRuntimeFixture(true, t)) using (var ct = new CancellationTokenSource())
            {
                var open = f.UI.OpenAsync<ProbeView>(ct.Token); await UniTask.NextFrame(); await UniTask.NextFrame(); ct.Cancel();
                try { await open; Assert.Fail(); } catch (OperationCanceledException) { }
                Assert.That(f.UI.GetDiagnostics().PendingCleanupCount, Is.EqualTo(1));
                Assert.That(t.Context.AnimationRoot.gameObject.activeInHierarchy, Is.False);
                Assert.That(f.Errors.Exists(e => e.Stage == "stop-protocol"), Is.True);
                t.Completion.TrySetResult(); Assert.That(f.Loader.ReleaseCount, Is.EqualTo(1));
                Assert.That(f.UI.GetDiagnostics().CacheLru, Is.Empty);
            }
        });
        [UnityTest] public IEnumerator ExitTimeout_StopsAndReleasesWithoutPendingCleanup() => UniTask.ToCoroutine(async () =>
        {
            var t = new ControlledTransition { BlockEnter = false, BlockExit = true };
            await using (var f = new CleanupRuntimeFixture(true, t))
            {
                var session = await f.UI.OpenAsync<ProbeView>(); var close = session.CloseAsync();
                await UniTask.NextFrame(); f.Clock.Advance(2); var result = await close;
                Assert.That(result.Errors.ExistsTimeout(), Is.True); Assert.That(result.HasPendingCleanup, Is.False);
                Assert.That(f.Loader.ReleaseCount, Is.EqualTo(1));
            }
        });
        [UnityTest] public IEnumerator ImmediateTransition_HasNoFrameDelay() => UniTask.ToCoroutine(async () =>
        {
            var transition = new ImmediateUITransition();
            await using (var f = new CleanupRuntimeFixture(transition: transition))
            {
                var session = await f.UI.OpenAsync<ProbeView>(); int frame = Time.frameCount;
                await session.CloseAsync(); Assert.That(Time.frameCount, Is.EqualTo(frame));
            }
        });

        static async UniTask FailureAtProgress(bool entering, bool cancel, int frames)
        {
            var t = new ControlledTransition { BlockEnter = entering, BlockExit = !entering };
            await using (var f = new CleanupRuntimeFixture(true, t)) using (var ct = new CancellationTokenSource())
            {
                UniTask<IUISession<ProbeView>> open = default; UniTask<UICompletion> close = default;
                if (entering) open = f.UI.OpenAsync<ProbeView>(ct.Token);
                else { var session = await f.UI.OpenAsync<ProbeView>(); close = session.CloseAsync(); }
                await UniTask.NextFrame(); await UniTask.NextFrame();
                for (int n = 0; n < frames; n++) { t.Tick(); await UniTask.NextFrame(); }
                var root = t.Context.AnimationRoot;
                if (cancel) { if (entering) ct.Cancel(); else f.Clock.Advance(2); }
                else t.Completion.TrySetException(new Exception("animation-failed"));
                if (entering)
                {
                    try { await open; Assert.Fail(); } catch (OperationCanceledException) { Assert.That(cancel, Is.True); }
                    catch (UIException e) { Assert.That(e.Code, Is.EqualTo(UIErrorCode.TransitionFailed)); }
                }
                else { var result = await close; Assert.That(result.Errors, Is.Not.Empty); }
                Assert.That(f.Loader.ReleaseCount, Is.EqualTo(1)); Assert.That(f.UI.GetDiagnostics().CacheLru, Is.Empty);
                int writes = t.WritesCount; t.Tick(); await UniTask.NextFrame(); Assert.That(t.WritesCount, Is.EqualTo(writes));
            }
        }
        [UnityTest] public IEnumerator EnterCancelAtStart() => UniTask.ToCoroutine(() => FailureAtProgress(true, true, 0));
        [UnityTest] public IEnumerator EnterCancelAtMiddle() => UniTask.ToCoroutine(() => FailureAtProgress(true, true, 2));
        [UnityTest] public IEnumerator EnterCancelAtEnd() => UniTask.ToCoroutine(() => FailureAtProgress(true, true, 4));
        [UnityTest] public IEnumerator EnterFaultAtStart() => UniTask.ToCoroutine(() => FailureAtProgress(true, false, 0));
        [UnityTest] public IEnumerator EnterFaultAtMiddle() => UniTask.ToCoroutine(() => FailureAtProgress(true, false, 2));
        [UnityTest] public IEnumerator EnterFaultAtEnd() => UniTask.ToCoroutine(() => FailureAtProgress(true, false, 4));
        [UnityTest] public IEnumerator ExitCancelAtStart() => UniTask.ToCoroutine(() => FailureAtProgress(false, true, 0));
        [UnityTest] public IEnumerator ExitCancelAtMiddle() => UniTask.ToCoroutine(() => FailureAtProgress(false, true, 2));
        [UnityTest] public IEnumerator ExitCancelAtEnd() => UniTask.ToCoroutine(() => FailureAtProgress(false, true, 4));
        [UnityTest] public IEnumerator ExitFaultAtStart() => UniTask.ToCoroutine(() => FailureAtProgress(false, false, 0));
        [UnityTest] public IEnumerator ExitFaultAtMiddle() => UniTask.ToCoroutine(() => FailureAtProgress(false, false, 2));
        [UnityTest] public IEnumerator ExitFaultAtEnd() => UniTask.ToCoroutine(() => FailureAtProgress(false, false, 4));

    }
}
