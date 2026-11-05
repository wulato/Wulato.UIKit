using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading;
using Cysharp.Threading.Tasks;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.TestTools;
using Wulato.UIKit.Navigation;
using Wulato.UIKit.Presentation;
using Wulato.UIKit.Dialogs;
using Wulato.UIKit.PopupQueue;
using Object = UnityEngine.Object;
namespace Wulato.UIKit.Verification.Addressables.Tests
{
    [Category("addressables_integration")]
    public sealed class BundleIntegrationTests
    {
        static readonly List<string> Measurements=new List<string>();
        [SetUp] public void BeginTrace()=>Measurements.Clear();
        sealed class TrackingLoader : IUIViewLoader
        {
            readonly AddressablesUIViewLoader inner=new AddressablesUIViewLoader();
            internal int Loaded,Released;internal bool HoldFirst;
            internal readonly UniTaskCompletionSource Started=new UniTaskCompletionSource(),Gate=new UniTaskCompletionSource();
            public async UniTask<IUIViewLease> CreateAsync(UIViewAsset asset,Transform parent,CancellationToken ct)
            {
                var hold=HoldFirst;HoldFirst=false;
                var lease=new Lease(this,await inner.CreateAsync(asset,parent,hold?CancellationToken.None:ct));Loaded++;
                if(hold){Started.TrySetResult();await Gate.Task;}
                return lease;
            }
            sealed class Lease:IUIViewLease
            {
                readonly TrackingLoader owner;IUIViewLease inner;
                public Lease(TrackingLoader owner,IUIViewLease inner){this.owner=owner;this.inner=inner;}
                public UIViewBase View=>inner?.View;
                public void Dispose(){if(inner==null)return;var owned=inner;inner=null;owner.Released++;owned.Dispose();}
            }
        }
        sealed class Presenters:IPresenterFactory
        {
            internal int Created,Released;
            public IUIPresenter Create(UIViewBase view,UIArguments args,UISessionScope scope){Created++;return new Presenter(this);}
            sealed class Presenter:IUIPresenter{readonly Presenters owner;internal Presenter(Presenters owner){this.owner=owner;}public void Bind(){}public void Dispose(){owner.Released++;}}
        }
        sealed class Errors:IUIErrorSink
        {
            internal readonly List<UIException> Values=new List<UIException>();
            public void Report(UIException error)=>Values.Add(error);
        }
        sealed class Fixture
        {
            internal readonly Errors Errors=new Errors();
            internal readonly IUIContext UI;internal readonly TrackingLoader Loader;internal readonly Presenters Presenters=new Presenters();
            internal readonly GameObject Root,Events;readonly UIRegistry registry;readonly BundleRegistryProvider provider;readonly EventSystem prior;
            internal Fixture(TrackingLoader loader=null,INavigationTransition pair=null,bool automatic=false)
            {
                BundleHomeView.Events.Clear();BundleDialogView.Responder=null;Loader=loader??new TrackingLoader();prior=EventSystem.current;
                Events=new GameObject("bundle-events",typeof(EventSystem));
#if ENABLE_INPUT_SYSTEM && !ENABLE_LEGACY_INPUT_MANAGER
                Events.AddComponent(Type.GetType("Wulato.UIKit.InputSystem.InputSystemUIInputDriver, Wulato.UIKit.InputSystem",true));
#else
                Events.AddComponent<LegacyUIInputDriver>();
#endif
                EventSystem.current=Events.GetComponent<EventSystem>();Root=new GameObject("bundle-root",typeof(RectTransform),typeof(UIRoot));Root.GetComponent<Canvas>().renderMode=RenderMode.ScreenSpaceOverlay;
                registry=ScriptableObject.CreateInstance<UIRegistry>();provider=ScriptableObject.CreateInstance<BundleRegistryProvider>();provider.MixedSources=automatic;
                typeof(UIRegistry).GetField("providers",BindingFlags.Instance|BindingFlags.NonPublic).SetValue(registry,new List<UIRegistryProvider>{provider});
                var builder=new UIContextBuilder(Root.GetComponent<UIRoot>(),registry).WithErrorSink(Errors).WithSettings(new UIContextSettings{CacheCapacity=1});
                if(!automatic)builder.WithLoader(Loader);
                UI=builder
                    .WithNavigationTransition(pair).UsePresentation(Presenters).Build();
            }
            internal async UniTask DisposeAsync()
            {
                Loader.Gate.TrySetResult();await UI.ShutdownAsync();await Settle();Object.DestroyImmediate(Root);Object.DestroyImmediate(Events);Object.DestroyImmediate(registry);Object.DestroyImmediate(provider);
                Measurements.Add("leases:"+Loader.Loaded+"/"+Loader.Released+";presenters:"+Presenters.Created+"/"+Presenters.Released);
                if(prior)EventSystem.current=prior;
            }
            internal void AssertStopped(bool transitionFailure=false)
            {
                Assert.That(Errors.Values.All(e=>transitionFailure && e.Code==UIErrorCode.TransitionFailed),Is.True,"Unexpected framework error");
                var d=UI.GetDiagnostics();Assert.That(d.ActiveLeaseCount,Is.Zero);Assert.That(d.InputLockCount,Is.Zero);Assert.That(d.PendingCleanupCount,Is.Zero);
                Assert.That(Loader.Loaded,Is.EqualTo(Loader.Released));Assert.That(Presenters.Created,Is.EqualTo(Presenters.Released));
            }
        }
        sealed class FailPair:INavigationTransition
        {
            internal bool Fail;
            public UITransitionWrites FromWrites=>UITransitionWrites.None;public UITransitionWrites ToWrites=>UITransitionWrites.None;
            public UniTask PlayAsync(UINavigationTransitionContext context,CancellationToken ct)=>Fail?UniTask.FromException(new InvalidOperationException("pair failure")):UniTask.CompletedTask;
            public void Stop(UINavigationTransitionContext context){}public void Reset(UINavigationTransitionContext context){}
        }
        sealed class Request:IPopupRequest{public UIViewTypeKey ViewType=>new UIViewTypeKey("bundle-reward");public UIArguments Arguments=>UIArguments.None;}
        static async UniTask Settle(){for(int i=0;i<5;i++)await UniTask.NextFrame();}
        static async UniTask WaitFor(Func<bool> condition)
        {var deadline=Time.realtimeSinceStartup+5;while(!condition()&&Time.realtimeSinceStartup<deadline)await UniTask.NextFrame();Assert.That(condition(),Is.True,"condition timed out");}
        [UnityTest] public IEnumerator RegistryRouting_MixesThreeSourcesWithoutLoaderInjection() => UniTask.ToCoroutine(async()=>
        {
            var f=new Fixture(automatic:true);try
            {
                var home=await f.UI.OpenAsync<BundleHomeView,int>(19);var first=BundleHomeView.Last;
                await home.CloseAsync();var reopened=await f.UI.OpenAsync<BundleHomeView,int>(23);
                Assert.That(BundleHomeView.Last,Is.SameAs(first));Assert.That(home.Id,Is.Not.EqualTo(reopened.Id));
                await reopened.CloseAsync();await f.UI.ClearCacheAsync();await Settle();Assert.That(first==null,Is.True);
                var resources=await f.UI.OpenAsync<BundlePageView>();await resources.CloseAsync();
                var direct=await f.UI.OpenAsync<AddressableProbeView>();await direct.CloseAsync();
                using(var ct=new CancellationTokenSource())
                {ct.Cancel();try{await f.UI.OpenAsync<BundleHomeView,int>(99,ct.Token);Assert.Fail("Expected cancellation");}catch(OperationCanceledException){}}
                await f.UI.ShutdownAsync();await Settle();f.AssertStopped();
                Assert.That(Resources.Load<GameObject>("UIKitAddressablesFallback"),Is.Not.Null);
                Measurements.Add("default-router:prefab/resources/addressables;cache;cancel;shutdown");
            }finally{await f.DisposeAsync();}
        });
        [UnityTest] public IEnumerator CacheReopen_RebindsPresenterAndPreservesLifecycleOrder() => UniTask.ToCoroutine(async()=>
        {
            var f=new Fixture();try
            {
                var a=await f.UI.OpenAsync<BundleHomeView,int>(42);var first=BundleHomeView.Last;
                Assert.That(BundleHomeView.Events.Take(4),Is.EqualTo(new[]{"create","awake","enable","open"}));
                await a.CloseAsync();Assert.That(f.Loader.Released,Is.Zero);var b=await f.UI.OpenAsync<BundleHomeView,int>(73);
                Assert.That(BundleHomeView.Last,Is.SameAs(first));Assert.That(BundleHomeView.Argument,Is.EqualTo(73));Assert.That(a.Id,Is.Not.EqualTo(b.Id));
                Assert.That(f.Presenters.Created,Is.EqualTo(2));Assert.That(f.Presenters.Released,Is.EqualTo(1));
                await b.CloseAsync();await f.UI.ClearCacheAsync();await Settle();Assert.That(first==null,Is.True);await f.UI.ShutdownAsync();f.AssertStopped();
            }finally{await f.DisposeAsync();}
        });
        [UnityTest] public IEnumerator LruEviction_DestroysOldCloneAndReloadsBundleLease() => UniTask.ToCoroutine(async()=>
        {
            var f=new Fixture();try
            {
                var home=await f.UI.OpenAsync<BundleHomeView,int>(1);var old=BundleHomeView.Last;await home.CloseAsync();
                var page=await f.UI.OpenAsync<BundlePageView>();await page.CloseAsync();await Settle();Assert.That(old==null,Is.True);
                await f.UI.OpenAsync<BundleHomeView,int>(2);Assert.That(f.Loader.Loaded,Is.EqualTo(3));await f.UI.ShutdownAsync();f.AssertStopped();
            }finally{await f.DisposeAsync();}
        });
        [UnityTest] public IEnumerator NavigationRollbackAndBack_RestoreTypedDescriptionAfterEviction() => UniTask.ToCoroutine(async()=>
        {
            var pair=new FailPair();var f=new Fixture(pair:pair);try
            {
                var nav=f.UI.GetService<IUINavigator>();var first=await nav.PushAsync<BundleHomeView,int>(91);pair.Fail=true;
                try{await nav.PushAsync<BundlePageView>();Assert.Fail();}catch(UIException){}
                Assert.That(nav.History.Count,Is.EqualTo(1));Assert.That(first.State,Is.EqualTo(UIState.Open));
                pair.Fail=false;await nav.PushAsync<BundlePageView>();await f.UI.ClearCacheAsync();Assert.That(await nav.BackAsync(),Is.True);
                Assert.That(BundleHomeView.Argument,Is.EqualTo(91));Assert.That(nav.History.Count,Is.EqualTo(1));Assert.That(f.Errors.Values.Any(e=>e.Code==UIErrorCode.TransitionFailed),Is.True);await f.UI.ShutdownAsync();f.AssertStopped(true);
            }finally{await f.DisposeAsync();}
        });
        [UnityTest] public IEnumerator DialogTokenCancellation_WaitsForClosedAndReleasesBundle() => UniTask.ToCoroutine(async()=>
        {
            var f=new Fixture();using(var ct=new CancellationTokenSource())try
            {
                var op=f.UI.GetService<IUIDialogs>().ShowAsync(new BundleQuestion(),ct.Token);await WaitFor(()=>BundleDialogView.Responder!=null && f.UI.GetDiagnostics().Sessions.Any(s=>s.Type.Value=="bundle-dialog" && s.State==UIState.Open));await Settle();
                var stale=BundleDialogView.Responder;ct.Cancel();bool canceled=false;try{await op;}catch(OperationCanceledException){canceled=true;}
                Assert.That(canceled,Is.True);Assert.That(f.UI.GetDiagnostics().Sessions.Count,Is.Zero);Assert.That(stale.TrySubmit(true),Is.False);
                await f.UI.ShutdownAsync();f.AssertStopped();
            }finally{await f.DisposeAsync();}
        });
        [UnityTest] public IEnumerator PausedQueueShutdown_ReleasesActiveBundleAndWaitingKey() => UniTask.ToCoroutine(async()=>
        {
            var f=new Fixture();try
            {
                var q=f.UI.GetService<IUIPopupQueue>();var active=q.Enqueue(new Request(),deduplicationKey:"active");
                await WaitFor(()=>f.UI.GetDiagnostics().Sessions.Any(s=>s.Type.Value=="bundle-reward"&&s.State==UIState.Open));
                var pause=q.AcquirePause();var waiting=q.Enqueue(new Request(),deduplicationKey:"waiting");await f.UI.ShutdownAsync();
                Assert.That((await active).Status,Is.EqualTo(PopupQueueStatus.ContextStopped));Assert.That((await waiting).Status,Is.EqualTo(PopupQueueStatus.ContextStopped));
                pause.Dispose();await q.StopAsync();f.AssertStopped();
            }finally{await f.DisposeAsync();}
        });
        [UnityTest] public IEnumerator CanceledIgnoredLoad_AllowsNextCommandAndReleasesLateLease() => UniTask.ToCoroutine(async()=>
        {
            var loader=new TrackingLoader{HoldFirst=true};var f=new Fixture(loader);using(var ct=new CancellationTokenSource())try
            {
                var op=f.UI.OpenAsync<BundleHomeView,int>(1,ct.Token);await WaitFor(()=>loader.Started.Task.Status.IsCompleted());ct.Cancel();
                bool canceled=false;try{await op;}catch(OperationCanceledException){canceled=true;}Assert.That(canceled,Is.True);
                await f.UI.OpenAsync<BundlePageView>();Assert.That(loader.Loaded,Is.EqualTo(2));loader.Gate.TrySetResult();
                await WaitFor(()=>loader.Released>=1);await f.UI.ShutdownAsync();await Settle();f.AssertStopped();
            }finally{await f.DisposeAsync();}
        });
        [UnityTest] public IEnumerator RootDestroyedBeforeLateHandoff_StillReleasesLease() => UniTask.ToCoroutine(async()=>
        {
            var loader=new TrackingLoader{HoldFirst=true};var f=new Fixture(loader);try
            {
                var op=f.UI.OpenAsync<BundleHomeView,int>(1);await WaitFor(()=>loader.Started.Task.Status.IsCompleted());Object.DestroyImmediate(f.Root);
                try{await op;Assert.Fail();}catch(OperationCanceledException){}await f.UI.ShutdownAsync();loader.Gate.TrySetResult();
                await WaitFor(()=>loader.Released==1);await Settle();Assert.That(loader.Loaded,Is.EqualTo(loader.Released));
            }finally{await f.DisposeAsync();}
        });
        [UnityTest] public IEnumerator MultiAndContexts_KeepIndependentSessionLeases() => UniTask.ToCoroutine(async()=>
        {
            var first=new Fixture();try
            {
                var a=await first.UI.OpenAsync<BundleRewardView>();var b=await first.UI.OpenAsync<BundleRewardView>();
                await a.CloseAsync();Assert.That(b.State,Is.EqualTo(UIState.Open));Assert.That(first.Loader.Released,Is.EqualTo(1));
                var second=new Fixture();try
                {
                    var c=await second.UI.OpenAsync<BundleRewardView>();await first.UI.ShutdownAsync();first.AssertStopped();
                    Assert.That(c.State,Is.EqualTo(UIState.Open));Assert.That(second.Loader.Released,Is.Zero);await second.UI.ShutdownAsync();second.AssertStopped();
                }finally{await second.DisposeAsync();}
            }finally{await first.DisposeAsync();}
        });
        [UnityTest] public IEnumerator AdapterRoutesDirectAndResources_WithoutChangingInactiveContract() => UniTask.ToCoroutine(async()=>
        {
            var source=Resources.Load<GameObject>("UIKitAddressablesFallback");Assert.That(source,Is.Not.Null);
            var parent=new GameObject("fallback-parent");parent.SetActive(false);var loader=new AddressablesUIViewLoader();
            try
            {
                foreach(var asset in new[]{UIViewAsset.Direct(source),UIViewAsset.Resources("UIKitAddressablesFallback")})
                {
                    var lease=await loader.CreateAsync(asset,parent.transform,default);
                    Assert.That(lease.View.gameObject.activeInHierarchy,Is.False);lease.Dispose();lease.Dispose();await Settle();
                    Assert.That(source,Is.Not.Null);
                }
            }finally{Object.DestroyImmediate(parent);}
        });
        [UnityTest] public IEnumerator AdapterGuidAtTimeScaleZero_LoadsAndReleasesRealBundle() => UniTask.ToCoroutine(async()=>
        {
            var guid=Resources.Load<TextAsset>("UIKitAddressablesGuid").text.Trim();var parent=new GameObject("guid-parent");parent.SetActive(false);
            var prior=Time.timeScale;Time.timeScale=0;IUIViewLease lease=null;
            try
            {
                lease=await new AddressablesUIViewLoader().CreateAsync(UIViewAsset.External("addressables",guid),parent.transform,default);
                Assert.That(lease.View,Is.TypeOf<AddressableProbeView>());Assert.That(lease.View.gameObject.activeInHierarchy,Is.False);
                var instance=lease.View.gameObject;lease.Dispose();lease.Dispose();await Settle();Assert.That(instance==null,Is.True);
            }
            finally{lease?.Dispose();Time.timeScale=prior;Object.DestroyImmediate(parent);}
        });
#if UNITY_EDITOR
        [UnityTest] public IEnumerator SampleScene_RendersAfterCacheAndCancellation() => UniTask.ToCoroutine(async()=>
        {
            const string path="Assets/Samples/Wulato UIKit/LocalInventory/LocalInventoryScene.unity";
            await UnityEditor.SceneManagement.EditorSceneManager.LoadSceneAsyncInPlayMode(path,new UnityEngine.SceneManagement.LoadSceneParameters(UnityEngine.SceneManagement.LoadSceneMode.Additive));
            var scene=UnityEngine.SceneManagement.SceneManager.GetSceneByPath(path);
            try
            {
                bool Rendered()=>scene.GetRootGameObjects().SelectMany(g=>g.GetComponentsInChildren<UnityEngine.UI.Text>(true)).Any(t=>t.gameObject.activeInHierarchy&&t.text=="Inventory: 24");
                await WaitFor(Rendered);await Settle();Assert.That(Rendered(),Is.True);
            }
            finally{if(scene.IsValid()&&scene.isLoaded)await UnityEngine.SceneManagement.SceneManager.UnloadSceneAsync(scene).ToUniTask();await Settle();}
            Assert.That(Resources.FindObjectsOfTypeAll<UIViewBase>().Count(v=>v&&v.gameObject.scene.IsValid()&&v.GetType().Name=="AddressableInventoryView"),Is.Zero);
        });
#endif
        [TearDown] public void Trace()
        {
            var dir=Environment.GetEnvironmentVariable("UIKIT_EVIDENCE_DIR");if(!string.IsNullOrEmpty(dir))File.WriteAllText(Path.Combine(dir,TestContext.CurrentContext.Test.Name+".trace.json"),JsonUtility.ToJson(new Report{events=BundleHomeView.Events.ToArray(),measurements=Measurements.ToArray(),frame=Time.frameCount}));
        }
        [Serializable] sealed class Report{public string[] events,measurements;public int frame;}
    }
}
