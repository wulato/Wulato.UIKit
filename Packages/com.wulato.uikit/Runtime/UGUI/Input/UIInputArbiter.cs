using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
namespace Wulato.UIKit
{
    public sealed class UIInputArbiter : IUIInputArbiter, IDisposable
    {
        sealed class Registration {internal UIInputRegistration Value;internal int Locks;internal long Recent;}
        sealed class Candidate {internal UIInputCandidate Value;internal long Order;}
        sealed class Lease : IDisposable {Action action;internal Lease(Action action){this.action=action;}public void Dispose(){var a=action;action=null;a?.Invoke();}}
        readonly UIInputDomain domain;
        readonly Dictionary<UIContextId,Registration> registrations=new Dictionary<UIContextId,Registration>();
        readonly List<Candidate> candidates=new List<Candidate>();
        Candidate owner;
        long generation,order;
        UIModalMask mask;
        bool disposed,arbitrating,rerun;
        public UIInputArbiter(UIInputDomain domain) {this.domain=domain??throw new ArgumentNullException(nameof(domain));domain.Register(this);}
        public IDisposable Register(UIContextId contextId,UIInputRegistration registration)
        {
            Alive();if(registration==null)throw new ArgumentNullException(nameof(registration));
            if(registrations.ContainsKey(contextId))throw new InvalidOperationException("Context already registered.");
            var record=new Registration{Value=registration};registrations.Add(contextId,record);generation++;
            return new Lease(()=>
            {
                if(!registrations.TryGetValue(contextId,out var current)||current!=record)return;
                var removed=candidates.Where(c=>c.Value.ContextId==contextId).ToArray();
                foreach(var c in removed)candidates.Remove(c);
                record.Locks=0;generation++;Arbitrate();registrations.Remove(contextId);
                foreach(var c in removed)if(c.Value.View)domain.Gate.Remove(c.Value.View);
            });
        }
        void Alive(){if(disposed)throw new ObjectDisposedException(nameof(UIInputArbiter));}
        Registration Get(UIContextId id){Alive();if(!registrations.TryGetValue(id,out var r))throw new InvalidOperationException("Unknown Context.");return r;}
        public IDisposable AcquireLock(UIContextId contextId)
        {
            var record=Get(contextId);record.Locks++;generation++;Arbitrate();
            return new Lease(()=>{if(!registrations.TryGetValue(contextId,out var current)||current!=record)return;record.Locks--;generation++;Arbitrate();});
        }
        public UIInputGrant Prepare(UIInputCandidate candidate)
        {
            Get(candidate.ContextId);if(!candidate.View)throw new ArgumentException("A live View is required.");
            var proposed=new Candidate{Value=candidate,Order=order+1};
            var winner=Choose(proposed);
            return new UIInputGrant(candidate,winner==proposed,generation);
        }
        public void Commit(UIInputGrant grant)
        {
            Get(grant.Candidate.ContextId);
            if(grant.OwnershipGeneration!=generation)throw new InvalidOperationException("Input grant is stale.");
            var c=candidates.FirstOrDefault(c=>Same(c.Value,grant.Candidate));
            if(c!=null)candidates.Remove(c);
            c=new Candidate{Value=grant.Candidate,Order=++order};candidates.Add(c);
            domain.Gate.Add(c.Value.View);generation++;Arbitrate(c,grant.IsEligible);
        }
        static bool Same(UIInputCandidate a,UIInputCandidate b)=>a.ContextId==b.ContextId&&a.SessionId==b.SessionId;
        public void Revoke(UIContextId contextId,UISessionId sessionId)
        {
            if(disposed)return;
            var removed=candidates.Where(c=>c.Value.ContextId==contextId&&c.Value.SessionId==sessionId).ToArray();
            foreach(var c in removed)candidates.Remove(c);
            generation++;Arbitrate();
            foreach(var c in removed)if(c.Value.View)domain.Gate.Remove(c.Value.View);
        }
        void Remove(Candidate c){candidates.Remove(c);if(c.Value.View)domain.Gate.Remove(c.Value.View);}
        Candidate Choose(Candidate proposed=null)
        {
            var all=candidates.Where(c=>c.Value.View&&registrations.ContainsKey(c.Value.ContextId)&&c.Value.Policy.TakesInput);
            if(proposed!=null)all=all.Where(c=>!Same(c.Value,proposed.Value)).Concat(new[]{proposed}.Where(c=>c.Value.Policy.TakesInput));
            var best=all.OrderByDescending(c=>registrations[c.Value.ContextId].Value.Priority)
                .ThenByDescending(c=>Math.Max(registrations[c.Value.ContextId].Recent,c.Order)).ThenByDescending(c=>c.Order).FirstOrDefault();
            var barrier=all.Where(c=>c.Value.Policy.BlocksLower).OrderByDescending(c=>registrations[c.Value.ContextId].Value.Priority)
                .ThenByDescending(c=>Math.Max(registrations[c.Value.ContextId].Recent,c.Order)).ThenByDescending(c=>c.Order).FirstOrDefault();
            if(best!=null&&!best.Value.Policy.BlocksLower&&barrier!=null&&
                !(best.Value.Policy.EmergencyPreemption&&registrations[best.Value.ContextId].Value.Priority>registrations[barrier.Value.ContextId].Value.Priority))best=barrier;
            if(owner!=null&&registrations.TryGetValue(owner.Value.ContextId,out var old)&&old.Locks>0)
            {
                if(best!=null&&best.Value.Policy.EmergencyPreemption&&registrations[best.Value.ContextId].Value.Priority>old.Value.Priority)return best;
                if(proposed!=null&&proposed.Value.ContextId==owner.Value.ContextId)return proposed.Value.Policy.TakesInput?proposed:owner;
                return owner;
            }
            return best;
        }
        void Arbitrate(Candidate justCommitted=null,bool alreadyEntered=false)
        {
            if(arbitrating){rerun=true;return;}
            arbitrating=true;
            try
            {
                do
                {
                    rerun=false;var next=disposed?null:Choose();var previous=owner;
                    owner=next;
                    if(previous!=next)
                    {
                        generation++;
                        if(previous!=null&&registrations.TryGetValue(previous.Value.ContextId,out var old))old.Value.ForegroundChanged?.Invoke(previous.Value.SessionId,false);
                        if(next!=null)
                        {
                            registrations[next.Value.ContextId].Recent=++order;
                            if(next!=justCommitted||!alreadyEntered)registrations[next.Value.ContextId].Value.ForegroundChanged?.Invoke(next.Value.SessionId,true);
                        }
                    }
                    bool locked=next!=null&&registrations[next.Value.ContextId].Locks>0;
                    domain.Gate.SetOwner(next?.Value.View,locked,()=>Request(UICloseReason.Back));
                    bool barrier=next!=null&&next.Value.View&&(next.Value.Policy.BlocksLower||locked);
                    if(barrier&& !mask)mask=UIModalMask.Create(next.Value.View.transform.parent);
                    if(mask)mask.Place(next?.Value.View,barrier,locked?null:(Action)(()=>Request(UICloseReason.Mask)));
                }while(rerun);
            }
            finally {arbitrating=false;}
        }
        void Request(UICloseReason reason)
        {
            var current=owner;
            if(current!=null&&registrations.TryGetValue(current.Value.ContextId,out var r)&&r.Locks==0)
                r.Value.UserCloseRequested?.Invoke(current.Value.SessionId,reason);
        }
        public void Dispose()
        {
            if(disposed)return;disposed=true;
            domain.Gate.SetOwner(null,true,null);
            foreach(var c in candidates.ToArray())Remove(c);
            owner=null;registrations.Clear();
            if(mask)UnityEngine.Object.Destroy(mask.gameObject);
            if(domain.Arbiter==this)domain.Arbiter=null;
        }
    }
}
