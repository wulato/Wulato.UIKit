using System;
using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.EventSystems;
using Wulato.UIKit.Navigation;
using Wulato.UIKit.Dialogs;
using Wulato.UIKit.PopupQueue;
namespace Wulato.UIKit
{
    /// <summary>Explicit main-thread composition. Injected dependencies remain owned by the caller.</summary>
    public sealed class UIContextBuilder
    {
        readonly UIRoot root;
        readonly UIRegistry registry;
        UIContextSettings settings = new UIContextSettings();
        IUIViewLoader loader;
        IUIAsyncScheduler scheduler;
        IUITransition transition;
        IUILayoutReady layout;
        IUIErrorSink sink;
        IUIInputArbiter input;
        INavigationTransition navigationTransition;
        NavigationSettings navigationSettings;
        readonly List<IUISessionLifecycleObserver> observers = new List<IUISessionLifecycleObserver>();
        readonly List<KeyValuePair<Type,Func<IUIContext,object>>> factories = new List<KeyValuePair<Type,Func<IUIContext,object>>>();
        public UIContextBuilder(UIRoot root, UIRegistry registry)
        {this.root=root?root:throw new ArgumentNullException(nameof(root));this.registry=registry?registry:throw new ArgumentNullException(nameof(registry));}
        static T Required<T>(T value) where T:class => value??throw new ArgumentNullException(nameof(value));
        public UIContextBuilder WithSettings(UIContextSettings value){settings=Required(value);return this;}
        public UIContextBuilder WithLoader(IUIViewLoader value){loader=Required(value);return this;}
        public UIContextBuilder WithScheduler(IUIAsyncScheduler value){scheduler=Required(value);return this;}
        public UIContextBuilder WithTransition(IUITransition value){transition=Required(value);return this;}
        public UIContextBuilder WithLayoutReady(IUILayoutReady value){layout=Required(value);return this;}
        public UIContextBuilder WithErrorSink(IUIErrorSink value){sink=Required(value);return this;}
        public UIContextBuilder WithInputArbiter(IUIInputArbiter value){input=Required(value);return this;}
        public UIContextBuilder WithSessionObserver(IUISessionLifecycleObserver value){observers.Add(Required(value));return this;}
        public UIContextBuilder WithServiceFactory<TService>(Func<IUIContext,TService> factory) where TService:class
        {
            Required(factory);
            if(typeof(TService)==typeof(IUIContext)||typeof(TService)==typeof(IUINavigator)||typeof(TService)==typeof(IUIDialogs)||typeof(TService)==typeof(IUIPopupQueue)||typeof(TService)==typeof(IUIContextLifetime)||typeof(TService)==typeof(UIRegistrySnapshot)||typeof(TService)==typeof(IUITransactionAccess)||typeof(TService)==typeof(IUIPageTransactionAccess))
                throw new InvalidOperationException("Built-in service types are reserved.");
            if(factories.Exists(f=>f.Key==typeof(TService)))throw new InvalidOperationException("Duplicate service type.");
            factories.Add(new KeyValuePair<Type,Func<IUIContext,object>>(typeof(TService),ui=>factory(ui)));return this;
        }
        /// <summary>Configure the built-in Navigator's paired transition; the dependency remains caller-owned.</summary>
        public UIContextBuilder WithNavigationTransition(INavigationTransition value,NavigationSettings settings=null)
        {navigationTransition=value;navigationSettings=settings;return this;}
        public IUIContext Build()
        {
            if(!PlayerLoopHelper.IsMainThread)throw new InvalidOperationException("Build requires the main thread.");
            if(!root||!registry)throw new InvalidOperationException("Root and registry must remain alive.");
            UIContextFacade.AssertAvailable(root);root.ValidateConfiguration();
            var mapping=registry.CopyValidated();var config=settings.CopyValidated();
            var resources=loader??new UIResourceResolver(mapping);
            var events=EventSystem.current;
            if(!events)throw new InvalidOperationException("An EventSystem is required.");
            BaseInputModule module=null;
            foreach(var m in events.GetComponents<BaseInputModule>())if(m.enabled)
            {if(module!=null)throw new InvalidOperationException("Only one active UI input module is supported.");module=m;}
            if(!(module is IUIInputDriver))throw new InvalidOperationException("An explicitly installed UIKit input driver is required.");
#if ENABLE_INPUT_SYSTEM && !ENABLE_LEGACY_INPUT_MANAGER
            if(module is LegacyUIInputDriver)throw new InvalidOperationException("Input System projects require an explicitly installed Input System UIKit driver.");
#endif
            var services=new UIServiceRegistry();var facade=new UIContextFacade(root,services);
            UIInputDomain ownedDomain=null;UIInputArbiter ownedArbiter=null;UIViewHostFactory hosts=null;
            try
            {
                var arbiter=input;
                if(arbiter==null){ownedDomain=new UIInputDomain(events,module);ownedArbiter=new UIInputArbiter(ownedDomain);arbiter=ownedArbiter;}
                else if(!(arbiter is UIInputArbiter concrete)||!concrete.Matches(events))
                    throw new InvalidOperationException("The injected arbiter must own the current EventSystem domain.");
                facade.Claim();
                services.Values.Add(typeof(IUIContextLifetime),facade.Lifetime);
                services.Values.Add(typeof(UIRegistrySnapshot),mapping);
                var navigator=new UINavigator(facade,navigationTransition,navigationSettings);
                services.Add(typeof(IUINavigator),navigator);
                services.Add(typeof(IUIDialogs),new UIDialogs(facade));
                services.Add(typeof(IUIPopupQueue),new UIPopupQueue(facade));
                var allObservers=new List<IUISessionLifecycleObserver>(observers){navigator};
                foreach(var factory in factories.ToArray())
                {
                    var value=factory.Value(facade);services.Add(factory.Key,value);
                    if(value is IUISessionLifecycleObserver observer&&!allObservers.Contains(observer))allObservers.Add(observer);
                }
                hosts=new UIViewHostFactory(root);
                facade.Initialize(new UIContextEngine(facade.Id,new UIRuntimeDependencies(mapping,config,resources,
                    scheduler??new UniTaskUIAsyncScheduler(),hosts,layout??new UILayoutReady(),transition??new ImmediateUITransition(),
                    arbiter,sink??new UnityErrorSink(),allObservers,services.Values,enableTransactions:true,stopResources:facade.Release,lifetime:facade.Lifetime)),ownedDomain,ownedArbiter);
                return facade;
            }
            catch(Exception original)
            {
                var errors=new List<Exception>{original};
                facade.StopFailedBuild(errors.Add);
                try{hosts?.Dispose();}catch(Exception error){errors.Add(error);}
                services.Release(errors.Add);
                try{ownedArbiter?.Dispose();}catch(Exception error){errors.Add(error);}
                try{ownedDomain?.Dispose();}catch(Exception error){errors.Add(error);}
                facade.Rollback();
                if(errors.Count>1)throw new AggregateException(errors);
                throw;
            }
        }
        sealed class UnityErrorSink:IUIErrorSink { public void Report(UIException error)=>Debug.LogException(error); }
    }
}
