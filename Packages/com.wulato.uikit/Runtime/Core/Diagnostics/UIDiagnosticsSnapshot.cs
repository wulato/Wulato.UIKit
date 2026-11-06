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
    /// <summary>Value-only error summary. Does not retain an Exception, View or business arguments.</summary>
    public readonly struct UIErrorDiagnostics
    {
        public UIErrorCode Code { get; }
        public string Stage { get; }
        public UISessionId? SessionId { get; }
        public string Message { get; }
        internal UIErrorDiagnostics(UIErrorCode code,string stage,UISessionId? session,string message)
        {Code=code;Stage=stage;SessionId=session;Message=message??string.Empty;}
        internal UIErrorDiagnostics(UIException error)
        {
            Code=error.Code;Stage=error.Stage;SessionId=error.SessionId;
            string message;
            try{message=error.InnerException?.Message??error.Message;}catch{message="Error message unavailable";}
            Message=message.Length>1024?message.Substring(0,1024):message;
        }
    }
    // Context-owned bounded recording; forwarding preserves the original sink/exception and ownership.
    internal sealed class UIDiagnosticErrorSink:IUIErrorSink
    {
        readonly IUIErrorSink next;readonly bool enabled;
        readonly Queue<UIErrorDiagnostics> recent=new Queue<UIErrorDiagnostics>();
        internal long Total { get; private set; }
        internal IEnumerable<UIErrorDiagnostics> Recent=>recent;
        internal UIDiagnosticErrorSink(IUIErrorSink next,bool enabled){this.next=next;this.enabled=enabled;}
        public void Report(UIException error)
        {
            if(enabled){Total++;if(recent.Count==32)recent.Dequeue();recent.Enqueue(new UIErrorDiagnostics(error));}
            next.Report(error);
        }
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
        public IReadOnlyList<UIViewTypeKey> History { get; }
        public IReadOnlyList<UIErrorDiagnostics> Errors { get; }
        public long TotalErrorCount { get; }
        public UIDiagnosticsSnapshot(UIContextId contextId,UIContextState state,UIDiagnosticsLevel level,double sampleTime,
            IEnumerable<UISessionDiagnostics> sessions,int activeLeaseCount,int isolatedLeaseCount,IEnumerable<UIViewTypeKey> cacheLru,
            UIInputOwner? inputOwner,int inputLockCount,int pendingCleanupCount,
            IEnumerable<UIViewTypeKey> history=null,IEnumerable<UIErrorDiagnostics> errors=null,long totalErrorCount=0)
        {
            ContextId=contextId;State=state;Level=level;SampleTime=sampleTime;
            Sessions=UIFrozen.Copy(sessions);ActiveLeaseCount=activeLeaseCount;IsolatedLeaseCount=isolatedLeaseCount;
            History=UIFrozen.Copy(history??Array.Empty<UIViewTypeKey>());Errors=UIFrozen.Copy(errors??Array.Empty<UIErrorDiagnostics>());TotalErrorCount=totalErrorCount;
            CacheLru=UIFrozen.Copy(cacheLru);InputOwner=inputOwner;InputLockCount=inputLockCount;PendingCleanupCount=pendingCleanupCount;
        }
    }
}
