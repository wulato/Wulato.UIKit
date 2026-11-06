using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;
using Wulato.UIKit.Navigation;
using Wulato.UIKit.Dialogs;
using Wulato.UIKit.PopupQueue;
namespace Wulato.UIKit.Tests.Support
{
    /// <summary>Package-owned real core harness; no repository verification or optional backend dependencies.</summary>
    public sealed class UIFlowTestHarness : IDisposable,IUIViewLoader,IUILayoutReady,IUITransition,IUIErrorSink,IUISessionLifecycleObserver
    {
        public IUIContext UI { get; }
        public readonly FakeInputArbiter Input=new FakeInputArbiter();
        public readonly ManualScheduler Clock=new ManualScheduler();
        public readonly List<UIException> Errors=new List<UIException>();
        public IUISessionLifecycleObserver Observer;
        public int Creates,Releases;
        public UIViewBase LastView;
        readonly UIRegistrySnapshot registry;
        public UIFlowTestHarness(UIRegistrySnapshot registry,INavigationTransition pair=null)
        {
            this.registry=registry;
            var services=new UIServiceRegistry();var facade=new UIContextFacade(null,services);
            services.Values.Add(typeof(UIRegistrySnapshot),registry);services.Values.Add(typeof(IUIContextLifetime),facade.Lifetime);
            var navigator=new UINavigator(facade,pair);services.Add(typeof(IUINavigator),navigator);
            services.Add(typeof(IUIDialogs),new UIDialogs(facade));services.Add(typeof(IUIPopupQueue),new UIPopupQueue(facade));
            var engine=new UIContextEngine(facade.Id,new UIRuntimeDependencies(registry,new UIContextSettings().CopyValidated(),this,Clock,new FakeViewHostFactory(),this,this,Input,this,new IUISessionLifecycleObserver[]{this,navigator},services.Values,true,stopResources:services.Release,lifetime:facade.Lifetime));
            facade.Initialize(engine,null,null);UI=facade;
        }
        public UniTask<IUIViewLease> CreateAsync(UIViewAsset asset,Transform parent,CancellationToken ct)
        {
            ct.ThrowIfCancellationRequested();Type type=null;
            foreach(var entry in registry.Entries)if(entry.Asset.ResourceId==asset.ResourceId){type=entry.ViewType;break;}
            if(type==null)throw new ArgumentException("Unknown fixture asset.");
            var go=new GameObject("flow-view",typeof(RectTransform));go.SetActive(false);go.transform.SetParent(parent,false);
            LastView=(UIViewBase)go.AddComponent(type);go.SetActive(true);Creates++;
            return UniTask.FromResult<IUIViewLease>(new Lease(this,LastView));
        }
        sealed class Lease:IUIViewLease
        {
            UIFlowTestHarness owner;public UIViewBase View{get;}
            public Lease(UIFlowTestHarness owner,UIViewBase view){this.owner=owner;View=view;}
            public void Dispose(){if(owner==null)return;owner.Releases++;owner=null;UnityEngine.Object.DestroyImmediate(View.gameObject);}
        }
        public UniTask WaitLayoutAsync(UIViewBase view,CancellationToken ct){ct.ThrowIfCancellationRequested();return UniTask.CompletedTask;}
        public UITransitionWrites Writes=>UITransitionWrites.None;
        public UniTask EnterAsync(UITransitionContext context,CancellationToken ct)=>UniTask.CompletedTask;
        public UniTask ExitAsync(UITransitionContext context,CancellationToken ct)=>UniTask.CompletedTask;
        public void Stop(UITransitionContext context){}
        public void Reset(UITransitionContext context){}
        public void Report(UIException error)=>Errors.Add(error);
        public IDisposable OnPreparing(UIViewBase view,UIArguments args,UISessionScope session)=>Observer?.OnPreparing(view,args,session)??new ActionResource(()=>{});
        public void OnTerminating(UISessionScope session,UICloseReason reason)=>Observer?.OnTerminating(session,reason);
        public void OnTerminated(UICompletion completion)=>Observer?.OnTerminated(completion);
        public void Dispose(){UI.ShutdownAsync().GetAwaiter().GetResult();}
    }
}
