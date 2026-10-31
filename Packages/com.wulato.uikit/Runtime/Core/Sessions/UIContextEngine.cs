using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;

namespace Wulato.UIKit
{
    /// <summary>Logical command engine. Unity host construction is a separate first-party backend.</summary>
    internal sealed class UIContextEngine : IUIContext
    {
        readonly UIContextId id;
        readonly UIRuntimeDependencies dependencies;
        readonly UICompletionQueue completions;
        readonly UICommandQueue commands;
        readonly List<UISessionHandle> sessions = new List<UISessionHandle>();
        readonly List<UICompletion> shutdownCompletions = new List<UICompletion>();
        readonly CancellationTokenSource stopping = new CancellationTokenSource();
        readonly UniTaskCompletionSource<UIShutdownCompletion> shutdown = new UniTaskCompletionSource<UIShutdownCompletion>();
        readonly IDisposable inputRegistration;
        readonly int mainThread = Thread.CurrentThread.ManagedThreadId;
        UIContextState state = UIContextState.Running;
        long sequence;
        int callbackDepth, pendingCleanup, isolatedLeases, inputLocks;
        UIInputOwner? inputOwner;

        internal UIContextEngine(UIContextId id, UIRuntimeDependencies dependencies)
        {
            this.id = id;
            this.dependencies = dependencies ?? throw new ArgumentNullException(nameof(dependencies));
            completions = new UICompletionQueue(e => Report(Error(UIErrorCode.LifecycleFailed, "completion", null, e)));
            commands = new UICommandQueue(completions, e => Report(Error(UIErrorCode.LifecycleFailed, "command", null, e)));
            inputRegistration = dependencies.InputArbiter.Register(id,
                new UIInputRegistration(0, ForegroundChanged, UserCloseRequested));
        }

        UIException Error(UIErrorCode code, string stage, UISessionHandle session = null, Exception cause = null)
            => new UIException(code, stage, id, session?.Id, cause);

        Exception SubmissionError(bool allowStopping = false)
        {
            if (Thread.CurrentThread.ManagedThreadId != mainThread)
                return Error(UIErrorCode.InvalidRequest, "main-thread");
            if (callbackDepth != 0) return Error(UIErrorCode.InvalidRequest, "nested-command");
            if (!allowStopping && state != UIContextState.Running) return Error(UIErrorCode.ContextStopped, "submit");
            return null;
        }

        T Callback<T>(Func<T> callback)
        {
            callbackDepth++;
            try { return callback(); }
            finally { callbackDepth--; }
        }
        void Callback(Action callback) => Callback(() => { callback(); return true; });
        void Report(UIException error)
        {
            try { Callback(() => dependencies.ErrorSink.Report(error)); }
            catch (Exception e) { Debug.LogWarning("UIKit error sink failed: " + e.Message); }
        }
        void Collect(UISessionHandle session, Action cleanup)
        {
            try { Callback(cleanup); }
            catch (Exception e) { session.Errors.Add(e); Report(Error(UIErrorCode.LifecycleFailed, "cleanup", session, e)); }
        }

        UniTask<T> Submit<T>(Func<UniTask<T>> execute)
        {
            var failure = SubmissionError();
            if (failure != null) return UniTask.FromException<T>(failure);
            var result = new UniTaskCompletionSource<T>();
            commands.Enqueue(async () =>
            {
                try
                {
                    if (state != UIContextState.Running) throw Error(UIErrorCode.ContextStopped, "execute");
                    var value = await execute();
                    completions.Post(() => result.TrySetResult(value));
                }
                catch (OperationCanceledException e) { completions.Post(() => result.TrySetCanceled(e.CancellationToken)); }
                catch (Exception e)
                {
                    var error = e as UIException ?? Error(UIErrorCode.LifecycleFailed, "execute", null, e);
                    Report(error);
                    completions.Post(() => result.TrySetException(error));
                }
            });
            return result.Task;
        }

