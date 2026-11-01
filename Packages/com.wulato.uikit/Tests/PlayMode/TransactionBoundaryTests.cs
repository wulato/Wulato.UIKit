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
    public class TransactionBoundaryTests : SessionTestBase
    {
        static UIViewTypeKey Key => new UIViewTypeKey("probe");
        [UnityTest] public IEnumerator ExternalOwner_DoesNotPlayDefaultTransition() => UniTask.ToCoroutine(async () =>
        {
            var t = new TransitionTests.ControlledTransition { BlockEnter = false };
            await using (var f = new CleanupRuntimeFixture(transition: t, transactions: true))
            {
                IUISession session = null;
                await f.UI.GetService<IUITransactionAccess>().ExecuteAsync(async (steps, ct) =>
                {
                    session = await steps.PrepareAsync(Key, UIArguments.None, UITransitionOwner.External, ct);
                    Assert.That(session.State, Is.EqualTo(UIState.Opening));
                    steps.Commit(session); await steps.CloseAsync(session, UITransitionOwner.External);
                });
                Assert.That(session.State, Is.EqualTo(UIState.Closed));
                Assert.That(t.Enters + t.Exits + t.Stops, Is.Zero);
                Assert.That(ProbeView.Events.FindAll(e => e == "open").Count, Is.EqualTo(1));
                Assert.That(ProbeView.Events.FindAll(e => e == "close").Count, Is.EqualTo(1));
            }
        });
        [UnityTest] public IEnumerator Rollback_RestoresOldVisibilityAndInput() => UniTask.ToCoroutine(async () =>
        {
            await using (var f = new CleanupRuntimeFixture(transactions: true))
            {
                var old = await f.UI.OpenAsync<ProbeView>(); var oldView = f.Loader.LastView;
                var shell = oldView.transform.parent.parent.GetComponent<CanvasGroup>();
                await f.UI.GetService<IUITransactionAccess>().ExecuteAsync(async (steps, ct) =>
                {
                    steps.SetVisible(old, false);
                    Assert.That(shell.interactable, Is.False);
                    // Rollback without a replacement must still restore the original owner.
                    await UniTask.CompletedTask;
                });
                Assert.That(old.State, Is.EqualTo(UIState.Open)); Assert.That(shell.alpha, Is.EqualTo(1));
                Assert.That(shell.interactable, Is.True); Assert.That(f.UI.GetDiagnostics().InputOwner.Value.SessionId, Is.EqualTo(old.Id));
                await old.CloseAsync();
            }
        });
        [UnityTest] public IEnumerator Transaction_HoldsQueueUntilRollback() => UniTask.ToCoroutine(async () =>
        {
            await using (var f = new CleanupRuntimeFixture(transactions: true))
            {
                var gate = new UniTaskCompletionSource(); IUISession prepared = null;
                var transaction = f.UI.GetService<IUITransactionAccess>().ExecuteAsync(async (steps, ct) =>
                { prepared = await steps.PrepareAsync(Key, UIArguments.None, UITransitionOwner.External, ct); await gate.Task; await steps.RollbackAsync(prepared); });
                await UniTask.NextFrame(); await UniTask.NextFrame();
                var open = f.UI.OpenAsync<ProbeView>(); Assert.That(open.Status.IsCompleted(), Is.False);
                gate.TrySetResult(); await transaction;
                var session = await open; Assert.That(prepared.State, Is.EqualTo(UIState.Closed)); await session.CloseAsync();
            }
        });
        [UnityTest] public IEnumerator Shutdown_InterruptsTransactionAndInvalidatesLateSteps() => UniTask.ToCoroutine(async () =>
        {
            await using (var f = new CleanupRuntimeFixture(transactions: true))
            {
                var gate = new UniTaskCompletionSource(); IUITransactionSteps retained = null; IUISession prepared = null;
                var transaction = f.UI.GetService<IUITransactionAccess>().ExecuteAsync(async (steps, ct) =>
                { retained = steps; prepared = await steps.PrepareAsync(Key, UIArguments.None, UITransitionOwner.External, ct); await gate.Task; });
                await UniTask.NextFrame(); await UniTask.NextFrame();
                var shutdown = f.UI.ShutdownAsync();
                try { await transaction; Assert.Fail(); } catch (OperationCanceledException) { }
                await shutdown; Assert.That(prepared.State, Is.EqualTo(UIState.Closed));
                Assert.Throws<UIException>(() => retained.Commit(prepared)); gate.TrySetResult();
                Assert.That(f.Loader.ReleaseCount, Is.EqualTo(1));
            }
        });
        [UnityTest] public IEnumerator TransactionAccess_RequiresExplicitRegistration() => UniTask.ToCoroutine(async () =>
        {
            await using (var f = new CleanupRuntimeFixture())
            { Assert.Throws<InvalidOperationException>(() => f.UI.GetService<IUITransactionAccess>()); await UniTask.CompletedTask; }
        });
        [UnityTest] public IEnumerator Shutdown_DuringPrepareDoesNotRunConcurrentClose() => UniTask.ToCoroutine(async () =>
        {
            await using (var f = new CleanupRuntimeFixture(transactions: true))
            {
                var raw = new SingleAwaitSource<bool>(); ProbeView.Open = (scope, ct) => raw.Task.AsUniTask();
                var entered = new UniTaskCompletionSource();
                var txn = f.UI.GetService<IUITransactionAccess>().ExecuteAsync(async (steps, ct) =>
                { entered.TrySetResult(); await steps.PrepareAsync(Key, UIArguments.None, UITransitionOwner.External, ct); });
                await entered.Task; await UniTask.NextFrame(); var stop = f.UI.ShutdownAsync();
                try { await txn; Assert.Fail(); } catch (OperationCanceledException) { }
                var result = await stop;
                Assert.That(ProbeView.Events.FindAll(e => e == "close").Count, Is.Zero);
                Assert.That(f.UI.GetDiagnostics().Sessions, Is.Empty);
                raw.Succeed(true); Assert.That(f.Loader.ReleaseCount, Is.EqualTo(1));
            }
        });
        [UnityTest] public IEnumerator ExternalAnimationCancel_StopsAndRollsBack() => UniTask.ToCoroutine(async () =>
        {
            var t = new TransitionTests.ControlledTransition();
            await using (var f = new CleanupRuntimeFixture(transactions: true)) using (var ct = new CancellationTokenSource())
            {
                var txn = f.UI.GetService<IUITransactionAccess>().ExecuteAsync(async (steps, token) =>
                {
                    var page = await steps.PrepareAsync(Key, UIArguments.None, UITransitionOwner.External, token);
                    await steps.AnimateAsync(page, t, true, token); steps.Commit(page);
                }, ct.Token);
                await UniTask.NextFrame(); await UniTask.NextFrame(); ct.Cancel();
                try { await txn; Assert.Fail(); } catch (OperationCanceledException) { }
                Assert.That(t.Stops, Is.GreaterThan(0)); Assert.That(f.Loader.ReleaseCount, Is.EqualTo(1));
                Assert.That(f.UI.GetDiagnostics().Sessions, Is.Empty);
            }
        });
        [UnityTest] public IEnumerator CallerCancel_DuringPrepareReleasesStepBeforeNextCommand() => UniTask.ToCoroutine(async () =>
        {
            await using (var f = new CleanupRuntimeFixture(transactions: true)) using (var ct = new CancellationTokenSource())
            {
                var raw = new SingleAwaitSource<bool>(); ProbeView.Open = (scope, token) => raw.Task.AsUniTask();
                var txn = f.UI.GetService<IUITransactionAccess>().ExecuteAsync(async (steps, token) =>
                    { await steps.PrepareAsync(Key, UIArguments.None, UITransitionOwner.External, token); }, ct.Token);
                await UniTask.NextFrame(); var next = f.UI.OpenAsync<ProbeView>(); ProbeView.Open = null; ct.Cancel();
                try { await txn; Assert.Fail(); } catch (OperationCanceledException) { }
                var session = await next; Assert.That(session.State, Is.EqualTo(UIState.Open));
                raw.Succeed(true); await session.CloseAsync();
            }
        });
        [UnityTest] public IEnumerator InputCommitFault_RollsBackReplacementAndRestoresOld() => UniTask.ToCoroutine(async () =>
        {
            await using (var f = new CleanupRuntimeFixture(transactions: true, multi: true))
            {
                var old = await f.UI.OpenAsync<ProbeView>(); IUISession replacement = null;
                f.Input.Committing = grant => { f.Input.Committing = null; throw new Exception("input-commit"); };
                try
                {
                    await f.UI.GetService<IUITransactionAccess>().ExecuteAsync(async (steps, ct) =>
                    { replacement = await steps.PrepareAsync(Key, UIArguments.None, UITransitionOwner.External, ct); steps.Commit(replacement); });
                    Assert.Fail();
                }
                catch (UIException) { }
                Assert.That(replacement.State, Is.EqualTo(UIState.Closed));
                Assert.That(f.UI.GetDiagnostics().Sessions.Count, Is.EqualTo(1));
                Assert.That(f.UI.GetDiagnostics().InputOwner.Value.SessionId, Is.EqualTo(old.Id)); await old.CloseAsync();
            }
        });
    }
}
