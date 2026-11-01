using System;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.EventSystems;
namespace Wulato.UIKit.Tests.EditMode
{
    [Category("integration")]
    public class BuilderTests
    {
        GameObject root, events;
        UIRegistry registry;
        readonly List<IUIContext> contexts = new List<IUIContext>();
        UIInputDomain domain;
        UIInputArbiter arbiter;
        public sealed class Resource : IDisposable { public bool Disposed; readonly Action action; public Resource(Action action=null){this.action=action;} public void Dispose(){Disposed=true;action?.Invoke();} }
        public sealed class Other : IDisposable { readonly Action action; public Other(Action action){this.action=action;} public void Dispose()=>action(); }
        sealed class Loader : IUIViewLoader, IDisposable
        {
            public bool Disposed;
            public Cysharp.Threading.Tasks.UniTask<IUIViewLease> CreateAsync(UIViewAsset a,Transform p,System.Threading.CancellationToken t)=>throw new NotSupportedException();
            public void Dispose()=>Disposed=true;
        }
        [SetUp] public void Setup()
        {
            root=new GameObject("T8Root",typeof(RectTransform),typeof(UIRoot));root.GetComponent<Canvas>().renderMode=RenderMode.ScreenSpaceOverlay;
            events=EditModeUIFixture.CreateEvents("T8Events");
            registry=ScriptableObject.CreateInstance<UIRegistry>();
            domain=new UIInputDomain(events.GetComponent<EventSystem>(),events.GetComponent<BaseInputModule>());
            arbiter=new UIInputArbiter(domain);
        }
        UIContextBuilder Builder(GameObject target=null)=>new UIContextBuilder((target??root).GetComponent<UIRoot>(),registry).WithInputArbiter(arbiter);
        IUIContext Keep(IUIContext ui){contexts.Add(ui);return ui;}
        [TearDown] public void Cleanup()
        {
            foreach(var ui in contexts)ui.ShutdownAsync().GetAwaiter().GetResult();contexts.Clear();
            domain?.Dispose();UnityEngine.Object.DestroyImmediate(root);EditModeUIFixture.DestroyEvents(events);UnityEngine.Object.DestroyImmediate(registry);
            foreach(var host in UnityEngine.Object.FindObjectsOfType<UIRuntimeHost>())UnityEngine.Object.DestroyImmediate(host.gameObject);
        }
        interface IEqualResource:IDisposable { }
        sealed class EqualResource:IEqualResource
        {
            internal bool Disposed;
            public void Dispose()=>Disposed=true;
            public override bool Equals(object obj)=>obj is IEqualResource;
            public override int GetHashCode()=>0;
        }
#if UIKIT_VERIFY_INPUT_SYSTEM
        [Test] public void LegacyDriverInInputOnlyProject_IsRejected()
        {
            var original=events.GetComponent<BaseInputModule>();original.enabled=false;
            var legacy=events.AddComponent<LegacyUIInputDriver>();IUIContext unexpected=null;
            try {Assert.Throws<InvalidOperationException>(()=>unexpected=Builder().Build());}
            finally {unexpected?.ShutdownAsync().GetAwaiter().GetResult();UnityEngine.Object.DestroyImmediate(legacy);original.enabled=true;}
        }
#endif
        [Test] public void EqualButDistinctServices_BothDispose()
        {
            var first=new EqualResource();var second=new EqualResource();
            var ui=Keep(Builder().WithServiceFactory<object>(_=>first).WithServiceFactory<IEqualResource>(_=>second).Build());
            ui.ShutdownAsync().GetAwaiter().GetResult();Assert.That(first.Disposed,Is.True);Assert.That(second.Disposed,Is.True);
        }
        [Test] public void InvalidRootLayer_DoesNotAllocatePartialHierarchy()
        {
            var foreign=new GameObject("Foreign",typeof(RectTransform));
            try {
                var layers=new RectTransform[5];layers[4]=(RectTransform)foreign.transform;
                typeof(UIRoot).GetField("layers",System.Reflection.BindingFlags.NonPublic|System.Reflection.BindingFlags.Instance).SetValue(root.GetComponent<UIRoot>(),layers);
                Assert.Throws<InvalidOperationException>(()=>Builder().Build());Assert.That(root.transform.childCount,Is.Zero);
            } finally {UnityEngine.Object.DestroyImmediate(foreign);}
        }
        [Test] public void ServiceLookup_RejectsWorkerThread()
        {
            var ui=Keep(Builder().WithServiceFactory(_=>new Resource()).Build());Exception error=null;
            var thread=new System.Threading.Thread(()=>{try{ui.GetService<Resource>();}catch(Exception e){error=e;}});thread.Start();thread.Join();Assert.That(error,Is.TypeOf<UIException>());
        }
        [Test] public void SharedInjectedService_IsNotDisposedByContext()
        {
            var loader=new Loader();var ui=Keep(Builder().WithLoader(loader).Build());ui.ShutdownAsync().GetAwaiter().GetResult();Assert.That(loader.Disposed,Is.False);
        }
        [Test] public void FactoryFails_PreviousServicesDisposeReverseOrder()
        {
            var order=new List<int>();
            Assert.Throws<Exception>(()=>Builder().WithServiceFactory(_=>new Resource(()=>order.Add(1)))
                .WithServiceFactory(ui=>{Assert.That(ui.GetService<Resource>(),Is.Not.Null);return new Other(()=>order.Add(2));})
                .WithServiceFactory<string>(_=>throw new Exception("factory")).Build());
            Assert.That(order,Is.EqualTo(new[]{2,1}));Keep(Builder().Build());
        }
        [Test] public void UnregisteredService_Throws(){var ui=Keep(Builder().Build());Assert.Throws<InvalidOperationException>(()=>ui.GetService<Resource>());}
        [Test] public void StoppedContext_ServiceLookupRejectedButSnapshotReadable()
        {
            var ui=Keep(Builder().Build());ui.ShutdownAsync().GetAwaiter().GetResult();
            Assert.That(Assert.Throws<UIException>(()=>ui.GetService<Resource>()).Code,Is.EqualTo(UIErrorCode.ContextStopped));
            Assert.That(ui.GetDiagnostics().State,Is.EqualTo(UIContextState.Stopped));Assert.That(ui.GetDiagnostics(),Is.SameAs(ui.GetDiagnostics()));
        }
        [Test] public void SourceSettingsMutated_RuntimeUnchanged()
        {
            var settings=new UIContextSettings();var ui=Keep(Builder().WithSettings(settings).Build());settings.DiagnosticsLevel=UIDiagnosticsLevel.Off;
            Assert.That(ui.GetDiagnostics().Level,Is.EqualTo(UIDiagnosticsLevel.Basic));
        }
        [Test] public void SharedInjectedArbiter_BuildsTwoContexts()
        {
            var other=new GameObject("Other",typeof(RectTransform),typeof(UIRoot));other.GetComponent<Canvas>().renderMode=RenderMode.ScreenSpaceOverlay;
            try {var a=Keep(Builder().Build());var b=Keep(Builder(other).Build());Assert.That(a.GetDiagnostics().ContextId,Is.Not.EqualTo(b.GetDiagnostics().ContextId));a.ShutdownAsync().GetAwaiter().GetResult();Assert.That(b.GetDiagnostics().State,Is.EqualTo(UIContextState.Running));}
            finally {UnityEngine.Object.DestroyImmediate(other);}
        }
        [Test] public void SecondDefaultArbiter_BuildFailsAndRollsBack()
        {
            domain.Dispose();domain=null;
            var first=Keep(new UIContextBuilder(root.GetComponent<UIRoot>(),registry).Build());
            var other=new GameObject("Other",typeof(RectTransform),typeof(UIRoot));other.GetComponent<Canvas>().renderMode=RenderMode.ScreenSpaceOverlay;
            try {Assert.Throws<InvalidOperationException>(()=>new UIContextBuilder(other.GetComponent<UIRoot>(),registry).Build());Assert.That(other.transform.childCount,Is.Zero);Assert.That(first.GetDiagnostics().State,Is.EqualTo(UIContextState.Running));}
            finally {UnityEngine.Object.DestroyImmediate(other);}
        }
        [Test] public void DifferentInjectedArbiterForSameDomain_BuildFailsAndRollsBack()
        {
            var first=Keep(Builder().Build());var otherEvents=EditModeUIFixture.CreateEvents("OtherEvents",false);
            var other=new GameObject("Other",typeof(RectTransform),typeof(UIRoot));other.GetComponent<Canvas>().renderMode=RenderMode.ScreenSpaceOverlay;
            using(var d=new UIInputDomain(otherEvents.GetComponent<EventSystem>(),otherEvents.GetComponent<BaseInputModule>()))
            using(var a=new UIInputArbiter(d))
            try {Assert.Throws<InvalidOperationException>(()=>new UIContextBuilder(other.GetComponent<UIRoot>(),registry).WithInputArbiter(a).Build());Assert.That(other.transform.childCount,Is.Zero);Assert.That(first.GetDiagnostics().State,Is.EqualTo(UIContextState.Running));}
            finally {UnityEngine.Object.DestroyImmediate(other);EditModeUIFixture.DestroyEvents(otherEvents);}
        }
        [Test] public void FactoryCannotOpenOrShutdown_AndDuplicateServicesRejected()
        {
            var builder=Builder().WithServiceFactory(ui=>{
                Assert.Throws<InvalidOperationException>(()=>ui.ShutdownAsync());
                Assert.Throws<InvalidOperationException>(()=>ui.OpenAsync(new UIViewTypeKey("missing"),UIArguments.None));return new Resource();});
            Assert.Throws<InvalidOperationException>(()=>builder.WithServiceFactory(_=>new Resource()));Keep(builder.Build());
        }
        [Test] public void OwnedServices_DisposeReverseOrderOnShutdown()
        {
            var order=new List<int>();Keep(Builder().WithServiceFactory(_=>new Resource(()=>order.Add(1))).WithServiceFactory(_=>new Other(()=>order.Add(2))).Build());
            contexts[contexts.Count-1].ShutdownAsync().GetAwaiter().GetResult();Assert.That(order,Is.EqualTo(new[]{2,1}));
        }
        [Test] public void SameRootRejected_ThenReusableAfterShutdown()
        {
            var first=Keep(Builder().Build());Assert.Throws<InvalidOperationException>(()=>Builder().Build());first.ShutdownAsync().GetAwaiter().GetResult();Keep(Builder().Build());
        }
    }
}