        UIRegistration Registration(Type type)
        {
            foreach (var entry in dependencies.Registry.Entries) if (entry.ViewType == type) return entry;
            throw Error(UIErrorCode.InvalidRequest, "registration");
        }
        public UniTask<IUISession<TView>> OpenAsync<TView>(CancellationToken cancellationToken = default) where TView : UIView
            => Submit<IUISession>(async () => await Open(Registration(typeof(TView)), UIArguments.None, cancellationToken)).AsTyped<TView>();
        public UniTask<IUISession<TView>> OpenAsync<TView, TArgs>(TArgs args, CancellationToken cancellationToken = default) where TView : UIView<TArgs>
            => Submit<IUISession>(async () => await Open(Registration(typeof(TView)), UIArguments.From(args), cancellationToken)).AsTyped<TView>();
        public UniTask<IUISession> OpenAsync(UIViewTypeKey type, UIArguments args, CancellationToken cancellationToken = default)
            => Submit<IUISession>(async () =>
            {
                UIRegistration entry;
                try { entry = dependencies.Registry.Get(type); }
                catch (ArgumentException e) { throw Error(UIErrorCode.InvalidRequest, "registration", null, e); }
                return await Open(entry, args, cancellationToken);
            });

        async UniTask<IUISession> Open(UIRegistration entry, UIArguments args, CancellationToken caller)
        {
            caller.ThrowIfCancellationRequested();
            if (!entry.Accepts(args)) throw Error(UIErrorCode.InvalidRequest, "arguments");
            if (entry.Multiplicity == UIMultiplicity.Single && sessions.Any(s => s.Registration.Key == entry.Key))
                throw Error(UIErrorCode.AlreadyOpen, "single");
            var session = new UISessionHandle(this, new UISessionId(++sequence), entry);
            sessions.Add(session);
            string stage = "load";
            using (var opening = CancellationTokenSource.CreateLinkedTokenSource(caller, stopping.Token))
            {
                IDisposable inputLock = null;
                CancellationTokenRegistration sessionCancellation = default;
                try
                {
                    inputLock = Callback(() => dependencies.InputArbiter.AcquireLock(id)); inputLocks++;
                    session.Host = Callback(() => dependencies.HostFactory.Create(entry.Layer));
                    var load = new UIOperationRecord<IUIViewLease>(Callback(() => dependencies.Loader.CreateAsync(entry.Asset, session.Host.InactiveParent, opening.Token)));
                    IUIViewLease loaded;
                    try { loaded = await load.Task.AttachExternalCancellation(opening.Token); }
                    catch
                    {
                        // A cancellation can win before an uncooperative loader hands over ownership.
                        await dependencies.Scheduler.SwitchToMainThreadAsync(CancellationToken.None);
                        var lateHost = session.Host; session.Host = null;
                        ObserveLateLoad(load, lateHost, session).Forget();
                        throw;
                    }
                    await dependencies.Scheduler.SwitchToMainThreadAsync(CancellationToken.None);
                    session.Lease = loaded;
                    opening.Token.ThrowIfCancellationRequested();
                    session.View = loaded?.View;
                    if (session.View == null || session.View.GetType() != entry.ViewType)
                        throw Error(UIErrorCode.LoadFailed, "view-binding", session);
                    stage = "create";
                    session.Instance = new UIInstanceScope();
                    Callback(session.View.InvokeCreate);
                    opening.Token.ThrowIfCancellationRequested();
                    session.Scope = new DefaultUISessionScope(session.Id, id, dependencies.Scheduler,
                        ct => dependencies.LayoutReady.WaitLayoutAsync(session.View, ct));
                    sessionCancellation = session.Scope.Token.Register(() => opening.Cancel());
                    session.View.UserCloseRequest = () => UserCloseRequested(session.Id, UICloseReason.UserRequest);
                    Callback(() => session.Host.AttachHidden(session.View));
                    Callback(() => session.Host.SetVisible(false));
                    Callback(() => session.Host.SetInteractive(false));
                    stage = "preparing";
                    foreach (var observer in dependencies.Observers)
                    {
                        var binding = Callback(() => observer.OnPreparing(session.View, args, session.Scope));
                        if (binding == null) throw new InvalidOperationException("Observer must return a binding lease.");
                        session.Scope.Track(binding);
                        session.Observers.Add(observer);
                        opening.Token.ThrowIfCancellationRequested();
                    }
                    stage = "open";
                    session.OpenEntered = true;
                    await AwaitStage(session, () => session.View.InvokeOpenAsync(args, session.Scope, opening.Token), opening.Token);
                    stage = "layout";
                    await AwaitStage(session, () => dependencies.LayoutReady.WaitLayoutAsync(session.View, opening.Token), opening.Token);
                    stage = "enter";
                    Callback(() => session.Host.SetVisible(true));
                    await AwaitStage(session, () => dependencies.Transition.EnterAsync(TransitionContext(session), opening.Token), opening.Token);
                    stage = "foreground";
                    var grant = Callback(() => dependencies.InputArbiter.Prepare(new UIInputCandidate(id, session.Id, session.View, entry.InputPolicy)));
                    session.OwnershipGeneration = grant.OwnershipGeneration;
                    if (grant.IsEligible) EnterForeground(session);
                    opening.Token.ThrowIfCancellationRequested();
                    // No await or user completion between the state and input commit.
                    session.State = UIState.Open;
                    Callback(() => dependencies.InputArbiter.Commit(grant));
                    Callback(() => session.Host.SetInteractive(grant.IsEligible));
                    if (grant.IsEligible) inputOwner = new UIInputOwner(id, session.Id, grant.OwnershipGeneration);
                    return session;
                }
                catch (Exception e)
                {
                    await dependencies.Scheduler.SwitchToMainThreadAsync(CancellationToken.None);
                    session.Reason = state == UIContextState.Running
                        ? (e is OperationCanceledException ? UICloseReason.OpenCanceled : UICloseReason.OpenFailed)
                        : UICloseReason.ContextShutdown;
                    TerminateResources(session);
                    Finish(session, session.OpenEntered);
                    if (e is OperationCanceledException) throw;
                    var code = stage == "load" ? UIErrorCode.LoadFailed : stage == "enter" ? UIErrorCode.TransitionFailed : UIErrorCode.LifecycleFailed;
                    throw e as UIException ?? Error(code, stage, session, e);
                }
                finally
                {
                    sessionCancellation.Dispose();
                    if (inputLock != null) { Collect(session, inputLock.Dispose); inputLocks--; }
                }
            }
        }

