using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
namespace Wulato.UIKit.Navigation
{
    /// <summary>FIFO page flow implemented inside the owning Context transaction queue.</summary>
    public sealed class UINavigator : IUINavigator,IUISessionLifecycleObserver,IDisposable
    {
        readonly IUIContext context;
        readonly UIRegistrySnapshot registry;
        readonly INavigationTransition transition;
        readonly TimeSpan timeout;
        readonly CancellationTokenRegistration stopping;
        IReadOnlyList<UIPageDescriptor> history=Array.AsReadOnly(Array.Empty<UIPageDescriptor>());
        IUISession current;
        bool stopped;
        /// <inheritdoc/>
        public IReadOnlyList<UIPageDescriptor> History=>history;
        /// <inheritdoc/>
        public UINavigator(IUIContext context,INavigationTransition transition=null,NavigationSettings settings=null)
        {
            this.context=context??throw new ArgumentNullException(nameof(context));
            registry=context.GetService<UIRegistrySnapshot>();this.transition=transition??new ImmediateNavigationTransition();
            timeout=(settings??new NavigationSettings()).PairTransitionTimeout;
            if(timeout<=TimeSpan.Zero)throw new ArgumentOutOfRangeException(nameof(settings));
            stopping=context.GetService<IUIContextLifetime>().Stopping.Register(()=>stopped=true);
        }
        internal UIViewTypeKey Key(Type type)
        {foreach(var entry in registry.Entries)if(entry.ViewType==type)return entry.Key;throw new UIException(UIErrorCode.InvalidRequest,"navigation-key",default);}
        /// <inheritdoc/>
        public async UniTask<IUISession> PushAsync<TView>(CancellationToken cancellationToken=default) where TView:UIView
            =>await PushAsync(Key(typeof(TView)),UIArguments.None,cancellationToken);
        /// <inheritdoc/>
        public async UniTask<IUISession> PushAsync<TView,TArgs>(TArgs arguments,CancellationToken cancellationToken=default) where TView:UIView<TArgs>
            =>await PushAsync(Key(typeof(TView)),UIArguments.From(arguments),cancellationToken);
        internal UniTask<IUISession> OpenRootAsync(UIViewTypeKey type,UIArguments arguments,CancellationToken token=default)
            =>Navigate(new UIPageDescriptor(type,arguments),Move.Root,token);
        /// <inheritdoc/>
        public UniTask<IUISession> PushAsync(UIViewTypeKey type,UIArguments arguments,CancellationToken cancellationToken=default)
            =>Navigate(new UIPageDescriptor(type,arguments),Move.Push,cancellationToken);
        /// <inheritdoc/>
        public async UniTask<IUISession> ReplaceAsync<TView>(CancellationToken token=default) where TView:UIView
            =>await ReplaceAsync(Key(typeof(TView)),UIArguments.None,token);
        /// <inheritdoc/>
        public async UniTask<IUISession> ReplaceAsync<TView,TArgs>(TArgs args,CancellationToken token=default) where TView:UIView<TArgs>
            =>await ReplaceAsync(Key(typeof(TView)),UIArguments.From(args),token);
        /// <inheritdoc/>
        public UniTask<IUISession> ReplaceAsync(UIViewTypeKey type,UIArguments args,CancellationToken token=default)
            =>Navigate(new UIPageDescriptor(type,args),Move.Replace,token);
        /// <inheritdoc/>
        public async UniTask<bool> PopToAsync<TView>(CancellationToken token=default) where TView:UIViewBase
            =>await Navigate(null,Move.PopTo,token,typeof(TView))!=null;
        /// <inheritdoc/>
        public async UniTask<bool> PopToAsync(UIViewTypeKey type,CancellationToken token=default)
            =>await Navigate(new UIPageDescriptor(type,UIArguments.None),Move.PopTo,token)!=null;
        /// <inheritdoc/>
        public async UniTask<bool> BackAsync(CancellationToken cancellationToken=default)
            =>await Navigate(null,Move.Back,cancellationToken)!=null;
        internal async UniTask<bool> BackFromInputAsync(UISessionId source)=>await Navigate(null,Move.Back,CancellationToken.None,inputSource:source)!=null;
        enum Move { Root,Push,Replace,Back,PopTo }
        async UniTask<IUISession> Navigate(UIPageDescriptor page,Move move,CancellationToken caller,Type popType=null,UISessionId? inputSource=null)
        {
            if(stopped)throw new UIException(UIErrorCode.ContextStopped,"navigation",context.ContextId);
            IUISession result=null;
            await context.GetService<IUIPageTransactionAccess>().ExecuteAsync(async(steps,ct)=>
            {
                if(stopped)throw new UIException(UIErrorCode.ContextStopped,"navigation",context.ContextId);
                // Every history read and proposed edit belongs to this FIFO turn.
                int destination=-1;
                if(move==Move.Back)
                {
                    var backTarget=await steps.RouteBackAsync(current,history.Count>1,inputSource);
                    if(backTarget==null)return;
                    if(!ReferenceEquals(backTarget,current)){result=backTarget;return;}
                    destination=history.Count-2;
                }
                else if(move==Move.PopTo)
                {
                    bool Matches(UIPageDescriptor item)=>popType!=null?registry.Get(item.Type).ViewType==popType:item.Type==page.Type;
                    if(history.Count==0||Matches(history[history.Count-1]))return;
                    for(int i=history.Count-2;i>=0;i--)if(Matches(history[i])){destination=i;break;}
                    if(destination<0)return;
                }
                var target=destination>=0?history[destination]:page;
                UIRegistration registration;
                try{registration=registry.Get(target.Type);}
                catch(ArgumentException error){throw new UIException(UIErrorCode.InvalidRequest,"navigation-key",context.ContextId,null,error);}
                if(registration.Layer!=UILayer.Page||!registration.Accepts(target.Arguments))
                    throw new UIException(UIErrorCode.InvalidRequest,"navigation-arguments",context.ContextId);
                var nextHistory=move==Move.Root?new List<UIPageDescriptor>():new List<UIPageDescriptor>(history);
                if(destination>=0)nextHistory.RemoveRange(destination+1,nextHistory.Count-destination-1);
                else if(move==Move.Replace&&nextHistory.Count>0)nextHistory[nextHistory.Count-1]=target;
                else nextHistory.Add(target);
                var old=current;
                var next=await steps.PrepareAsync(target.Type,target.Arguments,UITransitionOwner.External,ct);
                await steps.AnimatePairAsync(old,next,transition,ct);
                steps.CommitReplacement(old,next,new Publication(this,next,Array.AsReadOnly(nextHistory.ToArray())));
                result=next;
                if(old!=null&&old.State==UIState.Open)
                    await steps.CloseAsync(old,UITransitionOwner.External,destination>=0?UICloseReason.Back:UICloseReason.Owner);
            },timeout,caller);
            return result;
        }
        sealed class Publication : IUITransactionParticipant
        {
            readonly UINavigator navigator;readonly IUISession next,old;
            readonly IReadOnlyList<UIPageDescriptor> value,previous;
            public Publication(UINavigator navigator,IUISession next,IReadOnlyList<UIPageDescriptor> value)
            {this.navigator=navigator;this.next=next;this.value=value;old=navigator.current;previous=navigator.history;}
            public void Prepare(){if(navigator.stopped)throw new InvalidOperationException("Navigator is stopped.");}
            public void Commit(){navigator.current=next;navigator.history=value;}
            public void Rollback(){navigator.current=old;navigator.history=previous;}
        }
        /// <inheritdoc/>
        public IDisposable OnPreparing(UIViewBase view,UIArguments args,UISessionScope session)=>new Binding();
        /// <inheritdoc/>
        public void OnTerminating(UISessionScope session,UICloseReason reason){}
        /// <inheritdoc/>
        public void OnTerminated(UICompletion completion)
        {
            if(current!=null&&current.Id==completion.SessionId)
            {current=null;history=Array.AsReadOnly(Array.Empty<UIPageDescriptor>());}
        }
        /// <inheritdoc/>
        public void Dispose(){stopped=true;stopping.Dispose();current=null;history=Array.AsReadOnly(Array.Empty<UIPageDescriptor>());}
        sealed class Binding:IDisposable{public void Dispose(){}}
    }
    /// <summary>No-op paired transition; lifecycle and input gates still run.</summary>
    public sealed class ImmediateNavigationTransition : INavigationTransition
    {
        /// <inheritdoc/>
        public UITransitionWrites FromWrites=>UITransitionWrites.None;
        /// <inheritdoc/>
        public UITransitionWrites ToWrites=>UITransitionWrites.None;
        /// <inheritdoc/>
        public UniTask PlayAsync(UINavigationTransitionContext context,CancellationToken cancellationToken)
        {cancellationToken.ThrowIfCancellationRequested();return UniTask.CompletedTask;}
        /// <inheritdoc/>
        public void Stop(UINavigationTransitionContext context){}
        /// <inheritdoc/>
        public void Reset(UINavigationTransitionContext context){}
    }
}
