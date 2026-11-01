using System;
using System.Collections;
using System.IO;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.TestTools;
using Cysharp.Threading.Tasks;
namespace Wulato.UIKit.Tests.PlayMode
{
    [Category("integration")]
    public class RuntimeResetTests
    {
        [UnityTest] public IEnumerator TwoPlaySessions_NoOldLocksOrSubscriptions()
        {
            for(int i=0;i<2;i++) { IntegrationResetProbe.Enter();IntegrationResetProbe.Exit();yield return null; }
            Assert.That(UIContextFacade.ActiveCount,Is.Zero);
            var dir=Environment.GetEnvironmentVariable("UIKIT_EVIDENCE_DIR");if(!string.IsNullOrEmpty(dir))File.WriteAllText(Path.Combine(dir,"TwoPlaySessions_NoOldLocksOrSubscriptions.trace.json"),"{\"events\":[\"two owner lifetimes released\"]}");
        }

        sealed class Observer:IUISessionLifecycleObserver,IDisposable
        {
            internal readonly System.Collections.Generic.List<string> Events=new System.Collections.Generic.List<string>();
            sealed class Binding:IDisposable {readonly Action release;internal Binding(Action release){this.release=release;}public void Dispose()=>release();}
            public IDisposable OnPreparing(UIViewBase view,UIArguments args,UISessionScope session){Events.Add("preparing");return new Binding(()=>Events.Add("binding-disposed"));}
            public void OnTerminating(UISessionScope session,UICloseReason reason)=>Events.Add("terminating");
            public void OnTerminated(UICompletion completion)=>Events.Add("terminated");
            public void Dispose()=>Events.Add("service-disposed");
        }
        [UnityTest] public IEnumerator FactoryObserver_BindsSession_AndDisposesAfterTermination()=>UniTask.ToCoroutine(async()=>
        {
            var root=new GameObject("Root",typeof(RectTransform),typeof(UIRoot));root.GetComponent<Canvas>().renderMode=RenderMode.ScreenSpaceOverlay;
            var events=IntegrationResetProbe.CreateEvents();var registry=ScriptableObject.CreateInstance<UIRegistry>();var provider=ScriptableObject.CreateInstance<IntegrationRegistry>();
            typeof(UIRegistry).GetField("providers",System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic).SetValue(registry,new System.Collections.Generic.List<UIRegistryProvider>{provider});
            IUIContext ui=null;var observer=new Observer();
            try {
                ui=new UIContextBuilder(root.GetComponent<UIRoot>(),registry).WithServiceFactory(_=>observer).Build();
                Assert.That(ui.GetService<Observer>(),Is.SameAs(observer));await ui.OpenAsync<Wulato.UIKit.Tests.Support.T4ProbeView>();await ui.ShutdownAsync();
                Assert.That(observer.Events,Is.EqualTo(new[]{"preparing","terminating","binding-disposed","terminated","service-disposed"}));
                var dir=Environment.GetEnvironmentVariable("UIKIT_EVIDENCE_DIR");if(!string.IsNullOrEmpty(dir))File.WriteAllText(Path.Combine(dir,"FactoryObserver_BindsSession_AndDisposesAfterTermination.trace.json"),"{\"events\":[\"session binding terminated before service disposal\"]}");
            } finally {if(ui!=null)await ui.ShutdownAsync();UnityEngine.Object.Destroy(root);UnityEngine.Object.Destroy(events);UnityEngine.Object.Destroy(registry);UnityEngine.Object.Destroy(provider);}
        });
        [UnityTest] public IEnumerator FailedDefaultBuild_PreservesOwner_AndExplicitSharingTransfersForeground()=>UniTask.ToCoroutine(async()=>
        {
            var a=new GameObject("First",typeof(RectTransform),typeof(UIRoot));a.GetComponent<Canvas>().renderMode=RenderMode.ScreenSpaceOverlay;
            var b=new GameObject("Second",typeof(RectTransform),typeof(UIRoot));b.GetComponent<Canvas>().renderMode=RenderMode.ScreenSpaceOverlay;
            var events=IntegrationResetProbe.CreateEvents();var registry=ScriptableObject.CreateInstance<UIRegistry>();var provider=ScriptableObject.CreateInstance<IntegrationRegistry>();
            typeof(UIRegistry).GetField("providers",System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic).SetValue(registry,new System.Collections.Generic.List<UIRegistryProvider>{provider});
            IUIContext first=null,second=null;
            try {
                first=new UIContextBuilder(a.GetComponent<UIRoot>(),registry).Build();var one=await first.OpenAsync<Wulato.UIKit.Tests.Support.T4ProbeView>();
                Assert.Throws<InvalidOperationException>(()=>new UIContextBuilder(b.GetComponent<UIRoot>(),registry).Build());Assert.That(b.transform.childCount,Is.Zero);
                Assert.That(first.GetDiagnostics().InputOwner.Value.SessionId,Is.EqualTo(one.Id));
                var arbiter=events.GetComponent<UIInputDomainRegistration>().Domain.Arbiter;
                second=new UIContextBuilder(b.GetComponent<UIRoot>(),registry).WithInputArbiter(arbiter).Build();var two=await second.OpenAsync<Wulato.UIKit.Tests.Support.T4ProbeView>();
                Assert.That(first.GetDiagnostics().InputOwner,Is.Null);Assert.That(second.GetDiagnostics().InputOwner.Value.SessionId,Is.EqualTo(two.Id));
                await second.ShutdownAsync();Assert.That(first.GetDiagnostics().InputOwner.Value.SessionId,Is.EqualTo(one.Id));
                await one.CloseAsync();var reopened=await first.OpenAsync<Wulato.UIKit.Tests.Support.T4ProbeView>();Assert.That(reopened.State,Is.EqualTo(UIState.Open));
                var dir=Environment.GetEnvironmentVariable("UIKIT_EVIDENCE_DIR");if(!string.IsNullOrEmpty(dir))File.WriteAllText(Path.Combine(dir,"FailedDefaultBuild_PreservesOwner_AndExplicitSharingTransfersForeground.trace.json"),"{\"events\":[\"conflict rejected\",\"original owner retained\",\"shared foreground transferred\",\"owner restored and reopened\"]}");
            } finally {if(second!=null)await second.ShutdownAsync();if(first!=null)await first.ShutdownAsync();UnityEngine.Object.Destroy(a);UnityEngine.Object.Destroy(b);UnityEngine.Object.Destroy(events);UnityEngine.Object.Destroy(registry);UnityEngine.Object.Destroy(provider);}
        });
        [UnityTest] public IEnumerator RootDestruction_StopsOwnedServices()=>UniTask.ToCoroutine(async()=>
        {
            IntegrationResetProbe.Enter();
            var root=GameObject.Find("T8ResetRoot");UnityEngine.Object.Destroy(root);await UniTask.NextFrame();
            IntegrationResetProbe.AssertStopped();IntegrationResetProbe.Exit();
            var dir=Environment.GetEnvironmentVariable("UIKIT_EVIDENCE_DIR");if(!string.IsNullOrEmpty(dir))File.WriteAllText(Path.Combine(dir,"RootDestruction_StopsOwnedServices.trace.json"),"{\"events\":[\"root destroyed\",\"service released\"]}");
        });
        [UnityTest] public IEnumerator Builder_DefaultResources_OpensAndClosesActualView()=>UniTask.ToCoroutine(async()=>
        {
            var root=new GameObject("Root",typeof(RectTransform),typeof(UIRoot));root.GetComponent<Canvas>().renderMode=RenderMode.ScreenSpaceOverlay;
            var events=IntegrationResetProbe.CreateEvents();var registry=ScriptableObject.CreateInstance<UIRegistry>();
            var provider=ScriptableObject.CreateInstance<IntegrationRegistry>();
            typeof(UIRegistry).GetField("providers",System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic).SetValue(registry,new System.Collections.Generic.List<UIRegistryProvider>{provider});
            IUIContext ui=null;
            try {
                ui=new UIContextBuilder(root.GetComponent<UIRoot>(),registry).Build();var session=await ui.OpenAsync<Wulato.UIKit.Tests.Support.T4ProbeView>();
                Assert.That(session.State,Is.EqualTo(UIState.Open));Assert.That(ui.GetDiagnostics().ActiveLeaseCount,Is.EqualTo(1));
                var previous=ui.GetDiagnostics();await session.CloseAsync();Assert.That(previous.Sessions.Count,Is.EqualTo(1));Assert.That(ui.GetDiagnostics().ActiveLeaseCount,Is.Zero);
                await ui.ShutdownAsync();Assert.That(ui.GetDiagnostics().InputLockCount,Is.Zero);
                var dir=Environment.GetEnvironmentVariable("UIKIT_EVIDENCE_DIR");if(!string.IsNullOrEmpty(dir))File.WriteAllText(Path.Combine(dir,"Builder_DefaultResources_OpensAndClosesActualView.trace.json"),"{\"events\":[\"open\",\"close\",\"shutdown\"]}");
            } finally {if(ui!=null)await ui.ShutdownAsync();UnityEngine.Object.Destroy(root);UnityEngine.Object.Destroy(events);UnityEngine.Object.Destroy(registry);UnityEngine.Object.Destroy(provider);}
        });
    }
    // Observe destruction after Unity's quit callbacks. No explicit Shutdown in this path.
    [DefaultExecutionOrder(31900)]
    public sealed class IntegrationQuitCheck:MonoBehaviour
    {
        internal Action Check;
        void OnDestroy()=>Check?.Invoke();
    }
    public sealed class IntegrationRegistry:UIRegistryProvider
    {
        public override void Populate(UIRegistryBuilder builder)=>builder.Add<Wulato.UIKit.Tests.Support.T4ProbeView>(new UIViewTypeKey("probe"),UIViewAsset.Resources("T4/Probe"));
    }
    // Called by the independent Editor play-mode controller, including across domain reloads.
    public static class IntegrationResetProbe
    {
        static int subscriptions;
        sealed class Resource:IDisposable
        {
            internal bool Disposed;
            internal Resource(){subscriptions++;}
            public void Dispose(){if(Disposed)return;Disposed=true;subscriptions--;}
        }
        static IUIContext ui;static GameObject root,events;static UIRegistry registry;static Resource service;
        static IDisposable oldLock;static UIInputArbiter previousArbiter;
        public static GameObject CreateEvents()
        {
            var go=new GameObject("T8Events",typeof(EventSystem));
#if UIKIT_VERIFY_INPUT_SYSTEM
            var type=Type.GetType("Wulato.UIKit.InputSystem.InputSystemUIInputDriver, Wulato.UIKit.InputSystem",true);go.AddComponent(type);
#else
            go.AddComponent<LegacyUIInputDriver>();
#endif
            return go;
        }
        public static void Enter()
        {
            Assert.That(UIContextFacade.ActiveCount,Is.Zero,"old Context bridge");Assert.That(UnityEngine.Object.FindObjectsOfType<UIInputGate>().Length,Is.Zero,"old gate");
            if(service!=null)Assert.That(service.Disposed,Is.True,"old service subscription");
            root=new GameObject("T8ResetRoot",typeof(RectTransform),typeof(UIRoot));root.GetComponent<Canvas>().renderMode=RenderMode.ScreenSpaceOverlay;events=CreateEvents();registry=ScriptableObject.CreateInstance<UIRegistry>();
            ui=new UIContextBuilder(root.GetComponent<UIRoot>(),registry).WithServiceFactory(_=>service=new Resource()).Build();
            previousArbiter=events.GetComponent<UIInputDomainRegistration>().Domain.Arbiter;
            oldLock=previousArbiter.AcquireLock(ui.GetDiagnostics().ContextId);
            Assert.That(UIContextFacade.ActiveCount,Is.EqualTo(1));
        }
        public static void ArmExit()
        {
            Assert.That(subscriptions,Is.EqualTo(1));
            events.AddComponent<IntegrationQuitCheck>().Check=VerifyAutomaticExit;
        }
        static void VerifyAutomaticExit()
        {
            Assert.That(ui.GetDiagnostics().State,Is.EqualTo(UIContextState.Stopped));
            Assert.That(service.Disposed,Is.True);Assert.That(UIContextFacade.ActiveCount,Is.Zero);
            Assert.That(events.GetComponent<UIInputDomainRegistration>().Domain,Is.Null);
            var registrations=(System.Collections.IDictionary)typeof(UIInputArbiter).GetField("registrations",System.Reflection.BindingFlags.NonPublic|System.Reflection.BindingFlags.Instance).GetValue(previousArbiter);
            Assert.That(registrations.Count,Is.Zero);Assert.That(subscriptions,Is.Zero);
            Assert.Throws<ObjectDisposedException>(()=>previousArbiter.AcquireLock(ui.GetDiagnostics().ContextId));oldLock.Dispose();
            var dir=Environment.GetEnvironmentVariable("UIKIT_EVIDENCE_DIR");
            File.WriteAllText(Path.Combine(dir,"automatic-exit-"+Environment.GetEnvironmentVariable("UIKIT_RESET_CYCLE")+".json"),
                JsonUtility.ToJson(new ExitEvidence{activeContexts=UIContextFacade.ActiveCount,oldLocks=registrations.Count,subscriptions=subscriptions}));
        }
        [Serializable] sealed class ExitEvidence {public string status="PASS";public int activeContexts,oldLocks,subscriptions;}
        public static void AssertStopped()
        {
            Assert.That(ui.GetDiagnostics().State,Is.EqualTo(UIContextState.Stopped));Assert.That(service.Disposed,Is.True);Assert.That(subscriptions,Is.Zero);
        }
        public static void Exit()
        {
            // Same production teardown used at the Editor exit boundary. The lock is deliberately outstanding.
            UIContextFacade.StopPlaySession();Assert.That(ui.GetDiagnostics().State,Is.EqualTo(UIContextState.Stopped));
            Assert.That(service.Disposed,Is.True);Assert.That(UIContextFacade.ActiveCount,Is.Zero);Assert.That(events.GetComponent<UIInputDomainRegistration>().Domain,Is.Null);
            oldLock.Dispose();UnityEngine.Object.DestroyImmediate(root);UnityEngine.Object.DestroyImmediate(events);UnityEngine.Object.DestroyImmediate(registry);
        }
    }
}