        UITransitionContext TransitionContext(UISessionHandle session)
            => new UITransitionContext(session.Scope, session.Host.AnimationRoot, session.Host.AnimationOpacity);

        async UniTask AwaitStage(UISessionHandle session, Func<UniTask> invoke, CancellationToken ct)
        {
            var record = new UIOperationRecord<bool>(ToValue(Callback(invoke)));
            bool abandoned = false;
            try { await record.Task.AttachExternalCancellation(ct); }
            catch
            {
                abandoned = !record.Task.Status.IsCompleted();
                throw;
            }
            finally
            {
                await dependencies.Scheduler.SwitchToMainThreadAsync(CancellationToken.None);
                if (abandoned) session.AbandonedOperation = record;
            }
            ct.ThrowIfCancellationRequested();
        }
        static async UniTask<bool> ToValue(UniTask operation) { await operation; return true; }

        async UniTask ObserveLateLoad(UIOperationRecord<IUIViewLease> record, IUIViewHost host, UISessionHandle session)
        {
            pendingCleanup++;
            try
            {
                var lease = await record.Task;
                await dependencies.Scheduler.SwitchToMainThreadAsync(CancellationToken.None);
                try { Callback(lease.Dispose); }
                catch (Exception e) { Report(Error(UIErrorCode.LoadFailed, "late-load-release", session, e)); }
            }
            catch (OperationCanceledException) { }
            catch (Exception e)
            {
                await dependencies.Scheduler.SwitchToMainThreadAsync(CancellationToken.None);
                Report(Error(UIErrorCode.LoadFailed, "late-load", session, e));
            }
            finally
            {
                await dependencies.Scheduler.SwitchToMainThreadAsync(CancellationToken.None);
                try { Callback(host.Dispose); } catch (Exception e) { Report(Error(UIErrorCode.LoadFailed, "late-host-release", session, e)); }
                pendingCleanup--;
            }
        }

