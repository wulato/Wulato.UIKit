using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;
using Wulato.UIKit.Navigation;
namespace Wulato.UIKit
{
    internal sealed class UIContextFacade : IUIContext
    {
        static readonly Dictionary<UIRoot,UIContextFacade> roots=new Dictionary<UIRoot,UIContextFacade>();
        readonly UIRoot root;
        readonly UIServiceRegistry services;
        internal readonly UIContextLifetime Lifetime=new UIContextLifetime();
        UIContextEngine engine;
        UIInputDomain domain;
        UIInputArbiter arbiter;
        bool released;
        internal readonly UIContextId Id=new UIContextId(Guid.NewGuid());
        internal UIContextFacade(UIRoot root,UIServiceRegistry services){this.root=root;this.services=services;}
        internal static void AssertAvailable(UIRoot root){if(roots.ContainsKey(root))throw new InvalidOperationException("The root already has a live Context.");}
        internal void Claim(){AssertAvailable(root);roots.Add(root,this);}
        internal void Initialize(UIContextEngine engine,UIInputDomain domain,UIInputArbiter arbiter){if(engine==null||!engine.ContextId.Equals(Id))throw new InvalidOperationException("Engine and facade Context identities must match.");this.engine=engine;this.domain=domain;this.arbiter=arbiter;}
        internal void StopFailedBuild(Action<Exception> error) { released=true;try { Lifetime.Source.Cancel(); } catch(Exception e) { error(e); } }
        internal void Rollback(){if(roots.TryGetValue(root,out var value)&&value==this)roots.Remove(root);released=true;}
        internal void Release(Action<Exception> error)
        {
            if(released)return;released=true;
            services.Release(error);
            try {arbiter?.Dispose();}catch(Exception e){error(e);}
            try {domain?.Dispose();}catch(Exception e){error(e);}
            if(roots.TryGetValue(root,out var value)&&value==this)roots.Remove(root);
        }
        internal static int ActiveCount=>roots.Count;
        // Teardown live owners before removing the bridge; never reset unresolved ownership counters.
        internal static void StopPlaySession()
        {
            foreach(var context in new List<UIContextFacade>(roots.Values))
                if(context.engine!=null)context.engine.ShutdownAsync().Forget();
        }
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void Reset()=>StopPlaySession();
        public UIContextId ContextId=>Id;
        public UniTask<UICompletion> CloseAsync(IUISession session,UICloseReason reason=UICloseReason.Owner)=>Ready().CloseAsync(session,reason);
        UIContextEngine Ready()=>engine??throw new InvalidOperationException("Only previously created service lookup is allowed during Build.");
        UINavigator Navigator()=>(UINavigator)GetService<IUINavigator>();
        UIViewTypeKey Key(Type type)
        {
            foreach(var entry in GetService<UIRegistrySnapshot>().Entries)if(entry.ViewType==type)return entry.Key;
            throw new UIException(UIErrorCode.InvalidRequest,"registration",Id);
        }
        public UniTask<IUISession<TView>> OpenAsync<TView>(CancellationToken token=default) where TView:UIView
        {Ready();return OpenTyped<TView>(UIArguments.None,token);}
        public UniTask<IUISession<TView>> OpenAsync<TView,TArgs>(TArgs args,CancellationToken token=default) where TView:UIView<TArgs>
        {Ready();return OpenTyped<TView>(UIArguments.From(args),token);}
        async UniTask<IUISession<TView>> OpenTyped<TView>(UIArguments args,CancellationToken token) where TView:UIViewBase
            =>await OpenAsync(Key(typeof(TView)),args,token).AsTyped<TView>();
        public UniTask<IUISession> OpenAsync(UIViewTypeKey type,UIArguments args,CancellationToken token=default)
        {
            var commands=Ready();
            try
            {
                var entry=GetService<UIRegistrySnapshot>().Get(type);
                return entry.Layer==UILayer.Page?Navigator().OpenRootAsync(type,args,token):commands.OpenAsync(type,args,token);
            }
            catch(ArgumentException error){return UniTask.FromException<IUISession>(new UIException(UIErrorCode.InvalidRequest,"registration",Id,null,error));}
            catch(Exception error){return UniTask.FromException<IUISession>(error);}
        }
        UniTask<T> Navigate<T>(Func<UINavigator,UniTask<T>> command)
        {
            Ready();
            try{return command(Navigator());}catch(Exception error){return UniTask.FromException<T>(error);}
        }
        public UniTask<IUISession> PushAsync<TView>(CancellationToken token=default) where TView:UIView=>Navigate(nav=>nav.PushAsync<TView>(token));
        public UniTask<IUISession> PushAsync<TView,TArgs>(TArgs args,CancellationToken token=default) where TView:UIView<TArgs> =>Navigate(nav=>nav.PushAsync<TView,TArgs>(args,token));
        public UniTask<IUISession> PushAsync(UIViewTypeKey type,UIArguments args,CancellationToken token=default)=>Navigate(nav=>nav.PushAsync(type,args,token));
        public UniTask<IUISession> ReplaceAsync<TView>(CancellationToken token=default) where TView:UIView=>Navigate(nav=>nav.ReplaceAsync<TView>(token));
        public UniTask<IUISession> ReplaceAsync<TView,TArgs>(TArgs args,CancellationToken token=default) where TView:UIView<TArgs> =>Navigate(nav=>nav.ReplaceAsync<TView,TArgs>(args,token));
        public UniTask<IUISession> ReplaceAsync(UIViewTypeKey type,UIArguments args,CancellationToken token=default)=>Navigate(nav=>nav.ReplaceAsync(type,args,token));
        public UniTask<bool> PopToAsync<TView>(CancellationToken token=default) where TView:UIViewBase=>Navigate(nav=>nav.PopToAsync<TView>(token));
        public UniTask<bool> PopToAsync(UIViewTypeKey type,CancellationToken token=default)=>Navigate(nav=>nav.PopToAsync(type,token));
        public UniTask<bool> BackAsync(CancellationToken token=default)=>Navigate(nav=>nav.BackAsync(token));
        public UniTask<UIBatchCompletion> CloseAllAsync(UICloseReason reason=UICloseReason.Owner)=>Ready().CloseAllAsync(reason);
        public UniTask<UIBatchCompletion> CloseAllOfTypeAsync<TView>(UICloseReason reason=UICloseReason.Owner) where TView:UIViewBase =>Ready().CloseAllOfTypeAsync<TView>(reason);
        public UniTask ClearCacheAsync()=>Ready().ClearCacheAsync();
        public UniTask<UIShutdownCompletion> ShutdownAsync()=>Ready().ShutdownAsync();
        public TService GetService<TService>() where TService:class
        {
            if(!PlayerLoopHelper.IsMainThread)throw new UIException(UIErrorCode.InvalidRequest,"service-main-thread",Id);
            if(engine!=null)return engine.GetService<TService>();
            if(released)throw new UIException(UIErrorCode.ContextStopped,"service",Id);
            if(services.Values.TryGetValue(typeof(TService),out var value))return (TService)value;
            throw new InvalidOperationException("Service is not registered.");
        }
        public UIDiagnosticsSnapshot GetDiagnostics()=>Ready().GetDiagnostics();
    }
}
