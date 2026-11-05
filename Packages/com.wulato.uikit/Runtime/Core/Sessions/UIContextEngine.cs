using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;

namespace Wulato.UIKit
{
    /// <summary>Logical command engine. Unity host construction is a separate first-party backend.</summary>
    internal sealed partial class UIContextEngine : IUIContext, IUITransactionAccess, IUIPageTransactionAccess
    {
        readonly UIContextId id;
        readonly UIRuntimeDependencies dependencies;
        readonly UICompletionQueue completions;
        readonly UICommandQueue commands;
        readonly List<UISessionHandle> sessions = new List<UISessionHandle>();
        readonly List<UICompletion> shutdownCompletions = new List<UICompletion>();
        readonly CancellationTokenSource stopping;
        readonly UniTaskCompletionSource<UIShutdownCompletion> shutdown = new UniTaskCompletionSource<UIShutdownCompletion>();
        readonly IDisposable inputRegistration;
        readonly UIViewCache cache;
        readonly UIShutdownBudget shutdownBudget;
        readonly UIOrphanRegistry orphans;
        readonly IUIRuntimeHostProvider hostProvider;
        readonly IUIRuntimeHost runtimeHost;
        readonly IDisposable runtimeAttachment;
        UIDiagnosticsSnapshot finalDiagnostics;
        UICloseReason shutdownReason = UICloseReason.ContextShutdown;
        bool rootAvailable = true;
        readonly int mainThread = Thread.CurrentThread.ManagedThreadId;
        UIContextState state = UIContextState.Running;
        long sequence;
        readonly UICallbackScope callbacks = new UICallbackScope();
        int inputLocks;
        UIInputOwner? inputOwner;

        internal UIContextEngine(UIContextId id, UIRuntimeDependencies dependencies)
        {
            this.id = id;
            this.dependencies = dependencies ?? throw new ArgumentNullException(nameof(dependencies));
            stopping = dependencies.Lifetime.Source;
            cache = new UIViewCache(dependencies.Settings.CacheCapacity);
            shutdownBudget = new UIShutdownBudget(dependencies.Scheduler, dependencies.Settings.ShutdownBudget);
            orphans = new UIOrphanRegistry(dependencies.Scheduler, dependencies.ErrorSink, id, callbacks.Invoke);
            hostProvider = dependencies.HostFactory as IUIRuntimeHostProvider;
            runtimeHost = hostProvider?.RuntimeHost;
            completions = new UICompletionQueue(orphans.ReportCompletion);
            commands = new UICommandQueue(completions, e => Report(Error(UIErrorCode.LifecycleFailed, "command", null, e)));
            inputRegistration = dependencies.InputArbiter.Register(id,
                new UIInputRegistration(0, ForegroundChanged, UserCloseRequested));
            runtimeAttachment = runtimeHost?.Attach(completions, orphans);
            if (hostProvider != null)
            {
                rootAvailable = hostProvider.RootAvailable;
                hostProvider.RootDestroyed += OnRootDestroyed;
                hostProvider.AvailabilityChanged += OnRootAvailabilityChanged;
            }
        }

        UIException Error(UIErrorCode code, string stage, UISessionHandle session = null, Exception cause = null)
            => new UIException(code, stage, id, session?.Id, cause);

        Exception SubmissionError(bool allowStopping = false)
        {
            if (Thread.CurrentThread.ManagedThreadId != mainThread)
                return Error(UIErrorCode.InvalidRequest, "main-thread");
            if (callbacks.Depth != 0) return Error(UIErrorCode.InvalidRequest, "nested-command");
            if (!allowStopping && (state != UIContextState.Running || !rootAvailable || (hostProvider != null && !hostProvider.RootAvailable))) return Error(UIErrorCode.ContextStopped, "submit");
            return null;
        }