        void EnterForeground(UISessionHandle session)
        {
            if (session.Foreground != null) return;
            var scope = new DefaultUIForegroundScope();
            try { Callback(() => session.View.InvokeForegroundEntered(scope)); session.Foreground = scope; }
            catch { Collect(session, scope.Dispose); throw; }
        }
        void ExitForeground(UISessionHandle session)
        {
            if (session.Foreground == null) return;
            var scope = session.Foreground; session.Foreground = null;
            Collect(session, scope.Dispose);
            Collect(session, session.View.InvokeForegroundExited);
        }
        void TerminateResources(UISessionHandle session)
        {
            session.State = UIState.Closing;
            Collect(session, () => dependencies.InputArbiter.Revoke(id, session.Id));
            if (inputOwner?.SessionId == session.Id) inputOwner = null;
            ExitForeground(session);
            // Cancellation precedes observer termination; bindings release in reverse preparation order.
            if (session.Scope != null)
            {
                // Scope owns both cancellation and all binding resources. Notify termination on cancellation
                // before disposing the binding stack (see DefaultUISessionScope.End).
                Callback(() => session.Scope.End(() =>
                {
                    for (int i = session.Observers.Count - 1; i >= 0; i--)
                    {
                        var observer = session.Observers[i];
                        Collect(session, () => observer.OnTerminating(session.Scope, session.Reason));
                    }
                }, e => { session.Errors.Add(e); Report(Error(UIErrorCode.LifecycleFailed, "scope", session, e)); }));
            }
        }
        void Finish(UISessionHandle session, bool notifyClosed, Action beforeCompletion = null)
        {
            if (session.Host != null)
            {
                Collect(session, () => session.Host.SetInteractive(false));
                Collect(session, () => session.Host.SetVisible(false));
            }
            bool isolated = session.AbandonedOperation != null;
            if (isolated)
            {
                session.View.UserCloseRequest = null;
                Collect(session, session.Host.MoveToCache);
                ObserveLateOperation(session.AbandonedOperation, session.Lease, session.Host, session.Instance, session).Forget();
                session.Lease = null; session.Host = null; session.Instance = null;
            }
            if (session.Lease != null)
            {
                if (session.View != null)
                {
                    session.View.UserCloseRequest = null;
                    if (notifyClosed) Collect(session, () => session.View.InvokeClosed(session.Reason));
                    Collect(session, session.View.InvokeDispose);
                }
                if (session.Instance != null) Collect(session, session.Instance.Dispose);
                Collect(session, session.Lease.Dispose); session.Lease = null;
            }
            if (session.Host != null) { Collect(session, session.Host.Dispose); session.Host = null; }
            for (int i = session.Observers.Count - 1; i >= 0; i--)
            {
                var observer = session.Observers[i];
                Collect(session, () => observer.OnTerminated(new UICompletion(session.Id, session.Reason, session.Errors, isolated)));
            }
            sessions.Remove(session);
            session.State = UIState.Closed;
            beforeCompletion?.Invoke();
            var result = new UICompletion(session.Id, session.Reason, session.Errors, isolated);
            session.Result = result;
            if (state == UIContextState.Stopping) shutdownCompletions.Add(result);
            completions.Post(() => session.Completion.TrySetResult(result));
        }

