using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;

namespace Wulato.UIKit
{
    public readonly struct UISessionDiagnostics
    {
        public UISessionId Id { get; }
        public UIViewTypeKey Type { get; }
        public UIState State { get; }
        public UISessionDiagnostics(UISessionId id,UIViewTypeKey type,UIState state) { Id=id;Type=type;State=state; }
    }
    public readonly struct UIInputOwner
    {
        public UIContextId ContextId { get; }
        public UISessionId SessionId { get; }
        public long Generation { get; }
        public UIInputOwner(UIContextId contextId,UISessionId sessionId,long generation) { ContextId=contextId;SessionId=sessionId;Generation=generation; }
    }
    /// <summary>Copied diagnostics data; no mutable runtime object is exposed.</summary>
    public sealed class UIDiagnosticsSnapshot
    {
        public UIContextId ContextId { get; }
        public UIContextState State { get; }
        public UIDiagnosticsLevel Level { get; }
        public double SampleTime { get; }
        public IReadOnlyList<UISessionDiagnostics> Sessions { get; }
        public int ActiveLeaseCount { get; }
        public int IsolatedLeaseCount { get; }
        public IReadOnlyList<UIViewTypeKey> CacheLru { get; }
        public UIInputOwner? InputOwner { get; }
        public int InputLockCount { get; }
        public int PendingCleanupCount { get; }
        public UIDiagnosticsSnapshot(UIContextId contextId,UIContextState state,UIDiagnosticsLevel level,double sampleTime,
            IEnumerable<UISessionDiagnostics> sessions,int activeLeaseCount,int isolatedLeaseCount,IEnumerable<UIViewTypeKey> cacheLru,
            UIInputOwner? inputOwner,int inputLockCount,int pendingCleanupCount)
        {
            ContextId=contextId;State=state;Level=level;SampleTime=sampleTime;
            Sessions=UIFrozen.Copy(sessions);ActiveLeaseCount=activeLeaseCount;IsolatedLeaseCount=isolatedLeaseCount;
            CacheLru=UIFrozen.Copy(cacheLru);InputOwner=inputOwner;InputLockCount=inputLockCount;PendingCleanupCount=pendingCleanupCount;
        }
    }
}
