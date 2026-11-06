using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
namespace Wulato.UIKit.Dialogs
{
    /// <summary>Session results with caller cancellation covering the entire waiting period.</summary>
    public sealed class UIDialogs:IUIDialogs
    {
        readonly IUIContext context;readonly IUIContextLifetime lifetime;
        readonly Dictionary<Type,DialogRegistry.Entry> entries;
        /// <inheritdoc/>
        public UIDialogs(IUIContext context)
        {
            this.context=context??throw new ArgumentNullException(nameof(context));
            lifetime=context.GetService<IUIContextLifetime>();entries=DialogRegistry.Copy(context.GetService<UIRegistrySnapshot>());
        }
        /// <inheritdoc/>
        public async UniTask<DialogResult<TResult>> ShowAsync<TResult>(IDialogRequest<TResult> request,CancellationToken cancellationToken=default)
        {
            if(!PlayerLoopHelper.IsMainThread)throw new UIException(UIErrorCode.InvalidRequest,"dialog-main-thread",default);
            if(request==null||!entries.TryGetValue(request.GetType(),out var entry)||entry.ResultType!=typeof(TResult))
                throw new UIException(UIErrorCode.InvalidRequest,"dialog-request",default);
            var channel=new Channel<TResult>();
            using(var linked=CancellationTokenSource.CreateLinkedTokenSource(cancellationToken,lifetime.Stopping))
            using(var caller=cancellationToken.Register(channel.CancelCaller))
            using(var stopping=lifetime.Stopping.Register(channel.ContextStop))
            {
                try
                {
                    IUISession session;
                    try{session=await context.OpenAsync(entry.Key,entry.Bind(request,channel),linked.Token);}
                    catch(OperationCanceledException)
                    {
                        await UniTask.SwitchToMainThread();
                        if(lifetime.Stopping.IsCancellationRequested)return new DialogResult<TResult>(DialogStatus.ContextStopped,default,null);
                        throw new OperationCanceledException(cancellationToken);
                    }
                    catch(UIException error)when(error.Code==UIErrorCode.ContextStopped&&lifetime.Stopping.IsCancellationRequested)
                    {return new DialogResult<TResult>(DialogStatus.ContextStopped,default,null);}
                    await UniTask.WhenAny(channel.Ready.Task,session.Closed.AsUniTask());
                    await UniTask.SwitchToMainThread();
                    if(!lifetime.Stopping.IsCancellationRequested&&channel.HasIntent&&session.State==UIState.Open)
                        await UIContextFacade.WaitForCallbackExitAsync(context);
                    UICompletion completion;
                    if(!lifetime.Stopping.IsCancellationRequested&&channel.HasIntent&&session.State==UIState.Open)
                        completion=await session.CloseAsync();
                    else completion=await session.Closed;
                    await UniTask.SwitchToMainThread();
                    if(lifetime.Stopping.IsCancellationRequested||completion.Reason==UICloseReason.ContextShutdown||completion.Reason==UICloseReason.RootDestroyed)
                        return new DialogResult<TResult>(DialogStatus.ContextStopped,default,completion);
                    // A responder intent is not publication: caller cancellation covers the Closed barrier too.
                    if(cancellationToken.IsCancellationRequested)throw new OperationCanceledException(cancellationToken);
                    return new DialogResult<TResult>(channel.Submitted?DialogStatus.Submitted:DialogStatus.UserCanceled,channel.Value,completion);
                }
                finally{channel.Invalidate();}
            }
        }
        sealed class Channel<T>:IDialogResponder<T>
        {
            readonly object gate=new object();
            internal readonly UniTaskCompletionSource Ready=new UniTaskCompletionSource();
            int intent;bool ended;T value;
            internal bool HasIntent{get{lock(gate)return intent!=0;}}
            internal bool Submitted{get{lock(gate)return intent==1;}}
            internal T Value{get{lock(gate)return value;}}
            bool Resolve(int kind,T result)
            {
                lock(gate){if(ended||intent!=0)return false;intent=kind;value=result;}
                Ready.TrySetResult();return true;
            }
            public bool TrySubmit(T result)
            {if(!PlayerLoopHelper.IsMainThread)return false;return Resolve(1,result);}
            public bool TryCancel()
            {if(!PlayerLoopHelper.IsMainThread)return false;return Resolve(2,default);}
            internal void CancelCaller()=>Resolve(3,default);
            internal void ContextStop(){Ready.TrySetResult();}
            internal void Invalidate(){lock(gate)ended=true;}
        }
    }
}
