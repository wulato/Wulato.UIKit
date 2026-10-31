using System;
namespace Wulato.UIKit.Tests.Support
{
    public sealed class ActionResource : IDisposable
    {
        Action action; public ActionResource(Action action) { this.action=action; }
        public void Dispose() { var a=action;action=null;a?.Invoke(); }
    }
    public sealed class FakeInputArbiter : IUIInputArbiter
    {
        public int Locks,Commits; public bool Eligible=true;
        public UIInputRegistration Registration;
        public IDisposable Register(UIContextId id,UIInputRegistration registration) { Registration=registration;return new ActionResource(()=>Registration=null); }
        public IDisposable AcquireLock(UIContextId id) { Locks++;return new ActionResource(()=>Locks--); }
        public UIInputGrant Prepare(UIInputCandidate c) => new UIInputGrant(c,Eligible&&c.Policy.TakesInput,1);
        public void Commit(UIInputGrant grant) { Commits++; }
        public void Revoke(UIContextId id,UISessionId session) { }
    }
}
