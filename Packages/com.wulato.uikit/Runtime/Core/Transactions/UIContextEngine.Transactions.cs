using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Cysharp.Threading.Tasks;
namespace Wulato.UIKit
{
    internal sealed partial class UIContextEngine
    {
        void CommitPrepared(UISessionHandle session, CancellationToken token)
        {
            var grant = Callback(() => dependencies.InputArbiter.Prepare(new UIInputCandidate(id, session.Id, session.View, session.Registration.InputPolicy)));
            session.OwnershipGeneration = grant.OwnershipGeneration;
            if (grant.IsEligible) EnterForeground(session);
            token.ThrowIfCancellationRequested();
            Callback(() => session.Host.SetVisible(true));
            session.State = UIState.Open;
            Callback(() => dependencies.InputArbiter.Commit(grant));
            Callback(() => session.Host.SetInteractive(grant.IsEligible));
            if (grant.IsEligible) inputOwner = new UIInputOwner(id, session.Id, grant.OwnershipGeneration);
        }
        public UniTask ExecuteAsync(Func<IUITransactionSteps, CancellationToken, UniTask> transaction, CancellationToken cancellationToken = default)
        {
            if (!dependencies.EnableTransactions || transaction == null)
                return UniTask.FromException(Error(UIErrorCode.InvalidRequest, "transaction-registration"));
            return Submit(async () =>
            {
                using (var linked = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, stopping.Token))
                {
                    linked.Token.ThrowIfCancellationRequested();
                    var steps = new TransactionSteps(this, linked.Token);
                    IDisposable inputLock = null;
                    UIOperationRecord<bool> record = null;
                    try
                    {
                        inputLock = Callback(() => dependencies.InputArbiter.AcquireLock(id)); inputLocks++;
                        steps.Suspend();
                        record = new UIOperationRecord<bool>(ToValue(transaction(steps, linked.Token)));
                        await record.WaitAsync(linked.Token);
                        await dependencies.Scheduler.SwitchToMainThreadAsync(CancellationToken.None);
                        linked.Token.ThrowIfCancellationRequested();
                        if (steps.InFlight) throw Error(UIErrorCode.InvalidRequest, "unfinished-transaction-step");
                    }
                    finally
                    {
                        await dependencies.Scheduler.SwitchToMainThreadAsync(CancellationToken.None);
                        steps.Active = false;
                        if (steps.InFlight)
                        {
                            try { linked.Cancel(); } catch (Exception e) { Report(Error(UIErrorCode.LifecycleFailed, "transaction-cancel", null, e)); }
                            await steps.WaitForStepAsync();
                        }
                        // An abandoned extension callback remains observed, with no scheduling rights.
                        if (record != null && !record.Task.Status.IsCompleted())
                            orphans.Adopt(record, default, "late-transaction", UIErrorCode.TransitionFailed, false, null);
                        steps.Cleanup();
                        if (inputLock != null) { try { Callback(inputLock.Dispose); } finally { inputLocks--; } }
                    }
                    return true;
                }
            }).AsUniTask();
        }
        sealed class TransactionSteps : IUITransactionSteps
        {
            readonly UIContextEngine engine;
            readonly CancellationToken token;
            readonly UISessionHandle[] original;
            readonly UIInputOwner? oldOwner;
            readonly List<UISessionHandle> prepared = new List<UISessionHandle>();
            public bool Active = true;
            public bool InFlight;
            UniTaskCompletionSource stepSettled;
            void BeginStep() { InFlight = true; stepSettled = new UniTaskCompletionSource(); }
            void EndStep() { InFlight = false; stepSettled.TrySetResult(); }
            internal UniTask WaitForStepAsync() => InFlight ? stepSettled.Task : UniTask.CompletedTask;
            bool committed;
            internal TransactionSteps(UIContextEngine engine, CancellationToken token)
            {
                this.engine = engine; this.token = token;
                original = engine.sessions.Where(s => s.State == UIState.Open).ToArray(); oldOwner = engine.inputOwner;
            }
            void Guard()
            {
                var error = engine.SubmissionError();
                if (error != null) throw error;
                if (!Active || InFlight) throw engine.Error(UIErrorCode.InvalidRequest, "transaction-step");
                token.ThrowIfCancellationRequested();
            }
            UISessionHandle Find(IUISession session)
            {
                if (session is IUISessionReference reference) session = reference.Session;
                var handle = engine.sessions.FirstOrDefault(s => ReferenceEquals(s, session));
                if (handle == null) throw engine.Error(UIErrorCode.InvalidRequest, "transaction-session");
                return handle;
            }
            internal void Suspend()
            {
                foreach (var session in original)
                {
                    engine.Callback(() => session.Host.SetInteractive(false));
                    engine.ExitForeground(session);
                    engine.Callback(() => engine.dependencies.InputArbiter.Revoke(engine.id, session.Id));
                }
                engine.inputOwner = null;
            }
            public async UniTask<IUISession> PrepareAsync(UIViewTypeKey type, UIArguments arguments, UITransitionOwner owner, CancellationToken cancellationToken = default)
            {
                Guard(); ValidateOwner(owner); BeginStep();
                try
                {
                    using (var linked = CancellationTokenSource.CreateLinkedTokenSource(token, cancellationToken))
                    {
                        var session = (UISessionHandle)await engine.Open(engine.dependencies.Registry.Get(type), arguments, linked.Token, true, owner);
                        prepared.Add(session); return session;
                    }
                }
                finally { EndStep(); }
            }
            static void ValidateOwner(UITransitionOwner owner)
            { if (owner != UITransitionOwner.CoreDefault && owner != UITransitionOwner.External) throw new ArgumentOutOfRangeException(nameof(owner)); }
            public void Commit(IUISession session)
            {
                Guard(); var handle = Find(session);
                if (!prepared.Contains(handle) || handle.State != UIState.Opening || committed)
                    throw engine.Error(UIErrorCode.InvalidRequest, "transaction-commit");
                try { engine.CommitPrepared(handle, token); committed = true; }
                catch { Rollback(handle); throw; }
            }
            void Rollback(UISessionHandle handle)
            {
                handle.Reason = token.IsCancellationRequested ? UICloseReason.OpenCanceled : UICloseReason.OpenFailed;
                engine.TerminateResources(handle); engine.Finish(handle, handle.OpenEntered);
                prepared.Remove(handle);
            }
            public UniTask RollbackAsync(IUISession session)
            {
                Guard(); var handle = Find(session);
                if (!prepared.Contains(handle) || handle.State != UIState.Opening)
                    throw engine.Error(UIErrorCode.InvalidRequest, "transaction-rollback");
                Rollback(handle); return UniTask.CompletedTask;
            }
            public void SetVisible(IUISession session, bool visible)
            { Guard(); var handle = Find(session); engine.Callback(() => handle.Host.SetVisible(visible)); }
            public async UniTask AnimateAsync(IUISession session, IUITransition transition, bool entering, CancellationToken cancellationToken = default)
            {
                Guard(); var handle = Find(session);
                if (transition == null || handle.TransitionOwner != UITransitionOwner.External)
                    throw engine.Error(UIErrorCode.InvalidRequest, "transaction-animation");
                BeginStep();
                try
                {
                    using (var linked = CancellationTokenSource.CreateLinkedTokenSource(token, cancellationToken))
                        await engine.RunTransition(handle, entering, linked.Token, transition);
                }
                finally { EndStep(); }
            }
            public async UniTask<UICompletion> CloseAsync(IUISession session, UITransitionOwner owner, UICloseReason reason = UICloseReason.Owner)
            {
                Guard(); ValidateOwner(owner); var handle = Find(session);
                if (!committed || handle.State != UIState.Open || reason == UICloseReason.ContextShutdown || reason == UICloseReason.RootDestroyed || !Enum.IsDefined(typeof(UICloseReason), reason))
                    throw engine.Error(UIErrorCode.InvalidRequest, "transaction-close");
                BeginStep();
                try
                { handle.TransitionOwner = owner; handle.CloseRequested = true; handle.Reason = reason; return await engine.CloseCore(handle); }
                finally { EndStep(); }
            }
            internal void Cleanup()
            {
                foreach (var session in prepared.ToArray())
                    if (session.State == UIState.Opening) Rollback(session);
                if (!committed && engine.state == UIContextState.Running)
                {
                    foreach (var session in original)
                        if (session.State == UIState.Open) engine.Collect(session, () => session.Host.SetVisible(true));
                    var old = original.FirstOrDefault(s => s.Id == oldOwner?.SessionId && s.State == UIState.Open);
                    if (old != null)
                    {
                        try { engine.CommitPrepared(old, CancellationToken.None); }
                        catch (Exception e) { old.Errors.Add(e); engine.Report(engine.Error(UIErrorCode.LifecycleFailed, "transaction-restore", old, e)); }
                    }
                }
            }
        }
    }
}
