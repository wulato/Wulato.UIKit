using System;
using System.Linq;
namespace Wulato.UIKit.Editor
{
    // JSON for recovery contains only values. It is separate from the runtime objects and source assets.
    [Serializable] internal sealed class UIDiagnosticsRecord
    {
        [Serializable] public sealed class Session {public long id;public string type;public UIState state;}
        [Serializable] public sealed class Error {public UIErrorCode code;public string stage,message;public bool hasSession;public long session;}
        public string context,capturedUtc;
        public UIContextState state;
        public UIDiagnosticsLevel level;
        public double time;
        public int leases,isolated,locks,pending;
        public long totalErrors;
        public Session[] sessions;
        public string[] history,cache;
        public Error[] errors;
        public string ownerContext;
        public long ownerSession,ownerGeneration;
        internal static UIDiagnosticsRecord Capture(UIDiagnosticsSnapshot d,string utc)
            =>d==null?null:new UIDiagnosticsRecord {
                context=d.ContextId.ToString(),capturedUtc=utc,state=d.State,level=d.Level,time=d.SampleTime,
                leases=d.ActiveLeaseCount,isolated=d.IsolatedLeaseCount,locks=d.InputLockCount,pending=d.PendingCleanupCount,totalErrors=d.TotalErrorCount,
                sessions=d.Sessions.Select(s=>new Session{id=s.Id.Value,type=s.Type.Value,state=s.State}).ToArray(),
                history=d.History.Select(k=>k.Value).ToArray(),cache=d.CacheLru.Select(k=>k.Value).ToArray(),
                errors=d.Errors.Select(e=>new Error{code=e.Code,stage=e.Stage,message=e.Message,hasSession=e.SessionId.HasValue,session=e.SessionId?.Value??0}).ToArray(),
                ownerContext=d.InputOwner?.ContextId.ToString(),ownerSession=d.InputOwner?.SessionId.Value??0,ownerGeneration=d.InputOwner?.Generation??0};
        internal UIDiagnosticsSnapshot Restore()
        {
            if(!Guid.TryParse(context,out var id)||!Enum.IsDefined(typeof(UIContextState),state)||!Enum.IsDefined(typeof(UIDiagnosticsLevel),level))return null;
            UIInputOwner? owner=Guid.TryParse(ownerContext,out var ownerId)?new UIInputOwner(ownerId==Guid.Empty?default:new UIContextId(ownerId),new UISessionId(ownerSession),ownerGeneration):(UIInputOwner?)null;
            return new UIDiagnosticsSnapshot(new UIContextId(id),state,level,time,
                (sessions??Array.Empty<Session>()).Select(s=>new UISessionDiagnostics(new UISessionId(s.id),new UIViewTypeKey(s.type),s.state)),
                leases,isolated,(cache??Array.Empty<string>()).Select(k=>new UIViewTypeKey(k)),owner,locks,pending,
                (history??Array.Empty<string>()).Select(k=>new UIViewTypeKey(k)),
                (errors??Array.Empty<Error>()).Take(32).Select(e=>new UIErrorDiagnostics(e.code,e.stage,e.hasSession?new UISessionId(e.session):(UISessionId?)null,e.message)),totalErrors);
        }
    }
}