        T Callback<T>(Func<T> callback) => callbacks.Invoke(callback);
        void Callback(Action callback) => callbacks.Invoke(callback);
        T Callback<T>(Func<T> callback,Unity.Profiling.ProfilerMarker marker)
        {using(marker.Auto())return callbacks.Invoke(callback);}
        void Callback(Action callback,Unity.Profiling.ProfilerMarker marker)
        {using(marker.Auto())callbacks.Invoke(callback);}
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
            }, () => completions.Post(() => result.TrySetException(Error(UIErrorCode.ContextStopped, "queued-stop"))));
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

        async UniTask<IUISession> Open(UIRegistration entry, UIArguments args, CancellationToken caller, bool prepareOnly = false, UITransitionOwner owner = UITransitionOwner.CoreDefault)
        {
            caller.ThrowIfCancellationRequested();
            if (!entry.Accepts(args)) throw Error(UIErrorCode.InvalidRequest, "arguments");
            if (entry.Multiplicity == UIMultiplicity.Single && sessions.Any(s => s.Registration.Key == entry.Key))
                throw Error(UIErrorCode.AlreadyOpen, "single");
            var session = new UISessionHandle(this, new UISessionId(++sequence), entry) { TransitionOwner = owner };
            sessions.Add(session);
            string stage = "load";
            using (var opening = CancellationTokenSource.CreateLinkedTokenSource(caller, stopping.Token))
            {
                IDisposable inputLock = null;
                CancellationTokenRegistration sessionCancellation = default;
                try
                {
                    inputLock = Callback(() => dependencies.InputArbiter.AcquireLock(id)); inputLocks++;
                    var cached = cache.Take(entry.Key);
                    if (cached != null && !IsHealthy(cached)) { ReleaseInstance(cached, session.Errors, "cache-invalid"); cached = null; }
                    if (cached != null)
                    {
                        session.Host = cached.Host; session.Lease = cached.Lease;
                        session.View = cached.View; session.Instance = cached.Instance;
                    }
                    else
                    {
                        session.Host = Callback(() => dependencies.HostFactory.Create(entry.Layer));
                        session.Lease = await RunOperation(session,
                            token => dependencies.Loader.CreateAsync(entry.Asset, session.Host.InactiveParent, token),
                            opening.Token, dependencies.Settings.LoadTimeout, "load", true,
                            record => AdoptLateLoad(record, session));
                        opening.Token.ThrowIfCancellationRequested();
                        session.View = session.Lease?.View;
                        if (session.View == null || session.View.GetType() != entry.ViewType)
                            throw Error(UIErrorCode.LoadFailed, "view-binding", session);
                    }
                    session.OpenDeadline = dependencies.Scheduler.UnscaledTime + dependencies.Settings.OpenTimeout.TotalSeconds;
                    stage = "create";
                    if (cached == null)
                    {
                        session.Instance = new UIInstanceScope();
                        Callback(session.View.InvokeCreate,UIProfiler.Hooks);
                    }
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
                        Callback(() => session.Host.SetVisible(false));
                        opening.Token.ThrowIfCancellationRequested();
                    }
                    stage = "open";
                    await AwaitStage(session, token => { session.OpenEntered = true; return session.View.InvokeOpenAsync(args, session.Scope, token); },
                        opening.Token, OpenRemaining(session), "open");
                    stage = "layout";
                    await AwaitStage(session, token => dependencies.LayoutReady.WaitLayoutAsync(session.View, token),
                        opening.Token, OpenRemaining(session), "layout");
                    stage = "enter";
                    await RunTransition(session, true, opening.Token);
                    if (prepareOnly) return session;
                    stage = "foreground";
                    CommitPrepared(session, opening.Token);
                    return session;
                }
                catch (Exception e)
                {
                    await dependencies.Scheduler.SwitchToMainThreadAsync(CancellationToken.None);
                    session.Reason = state == UIContextState.Running
                        ? (e is OperationCanceledException ? UICloseReason.OpenCanceled : UICloseReason.OpenFailed)
                        : shutdownReason;
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
            => session.TransitionContext ?? (session.TransitionContext = new UITransitionContext(session.Scope, session.Host.AnimationRoot, session.Host.AnimationOpacity));

        async UniTask RunTransition(UISessionHandle session, bool entering, CancellationToken caller, IUITransition external = null)
        {
            if (session.TransitionOwner == UITransitionOwner.External && external == null) return;
            var transition = external ?? dependencies.Transition;
            var context = TransitionContext(session);
            Callback(() => context.Capture(transition.Writes));
            var stage = entering ? "enter" : "exit";
            var duration = shutdownBudget.Clamp(entering ? dependencies.Settings.EnterTimeout : dependencies.Settings.ExitTimeout);
            caller.ThrowIfCancellationRequested();
            if (duration <= TimeSpan.Zero) throw Error(UIErrorCode.Timeout, stage, session);
            using (var timer = new UIWaitTimer(dependencies.Scheduler, duration))
            using (var phase = CancellationTokenSource.CreateLinkedTokenSource(caller, shutdownBudget.Token, timer.Token))
            {
                UIOperationRecord<bool> record = null;
                try
                {
                    phase.Token.ThrowIfCancellationRequested();
                    record = new UIOperationRecord<bool>(ToValue(Callback(() => entering
                        ? transition.EnterAsync(context, phase.Token)
                        : transition.ExitAsync(context, phase.Token),UIProfiler.Transition)));
                    // EnterAsync applies its initial state synchronously before this gate opens.
                    if (entering) Callback(() => session.Host.SetVisible(true));
                    await record.WaitAsync(phase.Token);
                    await dependencies.Scheduler.SwitchToMainThreadAsync(CancellationToken.None);
                    phase.Token.ThrowIfCancellationRequested();
                    Callback(() => transition.Reset(context));
                    Callback(context.RestoreBaseline);
                }
                catch (Exception error)
                {
                    await dependencies.Scheduler.SwitchToMainThreadAsync(CancellationToken.None);
                    Collect(session, () => session.Host.SetInteractive(false));
                    Collect(session, () => session.Host.SetVisible(false));
                    Collect(session, () => transition.Stop(context));
                    var failure = record?.ObserveCompletedFailure();
                    if (failure != null && !ReferenceEquals(failure, error))
                    { session.Errors.Add(failure); Report(Error(UIErrorCode.TransitionFailed, "late-" + stage, session, failure)); }
                    if (record != null && !record.Task.Status.IsCompleted())
                    {
                        var protocol = Error(UIErrorCode.TransitionFailed, "stop-protocol", session,
                            new InvalidOperationException("Stop returned with an active transition."));
                        session.Errors.Add(protocol); Report(protocol);
                        session.AbandonedOperation = record; session.AbandonedStage = stage;
                        session.AbandonedCode = UIErrorCode.TransitionFailed;
                    }
                    else
                    {
                        Collect(session, () => transition.Reset(context));
                        Collect(session, context.RestoreBaseline);
                    }
                    if (caller.IsCancellationRequested) throw new OperationCanceledException(caller);
                    if (timer.Token.IsCancellationRequested || shutdownBudget.Token.IsCancellationRequested)
                        throw Error(UIErrorCode.Timeout, stage, session, timer.Failure ?? error);
                    throw;
                }
            }
        }

        TimeSpan OpenRemaining(UISessionHandle session)
            => UIShutdownBudget.Until(session.OpenDeadline, dependencies.Scheduler.UnscaledTime, dependencies.Settings.OpenTimeout);

        async UniTask<T> RunOperation<T>(UISessionHandle session, Func<CancellationToken, UniTask<T>> invoke,
            CancellationToken caller, TimeSpan duration, string stage, bool ownsResult, Action<UIOperationRecord<T>> abandoned)
        {
            caller.ThrowIfCancellationRequested();
            var allowed = shutdownBudget.Clamp(duration);
            if (allowed <= TimeSpan.Zero) throw Error(UIErrorCode.Timeout, stage, session);
            using (var timer = new UIWaitTimer(dependencies.Scheduler, allowed))
            using (var phase = CancellationTokenSource.CreateLinkedTokenSource(caller, shutdownBudget.Token, timer.Token))
            {
                UIOperationRecord<T> record = null;
                try
                {
                    phase.Token.ThrowIfCancellationRequested();
                    record = new UIOperationRecord<T>(Callback(() => invoke(phase.Token),UIProfiler.Stage(stage)));
                    return await record.WaitAsync(phase.Token);
                }
                catch (Exception e)
                {
                    await dependencies.Scheduler.SwitchToMainThreadAsync(CancellationToken.None);
                    var canceled = caller.IsCancellationRequested;
                    var timedOut = timer.Token.IsCancellationRequested || shutdownBudget.Token.IsCancellationRequested;
                    var completedFailure = record?.ObserveCompletedFailure();
                    if (completedFailure != null && (canceled || timedOut))
                    {
                        session.Errors.Add(completedFailure);
                        var code = stage == "load" ? UIErrorCode.LoadFailed : stage == "enter" || stage == "exit" ? UIErrorCode.TransitionFailed : UIErrorCode.LifecycleFailed;
                        Report(Error(code, "late-" + stage, session, completedFailure));
                    }
                    if (record != null && (!record.Task.Status.IsCompleted() || (ownsResult && record.Task.Status == UniTaskStatus.Succeeded)))
                        abandoned(record);
                    if (canceled) throw new OperationCanceledException(caller);
                    if (timedOut)
                        throw Error(UIErrorCode.Timeout, stage, session, timer.Failure ?? e);
                    throw;
                }
                finally { await dependencies.Scheduler.SwitchToMainThreadAsync(CancellationToken.None); }
            }
        }
        async UniTask AwaitStage(UISessionHandle session, Func<CancellationToken, UniTask> invoke,
            CancellationToken caller, TimeSpan duration, string stage)
        {
            await RunOperation(session, token => ToValue(invoke(token)), caller, duration, stage, false,
                record =>
                {
                    session.AbandonedOperation = record; session.AbandonedStage = stage;
                    session.AbandonedCode = stage == "enter" || stage == "exit" ? UIErrorCode.TransitionFailed : UIErrorCode.LifecycleFailed;
                });
            caller.ThrowIfCancellationRequested();
        }
        static async UniTask<bool> ToValue(UniTask operation) { await operation; return true; }
        void Isolate(IUIViewHost host, UISessionHandle session)
        {
            if (host is IUIIsolatableHost isolatable && runtimeHost != null)
                Collect(session, () => isolatable.MoveToIsolation(runtimeHost.IsolationRoot));
            else Collect(session, host.MoveToCache);
        }
        void AdoptLateLoad(UIOperationRecord<IUIViewLease> record, UISessionHandle session)
        {
            var host = session.Host; session.Host = null; Isolate(host, session);
            // These closures retain only the handed-off host/lease, not the Session or engine.
            session.Orphan = orphans.Adopt(record, session.Id, "late-load", UIErrorCode.LoadFailed, false, lease =>
            {
                var errors = new List<Exception>();
                try { host.DetachView(); } catch (Exception e) { errors.Add(e); }
                try { lease?.Dispose(); } catch (Exception e) { errors.Add(e); }
                if (errors.Count != 0) throw new AggregateException(errors);
            }, host.Dispose);
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
        static UICachedInstance InstanceOf(UISessionHandle session) => new UICachedInstance
        {
            Registration = session.Registration, Host = session.Host, Lease = session.Lease,
            View = session.View, Instance = session.Instance, LastSessionId = session.Id
        };
        bool IsHealthy(UICachedInstance item)
            => Callback(() => item.View != null && item.View.enabled && item.View.gameObject.activeSelf &&
               (!(item.Host is IUIIsolatableHost host) || host.IsHealthy));
        void ReleaseInstance(UICachedInstance item, List<Exception> errors, string stage)
        {
            void Release(Action action)
            {
                try { Callback(action); }
                catch (Exception e) { errors.Add(e); Report(new UIException(UIErrorCode.LifecycleFailed, stage, id, item.LastSessionId, e)); }
            }
            if (item.View != null) Release(item.View.InvokeDispose);
            if (item.Instance != null) Release(item.Instance.Dispose);
            if (item.Host != null) Release(item.Host.DetachView);
            if (item.Lease != null) Release(item.Lease.Dispose);
            if (item.Host != null) Release(item.Host.Dispose);
        }
        void Finish(UISessionHandle session, bool notifyClosed, Action beforeCompletion = null)
        {
            if (session.Host != null)
            {
                Collect(session, () => session.Host.SetInteractive(false));
                Collect(session, () => session.Host.SetVisible(false));
            }
            bool isolated = session.AbandonedOperation != null;
            bool cached = false;
            if (session.View != null) session.View.UserCloseRequest = null;
            if (isolated)
            {
                var host = session.Host; var lease = session.Lease; var instance = session.Instance;
                Isolate(host, session);
                session.Orphan = orphans.Adopt(session.AbandonedOperation, session.Id, "late-" + session.AbandonedStage, session.AbandonedCode,
                    true, null, () => instance?.Dispose(), host.DetachView, () => lease?.Dispose(), host.Dispose);
            }
            else
            {
                if (session.View != null && notifyClosed) Collect(session, () => session.View.InvokeClosed(session.Reason));
                if (session.Lease != null)
                {
                    if (session.NormalClose && session.Scope != null && session.Host != null)
                    {
                        if (session.TransitionOwner == UITransitionOwner.CoreDefault && session.TransitionContext != null)
                        {
                            Collect(session, () => dependencies.Transition.Stop(session.TransitionContext));
                            Collect(session, () => dependencies.Transition.Reset(session.TransitionContext));
                            Collect(session, session.TransitionContext.RestoreBaseline);
                        }
                    }
                    if (session.NormalClose && state == UIContextState.Running && session.Errors.Count == 0 &&
                        session.Registration.ClosePolicy == UIClosePolicy.CacheOne && cache.Capacity > 0 && IsHealthy(InstanceOf(session)))
                    {
                        Collect(session, session.Host.MoveToCache);
                        if (session.Errors.Count == 0)
                        {
                            cache.Put(InstanceOf(session), item => ReleaseInstance(item, session.Errors, "cache-evict")); cached = true;
                        }
                    }
                    if (!cached) ReleaseInstance(InstanceOf(session), session.Errors, "release");
                }
                else if (session.Host != null) Collect(session, session.Host.Dispose);
            }
            session.Lease = null; session.Host = null; session.Instance = null; session.View = null;
            for (int i = session.Observers.Count - 1; i >= 0; i--)
            {
                var observer = session.Observers[i];
                Collect(session, () => observer.OnTerminated(new UICompletion(session.Id, session.Reason, session.Errors, session.Orphan?.Pending == true)));
            }
            session.Observers.Clear(); session.AbandonedOperation = null;
            sessions.Remove(session); session.State = UIState.Closed;
            beforeCompletion?.Invoke();
            if (cached && session.Errors.Count != 0)
            {
                var unhealthy = cache.Take(session.Registration.Key);
                if (unhealthy != null) ReleaseInstance(unhealthy, session.Errors, "cache-finalize");
            }
            var result = new UICompletion(session.Id, session.Reason, session.Errors, session.Orphan?.Pending == true);
            session.Result = result;
            if (state == UIContextState.Stopping) shutdownCompletions.Add(result);
            completions.Post(() => session.Completion.TrySetResult(result));
        }

        internal UniTask<UICompletion> Close(UISessionHandle session, UICloseReason reason)
        {
            var error = SubmissionError(true);
            if (error != null) return UniTask.FromException<UICompletion>(error);
            if (reason == UICloseReason.ContextShutdown || reason == UICloseReason.RootDestroyed || !Enum.IsDefined(typeof(UICloseReason), reason))
                return UniTask.FromException<UICompletion>(Error(UIErrorCode.InvalidRequest, "close-reason", session));
            if (session.CloseRequested || session.State == UIState.Closed) return session.Closed;
            session.CloseRequested = true;
            session.Reason = state == UIContextState.Running ? reason : shutdownReason;
            if (state != UIContextState.Running) return session.Closed;
            commands.Enqueue(async () => { if (session.State != UIState.Closed) await CloseCore(session); });
            return session.Closed;
        }
        async UniTask<UICompletion> CloseCore(UISessionHandle session)
        {
            IDisposable inputLock = null;
            session.NormalClose = true;
            Collect(session, () => { inputLock = dependencies.InputArbiter.AcquireLock(id); inputLocks++; });
            try
            {
                TerminateResources(session);
                try
                {
                    await AwaitStage(session, token => session.View.InvokeCloseAsync(session.Reason, token),
                        CancellationToken.None, dependencies.Settings.CloseTimeout, "close");
                }
                catch (Exception e) { session.Errors.Add(e); Report(e as UIException ?? Error(UIErrorCode.LifecycleFailed, "close", session, e)); }
                if (session.AbandonedOperation == null && !(shutdownBudget.Started && shutdownBudget.Remaining == TimeSpan.Zero))
                {
                    try
                    {
                        await RunTransition(session, false, CancellationToken.None);
                    }
                    catch (Exception e) { session.Errors.Add(e); Report(e as UIException ?? Error(UIErrorCode.TransitionFailed, "exit", session, e)); }
                }
            }
            catch (Exception e) { session.Errors.Add(e); Report(Error(UIErrorCode.LifecycleFailed, "terminate", session, e)); }
            finally
            {
                Finish(session, session.AbandonedOperation == null, () =>
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
                    session.Reason = state == UIContextState.Running ? reason : shutdownReason;
                }
                results.Add(await CloseCore(session));
            }
            return new UIBatchCompletion(results, Array.Empty<Exception>());
        }
        public UniTask ClearCacheAsync() => Submit(() =>
        {
            var errors = new List<Exception>(); cache.Clear(item => ReleaseInstance(item, errors, "cache-clear"));
            if (errors.Count != 0) throw Error(UIErrorCode.LifecycleFailed, "cache-clear", null, new AggregateException(errors));
            return UniTask.FromResult(true);
        }).AsUniTask();

        void OnRootAvailabilityChanged(bool available) => rootAvailable = available;
        void OnRootDestroyed() => BeginShutdown(UICloseReason.RootDestroyed);
        public UniTask<UIShutdownCompletion> ShutdownAsync()
        {
            var error = SubmissionError(true);
            return error != null ? UniTask.FromException<UIShutdownCompletion>(error) : BeginShutdown(UICloseReason.ContextShutdown);
        }
        UniTask<UIShutdownCompletion> BeginShutdown(UICloseReason reason)
        {
            if (state == UIContextState.Stopped) return shutdown.Task;
            if (reason == UICloseReason.RootDestroyed) shutdownReason = reason;
            foreach (var session in sessions) session.Reason = shutdownReason;
            if (state == UIContextState.Stopping) return shutdown.Task;
            state = UIContextState.Stopping; shutdownBudget.Start();
            commands.RejectPending();
            try { Callback(stopping.Cancel); } catch (Exception e) { Report(Error(UIErrorCode.LifecycleFailed, "stop-signal", null, e)); }
            commands.Enqueue(async () =>
            {
                foreach (var session in sessions.ToArray())
                {
                    session.CloseRequested = true; session.Reason = shutdownReason;
                    await CloseCore(session);
                }
                var errors = new List<Exception>();
                cache.Clear(item => ReleaseInstance(item, errors, "shutdown-cache"));
                try { Callback(inputRegistration.Dispose); } catch (Exception e) { errors.Add(e); }
                if (hostProvider != null)
                {
                    hostProvider.RootDestroyed -= OnRootDestroyed;
                    hostProvider.AvailabilityChanged -= OnRootAvailabilityChanged;
                }
                // First-party factory owns this Context's wrappers/roots. Shared injected services remain external.
                if (hostProvider is IDisposable hostOwner)
                    try { Callback(hostOwner.Dispose); } catch (Exception e) { errors.Add(e); Report(Error(UIErrorCode.LifecycleFailed, "shutdown-host", null, e)); }
                try { Callback(() => dependencies.StopResources?.Invoke(errors.Add)); } catch (Exception e) { errors.Add(e); }
                shutdownBudget.Dispose();
                state = UIContextState.Stopped;
                finalDiagnostics = BuildDiagnostics();
                var result = new UIShutdownCompletion(shutdownCompletions, errors, orphans.PendingCount);
                completions.Post(() => shutdown.TrySetResult(result));
                runtimeAttachment?.Dispose();
            });
            // Queued request rejection is already committed; the active command remains non-reentrant.
            completions.Drain();
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
            if(Thread.CurrentThread.ManagedThreadId!=mainThread)throw Error(UIErrorCode.InvalidRequest,"main-thread");
            if (state != UIContextState.Running) throw Error(UIErrorCode.ContextStopped, "service");
            if (typeof(TService) == typeof(IUIContextLifetime)) return (TService)(object)dependencies.Lifetime;
            if (typeof(TService) == typeof(UIRegistrySnapshot)) return (TService)(object)dependencies.Registry;
            if (typeof(TService) == typeof(IUIPageTransactionAccess) && dependencies.EnableTransactions) return (TService)(object)this;
            if (typeof(TService) == typeof(IUITransactionAccess) && dependencies.EnableTransactions) return (TService)(object)this;
            if (dependencies.Services.TryGetValue(typeof(TService), out var service)) return (TService)service;
            throw new InvalidOperationException("Service is not registered.");
        }
        public UIDiagnosticsSnapshot GetDiagnostics()
        {
            if(Thread.CurrentThread.ManagedThreadId!=mainThread)throw Error(UIErrorCode.InvalidRequest,"main-thread");
            return finalDiagnostics ?? BuildDiagnostics();
        }
        UIDiagnosticsSnapshot BuildDiagnostics()
            => new UIDiagnosticsSnapshot(id, state, dependencies.Settings.DiagnosticsLevel, dependencies.Scheduler.UnscaledTime,
                dependencies.Settings.DiagnosticsLevel == UIDiagnosticsLevel.Off ? Enumerable.Empty<UISessionDiagnostics>() : sessions.Select(s => new UISessionDiagnostics(s.Id, s.Registration.Key, s.State)),
                sessions.Count(s => s.Lease != null), orphans.IsolatedLeaseCount, dependencies.Settings.DiagnosticsLevel == UIDiagnosticsLevel.Off ? Enumerable.Empty<UIViewTypeKey>() : cache.Keys, inputOwner, inputLocks, orphans.PendingCount);
    }
}
