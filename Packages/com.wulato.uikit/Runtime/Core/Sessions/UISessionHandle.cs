using System;
using System.Collections.Generic;
using Cysharp.Threading.Tasks;
namespace Wulato.UIKit
{
    // Never reused, even if the backing instance is reused later.
    internal sealed class UISessionHandle : IUISession
    {
        readonly UIContextEngine engine;
        internal readonly UniTaskCompletionSource<UICompletion> Completion=new UniTaskCompletionSource<UICompletion>();
        internal readonly UIRegistration Registration;
        internal readonly List<IUISessionLifecycleObserver> Observers=new List<IUISessionLifecycleObserver>();
        internal readonly List<Exception> Errors=new List<Exception>();
        internal IUIViewHost Host;
        internal IUIViewLease Lease;
        internal UIViewBase View;
        internal long OwnershipGeneration;
        internal UIInstanceScope Instance;
        internal UIOperationRecord<bool> AbandonedOperation;
        internal UIOrphanTicket Orphan;
        internal string AbandonedStage;
        internal UIErrorCode AbandonedCode;
        internal double OpenDeadline;
        internal bool NormalClose;
        internal UITransitionContext TransitionContext;
        internal UITransitionOwner TransitionOwner;
        internal UICompletion Result;
        internal DefaultUISessionScope Scope;
        internal DefaultUIForegroundScope Foreground;
        internal bool OpenEntered,CloseRequested,UserRequestPending;
        internal UICloseReason Reason;
        public UIContextId ContextId => engine.ContextId;
        internal bool BelongsTo(UIContextEngine owner)=>ReferenceEquals(engine,owner);
        public UISessionId Id { get; }
        public UIState State { get; internal set; }=UIState.Opening;
        public UniTask<UICompletion> Closed => Completion.Task;
        public UniTask<UICompletion> CloseAsync(UICloseReason reason=UICloseReason.Owner) => engine.Close(this,reason);
        internal UISessionHandle(UIContextEngine engine,UISessionId id,UIRegistration registration) { this.engine=engine;Id=id;Registration=registration; }
    }
}
