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
        /// <inheritdoc/>
        public UniTask<IUISession> PushAsync(UIViewTypeKey type,UIArguments arguments,CancellationToken cancellationToken=default)
            =>Navigate(new UIPageDescriptor(type,arguments),false,cancellationToken);
        /// <inheritdoc/>
        public async UniTask<bool> BackAsync(CancellationToken cancellationToken=default)
            =>await Navigate(null,true,cancellationToken)!=null;
        async UniTask<IUISession> Navigate(UIPageDescriptor page,bool back,CancellationToken caller)
        {
            if(stopped)throw new UIException(UIErrorCode.ContextStopped,"navigation",default);
            IUISession result=null;
            await context.GetService<IUIPageTransactionAccess>().ExecuteAsync(async(steps,ct)=>
            {
                if(stopped)throw new UIException(UIErrorCode.ContextStopped,"navigation",default);
                if(back&&history.Count<2)return;
                var target=back?history[history.Count-2]:page;
                UIRegistration registration;
                try{registration=registry.Get(target.Type);}
                catch(ArgumentException error){throw new UIException(UIErrorCode.InvalidRequest,"navigation-key",context.GetDiagnostics().ContextId,null,error);}
                if(registration.Layer!=UILayer.Page||!registration.Accepts(target.Arguments))
                    throw new UIException(UIErrorCode.InvalidRequest,"navigation-arguments",default);
                var nextHistory=new List<UIPageDescriptor>(history);
                if(back)nextHistory.RemoveAt(nextHistory.Count-1);else nextHistory.Add(target);
                var old=current;
                var next=await steps.PrepareAsync(target.Type,target.Arguments,UITransitionOwner.External,ct);
                await steps.AnimatePairAsync(old,next,transition,ct);
                steps.CommitReplacement(old,next,new Publication(this,next,Array.AsReadOnly(nextHistory.ToArray())));
                result=next;
                if(old!=null&&old.State==UIState.Open)await steps.CloseAsync(old,UITransitionOwner.External,back?UICloseReason.Back:UICloseReason.Owner);
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
