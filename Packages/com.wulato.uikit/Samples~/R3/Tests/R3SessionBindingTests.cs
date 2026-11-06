using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading;
using Cysharp.Threading.Tasks;
using NUnit.Framework;
using R3;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.TestTools;
using UnityEngine.UI;
namespace Wulato.UIKit.Samples.R3Binding.Tests
{
    public sealed class FailingCounterView:CounterView
    {
        protected override async UniTask OnOpenAsync(CounterModel model,UISessionScope scope,CancellationToken ct)
        {await base.OnOpenAsync(model,scope,ct);throw new InvalidOperationException("binding-open-failure");}
    }
    public sealed class BlockingCounterView:CounterView
    {
        internal static UniTaskCompletionSource Gate;
        protected override async UniTask OnOpenAsync(CounterModel model,UISessionScope scope,CancellationToken ct)
        {await base.OnOpenAsync(model,scope,ct);await Gate.Task;}
    }
    public sealed class CounterRegistry:UIRegistryProvider
    {
        public GameObject Counter,Failing,Blocking;
        public override void Populate(UIRegistryBuilder b)
        {
            b.Add<CounterView,CounterModel>(new UIViewTypeKey("counter"),UIViewAsset.Direct(Counter),UILayer.Modal,closePolicy:UIClosePolicy.CacheOne);
            b.Add<FailingCounterView,CounterModel>(new UIViewTypeKey("failing"),UIViewAsset.Direct(Failing),UILayer.Modal);
            b.Add<BlockingCounterView,CounterModel>(new UIViewTypeKey("blocking"),UIViewAsset.Direct(Blocking),UILayer.Modal);
        }
    }
    [Category("r3_binding")]
    public sealed class R3SessionBindingTests
    {
        sealed class Fixture:IUIErrorSink
        {
            internal readonly IUIContext UI;
            internal readonly List<UIException> Errors=new List<UIException>();
            readonly GameObject sources,root,events;readonly UIRegistry registry;readonly CounterRegistry provider;readonly EventSystem previous;
            internal Fixture()
            {
                previous=EventSystem.current;sources=new GameObject("R3 sources");sources.SetActive(false);
                provider=ScriptableObject.CreateInstance<CounterRegistry>();provider.Counter=Prefab<CounterView>();provider.Failing=Prefab<FailingCounterView>();provider.Blocking=Prefab<BlockingCounterView>();
                registry=ScriptableObject.CreateInstance<UIRegistry>();typeof(UIRegistry).GetField("providers",BindingFlags.Instance|BindingFlags.NonPublic).SetValue(registry,new List<UIRegistryProvider>{provider});
                events=new GameObject("R3 events",typeof(EventSystem));
#if ENABLE_INPUT_SYSTEM && !ENABLE_LEGACY_INPUT_MANAGER
                events.AddComponent(Type.GetType("Wulato.UIKit.InputSystem.InputSystemUIInputDriver, Wulato.UIKit.InputSystem",true));
#else
                events.AddComponent<LegacyUIInputDriver>();
#endif
                EventSystem.current=events.GetComponent<EventSystem>();root=new GameObject("R3 root",typeof(RectTransform),typeof(UIRoot));root.GetComponent<Canvas>().renderMode=RenderMode.ScreenSpaceOverlay;
                UI=new UIContextBuilder(root.GetComponent<UIRoot>(),registry).WithErrorSink(this).Build();
            }
            GameObject Prefab<T>() where T:UIViewBase
            {var go=new GameObject(typeof(T).Name,typeof(RectTransform));go.transform.SetParent(sources.transform,false);go.AddComponent<T>();return go;}
            internal CounterView View=>root.GetComponentInChildren<CounterView>(true);
            internal Text Label=>View.GetComponentsInChildren<Text>(true).Single(t=>t.name=="Count");
            internal Button Button(string name)=>View.GetComponentsInChildren<Button>(true).Single(b=>b.name==name);
            public void Report(UIException error)=>Errors.Add(error);
            internal async UniTask DisposeAsync()
            {
                await UI.ShutdownAsync();Assert.That(UI.GetDiagnostics().ActiveLeaseCount,Is.Zero);Assert.That(UI.GetDiagnostics().InputLockCount,Is.Zero);
                UnityEngine.Object.DestroyImmediate(root);UnityEngine.Object.DestroyImmediate(events);UnityEngine.Object.DestroyImmediate(sources);UnityEngine.Object.DestroyImmediate(registry);UnityEngine.Object.DestroyImmediate(provider);
                if(previous)EventSystem.current=previous;
            }
        }
        static string Version=>typeof(ReactiveProperty<int>).Assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>().InformationalVersion;
        [Serializable] sealed class Trace {public string caseName,version,status,unityAssembly;}
        [TearDown] public void SaveTrace()
        {
            var dir=Environment.GetEnvironmentVariable("UIKIT_EVIDENCE_DIR");if(string.IsNullOrEmpty(dir))return;
            var data=JsonUtility.ToJson(new Trace{caseName=TestContext.CurrentContext.Test.FullName,version=Version,status=TestContext.CurrentContext.Result.Outcome.Status.ToString(),unityAssembly=typeof(ObserveOnExtensions).Assembly.GetName().Name});
            File.WriteAllText(Path.Combine(dir,TestContext.CurrentContext.Test.Name+".trace.json"),data);File.WriteAllText(Path.Combine(dir,"r3-sdk.trace.json"),data);
        }
        static UniTask Wait(Func<bool> predicate)=>UniTask.WaitUntil(predicate).Timeout(TimeSpan.FromSeconds(3));
        [Test] public void SdkIdentity_UsesRealPinnedCoreAndUnityExtensions()
        {
            Assert.That(Version,Does.StartWith("1.3.1"));Assert.That(typeof(ObserveOnExtensions).Assembly.GetName().Name,Is.EqualTo("R3.Unity"));
            var references=typeof(IUIContext).Assembly.GetReferencedAssemblies().Select(a=>a.Name).ToArray();Assert.That(references,Does.Not.Contain("R3"));Assert.That(references,Does.Not.Contain("R3.Unity"));
        }
        [UnityTest] public IEnumerator CachedReopen_HasOneLiveSubscriptionAndButtonListener()=>UniTask.ToCoroutine(async()=>
        {
            var f=new Fixture();using(var model=new CounterModel())try
            {
                CounterView cached=null;
                for(int i=0;i<3;i++)
                {
                    var session=await f.UI.OpenAsync<CounterView,CounterModel>(model);if(cached==null)cached=f.View;else Assert.That(f.View,Is.SameAs(cached));
                    await Wait(()=>f.Label.text==i.ToString());Assert.That(model.Count.HasObservers,Is.True);f.Button("Increment").onClick.Invoke();Assert.That(model.Count.Value,Is.EqualTo(i+1));
                    await Wait(()=>f.Label.text==(i+1).ToString());await session.CloseAsync();Assert.That(model.Count.HasObservers,Is.False);Assert.That(model.Count.IsDisposed,Is.False);
                }
                Assert.That(f.Errors,Is.Empty);
            }finally{await f.DisposeAsync();}
        });
        [UnityTest] public IEnumerator EndedSession_DetachesWithoutDisposingTheBusinessModel()=>UniTask.ToCoroutine(async()=>
        {
            var f=new Fixture();using(var model=new CounterModel())try
            {
                var session=await f.UI.OpenAsync<CounterView,CounterModel>(model);await Wait(()=>f.Label.text=="0");var label=f.Label;
                await session.CloseAsync();Assert.That(model.Count.HasObservers,Is.False);model.Increment();await UniTask.NextFrame();await UniTask.NextFrame();Assert.That(label.text,Is.EqualTo("0"));Assert.That(model.Count.IsDisposed,Is.False);
            }finally{await f.DisposeAsync();}
        });
        [UnityTest] public IEnumerator WorkerNotification_WritesUguiOnTheMainThread()=>UniTask.ToCoroutine(async()=>
        {
            var f=new Fixture();using(var model=new CounterModel())try
            {
                await f.UI.OpenAsync<CounterView,CounterModel>(model);await Wait(()=>f.Label.text=="0");int main=Thread.CurrentThread.ManagedThreadId,writer=0;var label=f.Label;
                UnityEngine.Events.UnityAction dirty=()=>writer=Thread.CurrentThread.ManagedThreadId;label.RegisterDirtyVerticesCallback(dirty);
                try{System.Threading.Tasks.Task.Run(()=>model.Count.Value=42).GetAwaiter().GetResult();Assert.That(label.text,Is.EqualTo("0"));await Wait(()=>label.text=="42");Assert.That(writer,Is.EqualTo(main));}
                finally{label.UnregisterDirtyVerticesCallback(dirty);}
            }finally{await f.DisposeAsync();}
        });
        [UnityTest] public IEnumerator QueuedOldSession_CannotOverwriteTheCachedReopen()=>UniTask.ToCoroutine(async()=>
        {
            var f=new Fixture();using(var a=new CounterModel())using(var b=new CounterModel(7))try
            {
                var first=await f.UI.OpenAsync<CounterView,CounterModel>(a);await Wait(()=>f.Label.text=="0");var cached=f.View;var paints=new List<string>();var label=f.Label;UnityEngine.Events.UnityAction dirty=()=>paints.Add(label.text);label.RegisterDirtyVerticesCallback(dirty);
                try
                {
                    System.Threading.Tasks.Task.Run(()=>a.Count.Value=123).GetAwaiter().GetResult();await first.CloseAsync();Assert.That(a.Count.HasObservers,Is.False);
                    await f.UI.OpenAsync<CounterView,CounterModel>(b);Assert.That(f.View,Is.SameAs(cached));a.Count.Value=456;await Wait(()=>label.text=="7");await UniTask.NextFrame();
                    Assert.That(paints,Does.Not.Contain("123"));Assert.That(paints,Does.Not.Contain("456"));f.Button("Increment").onClick.Invoke();Assert.That(b.Count.Value,Is.EqualTo(8));Assert.That(a.Count.Value,Is.EqualTo(456));
                }finally{label.UnregisterDirtyVerticesCallback(dirty);}
            }finally{await f.DisposeAsync();}
        });
        [UnityTest] public IEnumerator NativeCloseButton_EndsTheSessionAndAllBindings()=>UniTask.ToCoroutine(async()=>
        {
            var f=new Fixture();using(var model=new CounterModel())try
            {
                var session=await f.UI.OpenAsync<CounterView,CounterModel>(model);await Wait(()=>f.Label.text=="0");var button=f.Button("Increment");f.Button("Close").onClick.Invoke();
                Assert.That((await session.Closed).Reason,Is.EqualTo(UICloseReason.UserRequest));Assert.That(model.Count.HasObservers,Is.False);button.onClick.Invoke();Assert.That(model.Count.Value,Is.Zero);
            }finally{await f.DisposeAsync();}
        });
        [UnityTest] public IEnumerator Shutdown_UnbindsWhileTheModelRemainsUsable()=>UniTask.ToCoroutine(async()=>
        {
            var f=new Fixture();using(var model=new CounterModel())try
            {
                await f.UI.OpenAsync<CounterView,CounterModel>(model);await Wait(()=>f.Label.text=="0");await f.UI.ShutdownAsync();Assert.That(model.Count.HasObservers,Is.False);Assert.That(model.Count.IsDisposed,Is.False);model.Increment();await UniTask.NextFrame();Assert.That(f.Errors,Is.Empty);
            }finally{await f.DisposeAsync();}
        });
        [UnityTest] public IEnumerator OpenFailure_RollsBackAlreadyCreatedSubscriptions()=>UniTask.ToCoroutine(async()=>
        {
            var f=new Fixture();using(var model=new CounterModel())try
            {
                try{await f.UI.OpenAsync<FailingCounterView,CounterModel>(model);Assert.Fail();}catch(UIException error){Assert.That(error.Code,Is.EqualTo(UIErrorCode.LifecycleFailed));}
                Assert.That(model.Count.HasObservers,Is.False);Assert.That(model.Count.IsDisposed,Is.False);model.Increment();await UniTask.NextFrame();Assert.That(f.UI.GetDiagnostics().ActiveLeaseCount,Is.Zero);
            }finally{await f.DisposeAsync();}
        });
        [UnityTest] public IEnumerator CanceledOpen_UnbindsBeforeLateCleanupCompletes()=>UniTask.ToCoroutine(async()=>
        {
            var f=new Fixture();using(var model=new CounterModel())using(var caller=new CancellationTokenSource())try
            {
                BlockingCounterView.Gate=new UniTaskCompletionSource();var open=f.UI.OpenAsync<BlockingCounterView,CounterModel>(model,caller.Token);await Wait(()=>model.Count.HasObservers);
                caller.Cancel();try{await open;Assert.Fail();}catch(OperationCanceledException){}
                Assert.That(model.Count.HasObservers,Is.False);Assert.That(model.Count.IsDisposed,Is.False);model.Increment();BlockingCounterView.Gate.TrySetResult();await UniTask.NextFrame();await Wait(()=>f.UI.GetDiagnostics().ActiveLeaseCount==0);
            }finally{BlockingCounterView.Gate?.TrySetResult();await f.DisposeAsync();}
        });
    }
}