        async UniTask ObserveLateOperation(UIOperationRecord<bool> record, IUIViewLease lease, IUIViewHost host, UIInstanceScope instance, UISessionHandle session)
        {
            pendingCleanup++; isolatedLeases++;
            try { await record.Task; }
            catch (OperationCanceledException) { }
            catch (Exception e)
            {
                await dependencies.Scheduler.SwitchToMainThreadAsync(CancellationToken.None);
                Report(Error(UIErrorCode.LifecycleFailed, "late-operation", session, e));
            }
            finally
            {
                await dependencies.Scheduler.SwitchToMainThreadAsync(CancellationToken.None);
                // User hooks were abandoned; never run them concurrently or replay them after termination.
                if (instance != null) Collect(session, instance.Dispose);
                Collect(session, lease.Dispose);
                Collect(session, host.Dispose);
                isolatedLeases--; pendingCleanup--;
            }
        }

        internal UniTask<UICompletion> Close(UISessionHandle session, UICloseReason reason)
        {
            var error = SubmissionError(true);
            if (error != null) return UniTask.FromException<UICompletion>(error);
            if (reason == UICloseReason.ContextShutdown || reason == UICloseReason.RootDestroyed || !Enum.IsDefined(typeof(UICloseReason), reason))
                return UniTask.FromException<UICompletion>(Error(UIErrorCode.InvalidRequest, "close-reason", session));
            if (session.CloseRequested || session.State == UIState.Closed) return session.Closed;
            session.CloseRequested = true;
            session.Reason = state == UIContextState.Running ? reason : UICloseReason.ContextShutdown;
            commands.Enqueue(async () => { if (session.State != UIState.Closed) await CloseCore(session); });
            return session.Closed;
        }
        async UniTask<UICompletion> CloseCore(UISessionHandle session)
        {
            IDisposable inputLock = null;
            Collect(session, () => { inputLock = dependencies.InputArbiter.AcquireLock(id); inputLocks++; });
            try
            {
                TerminateResources(session);
                try { await AwaitStage(session, () => session.View.InvokeCloseAsync(session.Reason, CancellationToken.None), CancellationToken.None); }
                catch (Exception e) { session.Errors.Add(e); Report(Error(UIErrorCode.LifecycleFailed, "close", session, e)); }
                try { await AwaitStage(session, () => dependencies.Transition.ExitAsync(TransitionContext(session), CancellationToken.None), CancellationToken.None); }
                catch (Exception e) { session.Errors.Add(e); Report(Error(UIErrorCode.TransitionFailed, "exit", session, e)); }
            }
            catch (Exception e) { session.Errors.Add(e); Report(Error(UIErrorCode.LifecycleFailed, "terminate", session, e)); }
            finally
            {
                Finish(session, true, () =>
                {
                    if (inputLock != null) { Collect(session, inputLock.Dispose); inputLocks--; }
                });
            }
            return session.Result;
        }
        public UniTask<UIBatchCompletion> CloseAllAsync(UICloseReason reason = UICloseReason.Owner)
            => Submit(() => CloseBatch(null, reason));
        public UniTask<UIBatchCompletion> CloseAllOfTypeAsync<TView>(UICloseReason reason = UICloseReason.Owner) where TView : UIViewBase
            => Submit(() => CloseBatch(Registration(typeof(TView)), reason));
        async UniTask<UIBatchCompletion> CloseBatch(UIRegistration registration, UICloseReason reason)
        {
            if (reason == UICloseReason.ContextShutdown || reason == UICloseReason.RootDestroyed || !Enum.IsDefined(typeof(UICloseReason), reason))
                throw Error(UIErrorCode.InvalidRequest, "batch-reason");
            var snapshot = sessions.Where(s => registration == null || s.Registration.Key == registration.Key).ToArray();
            var results = new List<UICompletion>();
            foreach (var session in snapshot)
            {
                if (!session.CloseRequested)
                {
                    session.CloseRequested = true;
                    session.Reason = state == UIContextState.Running ? reason : UICloseReason.ContextShutdown;
                }
                results.Add(await CloseCore(session));
            }
            return new UIBatchCompletion(results, Array.Empty<Exception>());
        }
        public UniTask ClearCacheAsync() => Submit(() => UniTask.FromResult(true)).AsUniTask();

