using System;
using System.Threading;
using System.Linq;
using Cysharp.Threading.Tasks;
namespace Wulato.UIKit
{
    internal sealed partial class UIContextEngine
    {
        UniTask IUIPageTransactionAccess.ExecuteAsync(Func<IUIPageTransactionSteps,CancellationToken,UniTask> transaction,
            TimeSpan pairTimeout,CancellationToken cancellationToken)
        {
            if(!dependencies.EnableTransactions||transaction==null||pairTimeout<=TimeSpan.Zero)
                return UniTask.FromException(Error(UIErrorCode.InvalidRequest,"page-transaction"));
            return Submit(async()=>
            {
                cancellationToken.ThrowIfCancellationRequested();
                using(var operation=CancellationTokenSource.CreateLinkedTokenSource(stopping.Token))
                {
                    var basic=new TransactionSteps(this,operation.Token);
                    var pages=new PageSteps(this,basic,operation,pairTimeout);
                    using(var caller=cancellationToken.Register(pages.CancelBeforeCommit))
                    {
                        IDisposable inputLock=null;UIOperationRecord<bool> record=null;
                        try
                        {
                            operation.Token.ThrowIfCancellationRequested();
                            inputLock=Callback(()=>dependencies.InputArbiter.AcquireLock(id));inputLocks++;
                            record=new UIOperationRecord<bool>(ToValue(transaction(pages,operation.Token)));
                            await record.WaitAsync(operation.Token);
                            await dependencies.Scheduler.SwitchToMainThreadAsync(CancellationToken.None);
                            operation.Token.ThrowIfCancellationRequested();
                            if(basic.InFlight)throw Error(UIErrorCode.InvalidRequest,"unfinished-page-step");
                        }
                        finally
                        {
                            await dependencies.Scheduler.SwitchToMainThreadAsync(CancellationToken.None);
                            basic.Active=false;
                            if(basic.InFlight)
                            {
                                try{operation.Cancel();}catch(Exception e){Report(Error(UIErrorCode.LifecycleFailed,"page-cancel",null,e));}
                                await basic.WaitForStepAsync();
                            }
                            if(record!=null&&!record.Task.Status.IsCompleted())
                                orphans.Adopt(record,default,"late-page-transaction",UIErrorCode.TransitionFailed,false,null);
                            basic.Cleanup();
                            if(inputLock!=null){try{Callback(inputLock.Dispose);}finally{inputLocks--;}}
                        }
                    }
                }
                return true;
            }).AsUniTask();
        }
        sealed class PageSteps : IUIPageTransactionSteps
        {
            readonly UIContextEngine engine;
            readonly TransactionSteps steps;
            readonly CancellationTokenSource operation;
            readonly TimeSpan timeout;
            readonly object gate=new object();
            bool committed;
            readonly Guid transactionId=Guid.NewGuid();
            internal PageSteps(UIContextEngine engine,TransactionSteps steps,CancellationTokenSource operation,TimeSpan timeout)
            {this.engine=engine;this.steps=steps;this.operation=operation;this.timeout=timeout;}
            internal void CancelBeforeCommit()
            {lock(gate){if(!committed)operation.Cancel();}}
            public UniTask<IUISession> PrepareAsync(UIViewTypeKey type,UIArguments args,UITransitionOwner owner,CancellationToken token=default)
                =>steps.PrepareAsync(type,args,owner,token);
            public void Commit(IUISession session)=>throw engine.Error(UIErrorCode.InvalidRequest,"use-page-replacement");
            public UniTask RollbackAsync(IUISession session)=>steps.RollbackAsync(session);
            public void SetVisible(IUISession session,bool visible)=>steps.SetVisible(session,visible);
            public UniTask AnimateAsync(IUISession session,IUITransition transition,bool entering,CancellationToken token=default)
                =>steps.AnimateAsync(session,transition,entering,token);
            public UniTask<UICompletion> CloseAsync(IUISession session,UITransitionOwner owner,UICloseReason reason=UICloseReason.Owner)
                =>steps.CloseAsync(session,owner,reason);
            public async UniTask<IUISession> RouteBackAsync(IUISession page,bool hasPrevious,UISessionId? inputSource=null)
            {
                steps.Guard();if(committed)throw engine.Error(UIErrorCode.InvalidRequest,"back-already-committed");
                var owner=engine.inputOwner.HasValue?engine.sessions.FirstOrDefault(s=>s.Id==engine.inputOwner.Value.SessionId&&s.State==UIState.Open&&!s.CloseRequested):null;
                // A device event is tied to the owner at dispatch, never retargeted after a queued replacement.
                if(inputSource.HasValue&&(owner==null||owner.Id!=inputSource.Value))return null;
                // Explicit Context Back stays local even when another Context owns the shared domain.
                if(owner==null&&!inputSource.HasValue)
                    owner=engine.sessions.LastOrDefault(s=>s.State==UIState.Open&&!s.CloseRequested&&s.Registration.Layer!=UILayer.Page&&s.Registration.InputPolicy.TakesInput);
                if(owner!=null&&owner.Registration.Layer!=UILayer.Page)
                {
                    if(!engine.CanUserClose(owner,UICloseReason.Back))return null;
                    lock(gate)
                    {
                        steps.Guard();
                        // Accepted closure is the commit point; caller cancellation cannot abandon its Closed barrier.
                        steps.committed=true;committed=true;
                    }
                    await steps.CloseAsync(owner,owner.TransitionOwner,UICloseReason.Back);return owner;
                }
                if(page==null||!hasPrevious)return null;
                var current=steps.Find(page);
                if(owner!=null&&!ReferenceEquals(owner,current))return null;
                if(!engine.CanUserClose(current,UICloseReason.Back))return null;
                steps.Guard();return page;
            }
            UISessionHandle Validate(IUISession session,bool target)
            {
                var handle=steps.Find(session);
                if(target ? !steps.prepared.Contains(handle)||handle.State!=UIState.Opening||handle.TransitionOwner!=UITransitionOwner.External : handle.State!=UIState.Open)
                    throw engine.Error(UIErrorCode.InvalidRequest,"page-session",handle);
                return handle;
            }
            public void CommitReplacement(IUISession from,IUISession to,IUITransactionParticipant participant)
            {
                lock(gate)
                {
                    steps.Guard();if(committed||participant==null)throw engine.Error(UIErrorCode.InvalidRequest,"page-commit");
                    var target=Validate(to,true);var source=from==null?null:Validate(from,false);
                    if(ReferenceEquals(source,target))throw engine.Error(UIErrorCode.InvalidRequest,"same-page");
                    // Cancellation may resume the transaction waiter inline from a backend callback.
                    // Keep cleanup behind this synchronous step until publication or rollback settles.
                    steps.BeginStep();
                    try
                    {
                        engine.Callback(participant.Prepare);
                        operation.Token.ThrowIfCancellationRequested();
                        if(source!=null)
                        {
                            engine.Callback(()=>source.Host.SetVisible(false));engine.ExitForeground(source);
                            engine.Callback(()=>engine.dependencies.InputArbiter.Revoke(engine.id,source.Id));
                        }
                        engine.CommitPrepared(target,operation.Token);
                        operation.Token.ThrowIfCancellationRequested();
                        engine.Callback(participant.Commit);
                        steps.committed=true;committed=true;
                    }
                    catch
                    {
                        try{engine.Callback(participant.Rollback);}catch(Exception e){engine.Report(engine.Error(UIErrorCode.LifecycleFailed,"participant-rollback",target,e));}
                        // CommitPrepared may already have changed State before a backend commit fault.
                        if(steps.prepared.Contains(target)){steps.Rollback(target);}
                        throw;
                    }
                    finally{steps.EndStep();}
                }
            }
            public async UniTask AnimatePairAsync(IUISession from,IUISession to,INavigationTransition transition,CancellationToken cancellationToken=default)
            {
                steps.Guard();if(transition==null)throw new ArgumentNullException(nameof(transition));
                var target=Validate(to,true);var source=from==null?null:Validate(from,false);
                if(ReferenceEquals(source,target))throw engine.Error(UIErrorCode.InvalidRequest,"same-page");
                var context=new UINavigationTransitionContext(source==null?null:new UITransitionContext(source.Scope,source.Host.AnimationRoot,source.Host.AnimationOpacity),
                    new UITransitionContext(target.Scope,target.Host.AnimationRoot,target.Host.AnimationOpacity),transactionId);
                engine.Callback(()=>context.From?.Capture(transition.FromWrites));
                engine.Callback(()=>context.To.Capture(transition.ToWrites));
                steps.BeginStep();
                try
                {
                    using(var timer=new UIWaitTimer(engine.dependencies.Scheduler,engine.shutdownBudget.Clamp(timeout)))
                    using(var phase=CancellationTokenSource.CreateLinkedTokenSource(operation.Token,cancellationToken,timer.Token))
                    {
                        UIOperationRecord<bool> record=null;
                        try
                        {
                            phase.Token.ThrowIfCancellationRequested();
                            engine.Callback(()=>target.Host.SetVisible(true));engine.Callback(()=>target.Host.SetInteractive(false));
                            record=new UIOperationRecord<bool>(ToValue(engine.Callback(()=>transition.PlayAsync(context,phase.Token))));
                            await record.WaitAsync(phase.Token);
                            await engine.dependencies.Scheduler.SwitchToMainThreadAsync(CancellationToken.None);
                            phase.Token.ThrowIfCancellationRequested();
                            engine.Callback(()=>transition.Reset(context));engine.Callback(()=>context.From?.RestoreBaseline());engine.Callback(context.To.RestoreBaseline);
                        }
                        catch(Exception error)
                        {
                            await engine.dependencies.Scheduler.SwitchToMainThreadAsync(CancellationToken.None);
                            engine.Collect(target,()=>target.Host.SetVisible(false));engine.Collect(target,()=>transition.Stop(context));
                            var failure=record?.ObserveCompletedFailure();
                            if(failure!=null&&!ReferenceEquals(failure,error))engine.Report(engine.Error(UIErrorCode.TransitionFailed,"late-pair",target,failure));
                            if(record!=null&&!record.Task.Status.IsCompleted())
                            {
                                target.AbandonedOperation=record;target.AbandonedStage="pair";target.AbandonedCode=UIErrorCode.TransitionFailed;
                                if(source!=null)
                                {
                                    source.AbandonedOperation=record;source.AbandonedStage="pair";source.AbandonedCode=UIErrorCode.TransitionFailed;
                                    source.Reason=UICloseReason.OpenFailed;engine.TerminateResources(source);engine.Finish(source,false);
                                }
                                engine.Report(engine.Error(UIErrorCode.TransitionFailed,"pair-stop-protocol",target,error));
                            }
                            else
                            {engine.Collect(target,()=>transition.Reset(context));engine.Collect(target,()=>context.From?.RestoreBaseline());engine.Collect(target,context.To.RestoreBaseline);}
                            if(operation.IsCancellationRequested||cancellationToken.IsCancellationRequested)throw new OperationCanceledException(phase.Token);
                            if(timer.Token.IsCancellationRequested)throw engine.Error(UIErrorCode.Timeout,"pair",target,error);
                            throw error as UIException??engine.Error(UIErrorCode.TransitionFailed,"pair",target,error);
                        }
                    }
                }
                finally{steps.EndStep();}
            }
        }
    }
}
