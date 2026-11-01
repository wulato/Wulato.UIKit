using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;

namespace Wulato.UIKit
{
    internal interface IUIRuntimeHost
    {
        Transform IsolationRoot { get; }
        IDisposable Attach(UICompletionQueue notifications, UIOrphanRegistry orphans);
    }
    internal interface IUIRuntimeHostProvider
    {
        IUIRuntimeHost RuntimeHost { get; }
        bool RootAvailable { get; }
        event Action RootDestroyed;
        event Action<bool> AvailabilityChanged;
    }
    internal interface IUIIsolatableHost
    {
        bool IsHealthy { get; }
        void MoveToIsolation(Transform parent);
    }
    internal sealed class UIOrphanTicket { internal bool Pending = true; }
    internal sealed class UIOrphanRegistry
    {
        readonly IUIAsyncScheduler scheduler;
        readonly IUIErrorSink sink;
        readonly UIContextId contextId;
        readonly Action<Action> callback;
        internal int PendingCount { get; private set; }
        internal int IsolatedLeaseCount { get; private set; }
        internal readonly List<UIException> Errors = new List<UIException>();
        internal UIOrphanRegistry(IUIAsyncScheduler scheduler, IUIErrorSink sink, UIContextId contextId, Action<Action> callback = null)
        { this.scheduler = scheduler; this.sink = sink; this.contextId = contextId; this.callback = callback ?? (action => action()); }
        internal UIOrphanTicket Adopt<T>(UIOperationRecord<T> record, UISessionId id, string stage, UIErrorCode code,
            bool hasLease, Action<T> delivered, params Action[] cleanup)
        {
            var ticket = new UIOrphanTicket(); PendingCount++; if (hasLease) IsolatedLeaseCount++;
            Observe(record, ticket, id, stage, code, hasLease, delivered, cleanup).Forget(); return ticket;
        }
        internal void ReportCompletion(Exception cause) => Report(cause, null, "completion", UIErrorCode.LifecycleFailed);
        void Report(Exception cause, UISessionId? id, string stage, UIErrorCode code)
        {
            var error = new UIException(code, stage, contextId, id, cause); Errors.Add(error);
            try { callback(() => sink.Report(error)); } catch (Exception e) { Debug.LogWarning("UIKit error sink failed: " + e.Message); }
        }
        async UniTask Observe<T>(UIOperationRecord<T> record, UIOrphanTicket ticket, UISessionId id, string stage,
            UIErrorCode code, bool hasLease, Action<T> delivered, Action[] cleanup)
        {
            try
            {
                var value = await record.Task;
                await scheduler.SwitchToMainThreadAsync(CancellationToken.None);
                if (delivered != null) callback(() => delivered(value));
            }
            catch (OperationCanceledException) { }
            catch (Exception e)
            {
                await scheduler.SwitchToMainThreadAsync(CancellationToken.None); Report(e, id, stage, code);
            }
            finally
            {
                await scheduler.SwitchToMainThreadAsync(CancellationToken.None);
                foreach (var action in cleanup)
                    try { if (action != null) callback(action); } catch (Exception e) { Report(e, id, stage + "-cleanup", code); }
                ticket.Pending = false; PendingCount--; if (hasLease) IsolatedLeaseCount--;
            }
        }
    }
}