        public UniTask<UIShutdownCompletion> ShutdownAsync()
        {
            var error = SubmissionError(true);
            if (error != null) return UniTask.FromException<UIShutdownCompletion>(error);
            if (state != UIContextState.Running) return shutdown.Task;
            state = UIContextState.Stopping;
            foreach (var session in sessions) session.Reason = UICloseReason.ContextShutdown;
            try { stopping.Cancel(); } catch (Exception e) { Report(Error(UIErrorCode.LifecycleFailed, "stop-signal", null, e)); }
            commands.Enqueue(async () =>
            {
                var results = new List<UICompletion>();
                foreach (var session in sessions.ToArray())
                {
                    session.CloseRequested = true; session.Reason = UICloseReason.ContextShutdown;
                    results.Add(await CloseCore(session));
                }
                var errors = new List<Exception>();
                try { Callback(inputRegistration.Dispose); } catch (Exception e) { errors.Add(e); }
                state = UIContextState.Stopped;
                var result = new UIShutdownCompletion(shutdownCompletions, errors, pendingCleanup);
                completions.Post(() => shutdown.TrySetResult(result));
            });
            return shutdown.Task;
        }

        void UserCloseRequested(UISessionId sessionId, UICloseReason reason)
        {
            var session = sessions.FirstOrDefault(s => s.Id == sessionId);
            if (state != UIContextState.Running || session == null || session.State != UIState.Open || session.UserRequestPending || session.CloseRequested) return;
            if (reason != UICloseReason.UserRequest && reason != UICloseReason.Back && reason != UICloseReason.Mask) return;
            session.UserRequestPending = true;
            commands.Enqueue(async () =>
            {
                if (session.State != UIState.Open) return;
                bool accepts;
                try { accepts = Callback(() => session.View.InvokeCanClose(reason)); }
                catch (Exception e) { Report(Error(UIErrorCode.LifecycleFailed, "can-close", session, e)); return; }
                finally { session.UserRequestPending = false; }
                if (!accepts) return;
                session.CloseRequested = true; session.Reason = reason;
                await CloseCore(session);
            });
        }
        void ForegroundChanged(UISessionId sessionId, bool eligible)
        {
            commands.Enqueue(async () =>
            {
                var session = sessions.FirstOrDefault(s => s.Id == sessionId);
                if (session == null || session.State != UIState.Open) return;
                try
                {
                    if (eligible)
                    {
                        EnterForeground(session);
                        inputOwner = new UIInputOwner(id, session.Id, session.OwnershipGeneration);
                    }
                    else
                    {
                        ExitForeground(session);
                        if (inputOwner?.SessionId == session.Id) inputOwner = null;
                    }
                    Callback(() => session.Host.SetInteractive(eligible));
                }
                catch (Exception e)
                {
                    session.Errors.Add(e); session.Reason = UICloseReason.OpenFailed;
                    await CloseCore(session);
                }
            });
        }
        public TService GetService<TService>() where TService : class
        {
            if (state != UIContextState.Running) throw Error(UIErrorCode.ContextStopped, "service");
            if (dependencies.Services.TryGetValue(typeof(TService), out var service)) return (TService)service;
            throw new InvalidOperationException("Service is not registered.");
        }
        public UIDiagnosticsSnapshot GetDiagnostics()
            => new UIDiagnosticsSnapshot(id, state, dependencies.Settings.DiagnosticsLevel, dependencies.Scheduler.UnscaledTime,
                sessions.Select(s => new UISessionDiagnostics(s.Id, s.Registration.Key, s.State)),
                sessions.Count(s => s.Lease != null), isolatedLeases, Array.Empty<UIViewTypeKey>(), inputOwner, inputLocks, pendingCleanup);
    }
}
