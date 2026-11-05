using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;

namespace Wulato.UIKit
{
    public sealed class UIInputRegistration
    {
        public int Priority { get; }
        public Action<UISessionId,bool> ForegroundChanged { get; }
        public Action<UISessionId,UICloseReason> UserCloseRequested { get; }
        public UIInputRegistration(int priority,Action<UISessionId,bool> foregroundChanged,Action<UISessionId,UICloseReason> userCloseRequested)
        { Priority=priority;ForegroundChanged=foregroundChanged;UserCloseRequested=userCloseRequested; }
    }
    public readonly struct UIInputCandidate
    {
        public UIContextId ContextId { get; }
        public UISessionId SessionId { get; }
        public UIViewBase View { get; }
        public UIInputPolicy Policy { get; }
        public UIInputCandidate(UIContextId contextId,UISessionId sessionId,UIViewBase view,UIInputPolicy policy)
        { ContextId=contextId;SessionId=sessionId;View=view;Policy=policy; }
    }
    public readonly struct UIInputGrant
    {
        public UIInputCandidate Candidate { get; }
        public bool IsEligible { get; }
        public long OwnershipGeneration { get; }
        public UIInputGrant(UIInputCandidate candidate,bool isEligible,long ownershipGeneration)
        { Candidate=candidate;IsEligible=isEligible;OwnershipGeneration=ownershipGeneration; }
    }
    /// <summary>Shared input-domain arbitration; implementations must validate ownership generations.</summary>
    public interface IUIInputArbiter
    {
        IDisposable Register(UIContextId contextId,UIInputRegistration registration);
        IDisposable AcquireLock(UIContextId contextId);
        UIInputGrant Prepare(UIInputCandidate candidate);
        void Commit(UIInputGrant grant);
        void Revoke(UIContextId contextId,UISessionId sessionId);
    }
}
