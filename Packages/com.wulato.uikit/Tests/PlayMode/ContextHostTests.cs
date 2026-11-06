using System;
using System.Collections;
using System.IO;
using System.Reflection;
using Cysharp.Threading.Tasks;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
namespace Wulato.UIKit.Tests.PlayMode
{
    public sealed class HostSinglePreset:UITransitionPreset
    {
        public int Creates;
        public override IUITransition CreateTransition(){Creates++;return new ImmediateUITransition();}
    }
    public sealed class HostPairPreset:UINavigationTransitionPreset
    {
        public int Creates;
        sealed class Pair:INavigationTransition
        {
            public UITransitionWrites FromWrites=>UITransitionWrites.None;public UITransitionWrites ToWrites=>UITransitionWrites.None;
            public UniTask PlayAsync(UINavigationTransitionContext context,System.Threading.CancellationToken token)=>UniTask.CompletedTask;
            public void Stop(UINavigationTransitionContext context){}public void Reset(UINavigationTransitionContext context){}
        }
        public override INavigationTransition CreateTransition(){Creates++;return new Pair();}
    }
    [Category("context_host")]
    public class ContextHostTests
    {
        GameObject root,events; UIRegistry registry; Component host;
        Type HostType => typeof(UIContextBuilder).Assembly.GetType("Wulato.UIKit.UIContextHost");
        [SetUp] public void Setup()
        {
            Assert.That(HostType,Is.Not.Null,"UIContextHost is not implemented");
            root=new GameObject("HostTestRoot",typeof(RectTransform),typeof(UIRoot));root.GetComponent<Canvas>().renderMode=RenderMode.ScreenSpaceOverlay;
            events=IntegrationResetProbe.CreateEvents();registry=ScriptableObject.CreateInstance<UIRegistry>();host=root.AddComponent(HostType);
            Set("root",root.GetComponent<UIRoot>());Set("registry",registry);Set("initializeOnStart",false);
        }
        void Set(string field,object value)=>HostType.GetField(field,BindingFlags.Instance|BindingFlags.NonPublic).SetValue(host,value);
        IUIContext Init(Action<UIContextBuilder> configure=null)
        {
            try{return (IUIContext)HostType.GetMethod("Initialize").Invoke(host,new object[]{configure});}catch(TargetInvocationException e){throw e.InnerException;}
        }
        UniTask<UIShutdownCompletion> Stop()=>(UniTask<UIShutdownCompletion>)HostType.GetMethod("ShutdownAsync").Invoke(host,null);
        IUIContext Context=>(IUIContext)HostType.GetProperty("UI").GetValue(host);
        [UnityTest] public IEnumerator Presets_CreatePerHostOwnersOnceBeforeConfigure()=>UniTask.ToCoroutine(async()=>{
            var single=ScriptableObject.CreateInstance<HostSinglePreset>();var pair=ScriptableObject.CreateInstance<HostPairPreset>();
            try{Set("transitionPreset",single);Set("navigationPreset",pair);var ui=Init(b=>{Assert.That(single.Creates,Is.EqualTo(1));Assert.That(pair.Creates,Is.EqualTo(1));b.WithTransition(new ImmediateUITransition());});
                Assert.That(Init(),Is.SameAs(ui));Assert.That(single.Creates,Is.EqualTo(1));Assert.That(pair.Creates,Is.EqualTo(1));await Stop();}
            finally{UnityEngine.Object.DestroyImmediate(single);UnityEngine.Object.DestroyImmediate(pair);}
        });
        [UnityTest] public IEnumerator ShutdownDuringInitialization_DoesNotPublishLiveContext()=>UniTask.ToCoroutine(async()=>{
            UniTask<UIShutdownCompletion> stopping=default;
            Assert.Throws<InvalidOperationException>(()=>Init(b=>{stopping=Stop();}));
            await stopping;Assert.That(Context,Is.Null);UnityEngine.Object.DestroyImmediate(host);
            host=root.AddComponent(HostType);Set("root",root.GetComponent<UIRoot>());Set("registry",registry);Set("initializeOnStart",false);
            var resource=new Resource();
            Assert.Throws<InvalidOperationException>(()=>Init(b=>b.WithServiceFactory(_=>{stopping=Stop();return resource;})));
            await stopping;Assert.That(Context.GetDiagnostics().State,Is.EqualTo(UIContextState.Stopped));Assert.That(resource.Releases,Is.EqualTo(1));
        });
        [Test] public void AutomaticBuildFailure_RequiresExplicitRetry()
        {
            Set("initializeOnStart",true);Set("registry",null);
            LogAssert.Expect(LogType.Exception,new System.Text.RegularExpressions.Regex("ArgumentNullException: Value cannot be null"));
            var method=HostType.GetMethod("StartAutomatically",BindingFlags.Instance|BindingFlags.NonPublic);
            method.Invoke(host,null);method.Invoke(host,null);Assert.That(Context,Is.Null);
            Set("registry",registry);Assert.That(Init(),Is.Not.Null,"Only an explicit repaired initialization may retry.");
        }
        [UnityTest] public IEnumerator Initialize_IsIdempotent_AndRejectsSecondConfiguration()=>UniTask.ToCoroutine(async()=>{
            var ui=Init();Assert.That(Init(),Is.SameAs(ui));Assert.Throws<InvalidOperationException>(()=>Init(_=>{}));
            var a=Stop();var b=Stop();Assert.That(await a,Is.SameAs(await b));Assert.Throws<InvalidOperationException>(()=>Init());
        });
        [UnityTest] public IEnumerator Start_AutomaticallyBuilds_AndDisableDoesNotStop()=>UniTask.ToCoroutine(async()=>{
            UnityEngine.Object.DestroyImmediate(host);host=root.AddComponent(HostType);
            Set("root",root.GetComponent<UIRoot>());Set("registry",registry);Set("initializeOnStart",true);await UniTask.DelayFrame(2);Assert.That(Context,Is.Not.Null);
            var ui=Context;((Behaviour)host).enabled=false;await UniTask.NextFrame();Assert.That(ui.GetDiagnostics().State,Is.EqualTo(UIContextState.Running));
            ((Behaviour)host).enabled=true;Assert.That(Init(),Is.SameAs(ui));await Stop();
        });
        [UnityTest] public IEnumerator ShutdownBeforeInitialize_IsTerminal()=>UniTask.ToCoroutine(async()=>{
            var result=await Stop();Assert.That(result.PendingCleanupCount,Is.Zero);Assert.That(Context,Is.Null);Assert.Throws<InvalidOperationException>(()=>Init());
        });
        [UnityTest] public IEnumerator FailedBuild_CanRetry_AndDoesNotOwnExternalContext()=>UniTask.ToCoroutine(async()=>{
            Set("registry",null);Assert.Throws<ArgumentNullException>(()=>Init());Set("registry",registry);
            var external=new UIContextBuilder(root.GetComponent<UIRoot>(),registry).Build();
            Assert.Throws<InvalidOperationException>(()=>Init());await Stop();Assert.That(external.GetDiagnostics().State,Is.EqualTo(UIContextState.Running));await external.ShutdownAsync();
        });
        [UnityTest] public IEnumerator FailedConfigure_CanRetry_AndFactoryResourcesReleaseOnce()=>UniTask.ToCoroutine(async()=>{
            Assert.Throws<ArgumentException>(()=>Init(_=>throw new ArgumentException("configure")));
            var service=new Resource();var loader=new SharedLoader();var ui=Init(b=>b.WithLoader(loader).WithServiceFactory(_=>service));Assert.That(ui.GetService<Resource>(),Is.SameAs(service));await Stop();await Stop();Assert.That(service.Releases,Is.EqualTo(1));Assert.That(loader.Releases,Is.Zero);loader.Dispose();
        });
        [UnityTest] public IEnumerator DestroyingHost_ShutsDownOwnedContext()=>UniTask.ToCoroutine(async()=>{
            var ui=Init();UnityEngine.Object.Destroy(host);await UniTask.NextFrame();await ui.ShutdownAsync();Assert.That(ui.GetDiagnostics().State,Is.EqualTo(UIContextState.Stopped));Assert.That(ui.GetDiagnostics().InputLockCount,Is.Zero);
        });
        [UnityTest] public IEnumerator DestroyDuringLoad_CancelsOpen_AndReleasesLateLease()=>UniTask.ToCoroutine(async()=>{
            var provider=ScriptableObject.CreateInstance<IntegrationRegistry>();
            typeof(UIRegistry).GetField("providers",BindingFlags.Instance|BindingFlags.NonPublic).SetValue(registry,new System.Collections.Generic.List<UIRegistryProvider>{provider});
            try
            {
                foreach(bool destroyRoot in new[]{false,true})
                {
                    if(host==null){host=root.AddComponent(HostType);Set("root",root.GetComponent<UIRoot>());Set("registry",registry);Set("initializeOnStart",false);}
                    var loader=new PendingLoader();var ui=Init(b=>b.WithLoader(loader));var opening=ui.OpenAsync<Wulato.UIKit.Tests.Support.T4ProbeView>();
                    if(destroyRoot)UnityEngine.Object.Destroy(root);else UnityEngine.Object.Destroy(host);await UniTask.NextFrame();
                    Exception failure=null;try{await opening;}catch(Exception e){failure=e;}Assert.That(failure,Is.Not.Null);Assert.That(loader.Token.IsCancellationRequested,Is.True);
                    var result=await ui.ShutdownAsync();Assert.That(result.PendingCleanupCount,Is.EqualTo(1));Assert.That(ui.GetDiagnostics().InputLockCount,Is.Zero);
                    var lease=new LateLease();Assert.That(loader.Pending.TrySetResult(lease),Is.True);await UniTask.NextFrame();Assert.That(lease.Releases,Is.EqualTo(1));Assert.That(loader.Disposes,Is.Zero);
                }
            }
            finally{UnityEngine.Object.Destroy(provider);}
        });
        sealed class PendingLoader:IUIViewLoader,IDisposable
        {
            public readonly UniTaskCompletionSource<IUIViewLease> Pending=new UniTaskCompletionSource<IUIViewLease>();
            public System.Threading.CancellationToken Token;public int Disposes;
            public UniTask<IUIViewLease> CreateAsync(UIViewAsset asset,Transform parent,System.Threading.CancellationToken token){Token=token;return Pending.Task;}
            public void Dispose()=>Disposes++;
        }
        sealed class LateLease:IUIViewLease
        {public int Releases;public UIViewBase View=>null;public void Dispose()=>Releases++;}
        sealed class SharedLoader:IUIViewLoader,IDisposable
        {
            public int Releases;
            public UniTask<IUIViewLease> CreateAsync(UIViewAsset asset,Transform parent,System.Threading.CancellationToken token)=>throw new NotSupportedException();
            public void Dispose()=>Releases++;
        }
        [UnityTest] public IEnumerator Initialize_RejectsWorkerThread()=>UniTask.ToCoroutine(async()=>{
            Exception failure=null;await UniTask.RunOnThreadPool(()=>{try{Init();}catch(Exception e){failure=e;}});
            await UniTask.SwitchToMainThread();Assert.That(failure,Is.TypeOf<InvalidOperationException>());Assert.That(Context,Is.Null);
        });
        sealed class Resource:IDisposable {public int Releases;public void Dispose()=>Releases++;}
        [UnityTearDown] public IEnumerator Cleanup()=>UniTask.ToCoroutine(async()=>{
            if(host!=null && Context!=null)await Stop();
            if(root)UnityEngine.Object.Destroy(root);if(events)UnityEngine.Object.Destroy(events);if(registry)UnityEngine.Object.Destroy(registry);
            var dir=Environment.GetEnvironmentVariable("UIKIT_EVIDENCE_DIR");if(!string.IsNullOrEmpty(dir))File.WriteAllText(Path.Combine(dir,TestContext.CurrentContext.Test.Name+".trace.json"),"{\"events\":[\"host-test-cleanup\"]}");
            await UniTask.NextFrame();
        });
    }
}
