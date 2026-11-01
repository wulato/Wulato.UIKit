#if UNITY_EDITOR
using System;
using System.Collections;
using System.IO;
using System.Linq;
using System.Threading;
using Cysharp.Threading.Tasks;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.TestTools;
using Wulato.UIKit.Samples.QuickStart;
using Wulato.UIKit.Samples.Modals;
using Wulato.UIKit.Samples.Inventory;
namespace Wulato.UIKit.Acceptance.Samples
{
    // Real Prefab loader, with independent live-lease accounting including cached instances.
    sealed class CountingLoader : IUIViewLoader
    {
        sealed class Lease : IUIViewLease
        {
            readonly CountingLoader owner; IUIViewLease inner;
            public Lease(CountingLoader owner,IUIViewLease inner){this.owner=owner;this.inner=inner;}
            public UIViewBase View=>inner.View;
            public void Dispose(){if(inner==null)return;inner.Dispose();inner=null;owner.Live--;}
        }
        public int Created,Live;
        public async UniTask<IUIViewLease> CreateAsync(UIViewAsset asset,Transform parent,CancellationToken ct)
        {var lease=await new PrefabUIViewLoader().CreateAsync(asset,parent,ct);Created++;Live++;return new Lease(this,lease);}
    }
    sealed class SampleFixture : IDisposable
    {
        public readonly GameObject Root,Events;
        public readonly CountingLoader Loader=new CountingLoader();
        public IUIContext UI;
        public SampleFixture(UIRegistry registry=null,int capacity=1)
        {
            Events=CreateEvents();
            Root=new GameObject("Sample root",typeof(RectTransform),typeof(UIRoot));Root.GetComponent<Canvas>().renderMode=RenderMode.ScreenSpaceOverlay;
            registry=registry??AssetDatabase.LoadAssetAtPath<UIRegistry>("Assets/Wulato.UIKit.Generated/UIRegistry.asset");
            Assert.That(registry,Is.Not.Null,"generated imported-sample registry required");
            UI=new UIContextBuilder(Root.GetComponent<UIRoot>(),registry).WithLoader(Loader).WithSettings(new UIContextSettings{CacheCapacity=capacity,DiagnosticsLevel=UIDiagnosticsLevel.Basic}).Build();
        }
        internal static GameObject CreateEvents()
        {
            var events=new GameObject("Sample events",typeof(EventSystem));
            var input=AppDomain.CurrentDomain.GetAssemblies().Select(a=>a.GetType("Wulato.UIKit.InputSystem.InputSystemUIInputDriver")).FirstOrDefault(t=>t!=null);
            if(input!=null)events.AddComponent(input);else events.AddComponent<LegacyUIInputDriver>();return events;
        }
        public void Dispose(){Root.SetActive(false);Events.SetActive(false);UnityEngine.Object.Destroy(Root);UnityEngine.Object.Destroy(Events);}
        public static void Trace(string name,object value)
        {File.WriteAllText(Path.Combine(Environment.GetEnvironmentVariable("UIKIT_EVIDENCE_DIR"),name+".trace.json"),JsonUtility.ToJson(value,true));}
        public static void Trace(string name,string message)=>File.WriteAllText(Path.Combine(Environment.GetEnvironmentVariable("UIKIT_EVIDENCE_DIR"),name+".trace.json"),"{\"events\":[\""+message+"\"]}");
        public static async UniTask Stable(){await UniTask.NextFrame();await UniTask.NextFrame();}
    }
    [Category("samples")]
    public class SampleAcceptanceTests
    {
        [UnityTest] public IEnumerator Scenes_BootstrapAndExternalConfirmation()=>UniTask.ToCoroutine(async()=>
        {
            {
                foreach(var name in new[]{"QuickStart","Modals","Inventory"})
                {
                    var path="Assets/Samples/Wulato UIKit/"+name+"/"+(name=="QuickStart"?"Settings":name)+"Scene.unity";
                    await SampleFixture.Stable();
                    await UnityEditor.SceneManagement.EditorSceneManager.LoadSceneAsyncInPlayMode(path,new UnityEngine.SceneManagement.LoadSceneParameters(UnityEngine.SceneManagement.LoadSceneMode.Additive));
                    var scene=UnityEngine.SceneManagement.SceneManager.GetSceneByPath(path);
                    Assert.That(scene.IsValid()&&scene.isLoaded,Is.True,"runtime scene handle");
                    Assert.That(UnityEngine.Object.FindObjectsOfType<EventSystem>().Length,Is.EqualTo(1),"host selects exactly one EventSystem");
                    IUIContext ui=null;
                    try
                    {
                        var demo=scene.GetRootGameObjects().SelectMany(g=>g.GetComponents<MonoBehaviour>()).Single(c=>c!=null&&c.GetType().Name==name+"Demo");
                        for(int i=0;i<90;i++){ui=(IUIContext)demo.GetType().GetProperty("Context").GetValue(demo);if(ui!=null&&ui.GetDiagnostics().Sessions.Any(x=>x.State==UIState.Open))break;await UniTask.NextFrame();}
                        Assert.That(ui,Is.Not.Null);Assert.That(ui.GetDiagnostics().Sessions.Count,Is.EqualTo(1));Assert.That(ui.GetDiagnostics().Sessions[0].State,Is.EqualTo(UIState.Open));
                        if(name=="Modals")
                        {
                            var modal=(ModalsDemo)demo;var confirmation=new UniTaskCompletionSource<bool>();
                            var pending=modal.ConfirmAndOpenAsync(()=>confirmation.Task);await UniTask.NextFrame();Assert.That(ui.GetDiagnostics().Sessions.Count,Is.EqualTo(1));
                            // A second click while confirmation is pending must not enqueue a duplicate flow.
                            await modal.ConfirmAndOpenAsync(()=>UniTask.FromResult(true));Assert.That(ui.GetDiagnostics().Sessions.Count,Is.EqualTo(1));
                            confirmation.TrySetResult(false);await pending;Assert.That(ui.GetDiagnostics().Sessions.Count,Is.EqualTo(1));
                            await modal.ConfirmAndOpenAsync(()=>UniTask.FromResult(true));Assert.That(ui.GetDiagnostics().Sessions.Count,Is.EqualTo(2));
                        }
                    }
                    finally{if(ui!=null)await ui.ShutdownAsync();var unloading=UnityEngine.SceneManagement.SceneManager.UnloadSceneAsync(scene);Assert.That(unloading,Is.Not.Null,"runtime scene unload");await unloading;await SampleFixture.Stable();}
                }
                SampleFixture.Trace(nameof(Scenes_BootstrapAndExternalConfirmation),"three serialized scenes bootstrap; external confirmation veto/accept; pending-click suppression; no Dialogs dependency");
            }
        });
        [UnityTest] public IEnumerator Settings_OpenAndOwnerClose()=>UniTask.ToCoroutine(async()=>
        {
            using(var f=new SampleFixture())try
            {
                var session=await f.UI.OpenAsync<SettingsView>();var view=UnityEngine.Object.FindObjectOfType<SettingsView>();
                Assert.That(view,Is.Not.Null);Assert.That(view.GetComponentInChildren<UnityEngine.UI.Text>().font,Is.Not.Null);Assert.That(view.Title,Is.EqualTo("Settings"));Assert.That(view.GetComponentInChildren<UnityEngine.UI.Text>().text,Is.EqualTo("Settings"));
                var completion=await session.CloseAsync();await SampleFixture.Stable();Assert.That(session.State,Is.EqualTo(UIState.Closed));Assert.That(f.Loader.Live,Is.Zero);
                Assert.That(UnityEngine.Object.FindObjectsOfType<SettingsView>(true).Length,Is.Zero);
                var userSession=await f.UI.OpenAsync<SettingsView>();UnityEngine.Object.FindObjectOfType<SettingsView>().GetComponentInChildren<UnityEngine.UI.Button>().onClick.Invoke();
                var userClosed=await userSession.Closed;Assert.That(userClosed.Reason,Is.EqualTo(UICloseReason.UserRequest));await SampleFixture.Stable();Assert.That(f.Loader.Live,Is.Zero);SampleFixture.Trace(nameof(Settings_OpenAndOwnerClose),"serialized bindings; font; owner close; actual release");
            }finally{await f.UI.ShutdownAsync();}
        });
        [UnityTest] public IEnumerator TwoModals_RestoreFocus()=>UniTask.ToCoroutine(async()=>
        {
            using(var f=new SampleFixture())try
            {
                var first=await f.UI.OpenAsync<StackedModalView>();await SampleFixture.Stable();var focus=EventSystem.current.currentSelectedGameObject;Assert.That(focus,Is.Not.Null);
                var second=await f.UI.OpenAsync<StackedModalView>();await SampleFixture.Stable();Assert.That(f.UI.GetDiagnostics().InputOwner.Value.SessionId,Is.EqualTo(second.Id));Assert.That(EventSystem.current.currentSelectedGameObject,Is.Not.EqualTo(focus));
                await second.CloseAsync();await SampleFixture.Stable();Assert.That(f.UI.GetDiagnostics().InputOwner.Value.SessionId,Is.EqualTo(first.Id));Assert.That(EventSystem.current.currentSelectedGameObject,Is.EqualTo(focus));
                await first.CloseAsync();await SampleFixture.Stable();Assert.That(f.Loader.Live,Is.Zero);SampleFixture.Trace(nameof(TwoModals_RestoreFocus),"two real modal instances; prior selection restored; release zero");
            }finally{await f.UI.ShutdownAsync();}
        });
        [UnityTest] public IEnumerator InventoryArgs_DisplayCorrectTab()=>UniTask.ToCoroutine(async()=>
        {
            using(var f=new SampleFixture())try
            {
                var session=await f.UI.OpenAsync<InventoryView,InventoryArgs>(new InventoryArgs("player-7",InventoryTab.Equipment));var view=UnityEngine.Object.FindObjectOfType<InventoryView>();
                Assert.That(view.DisplayedTab,Is.EqualTo(InventoryTab.Equipment));Assert.That(view.Title,Is.EqualTo("player-7 / Equipment"));Assert.That(view.GetComponentInChildren<UnityEngine.UI.Text>().text,Is.EqualTo("player-7 / Equipment"));Assert.That(view.GetComponentInChildren<UnityEngine.UI.ScrollRect>(),Is.Not.Null);
                await session.CloseAsync();var old=view;var again=await f.UI.OpenAsync<InventoryView,InventoryArgs>(new InventoryArgs("player-8",InventoryTab.Items));
                view=UnityEngine.Object.FindObjectOfType<InventoryView>();Assert.That(view,Is.SameAs(old));Assert.That(view.Title,Is.EqualTo("player-8 / Items"));await again.CloseAsync();
                await f.UI.ClearCacheAsync();await SampleFixture.Stable();Assert.That(f.Loader.Live,Is.Zero);SampleFixture.Trace(nameof(InventoryArgs_DisplayCorrectTab),"typed args refresh cached view; ordinary ScrollRect; clear releases");
            }finally{await f.UI.ShutdownAsync();}
        });
        [UnityTest] public IEnumerator FiftyCacheCycles_LeaseCountDoesNotGrow()=>UniTask.ToCoroutine(async()=>
        {
            using(var f=new SampleFixture())try
            {
                for(int i=0;i<50;i++){var s=await f.UI.OpenAsync<InventoryView,InventoryArgs>(new InventoryArgs("fixed",InventoryTab.Items));await s.CloseAsync();await SampleFixture.Stable();Assert.That(f.Loader.Live,Is.EqualTo(1));Assert.That(f.UI.GetDiagnostics().CacheLru.Count,Is.EqualTo(1));Assert.That(f.UI.GetDiagnostics().PendingCleanupCount,Is.Zero);}
                Assert.That(f.Loader.Created,Is.EqualTo(1));await f.UI.ClearCacheAsync();await SampleFixture.Stable();Assert.That(f.Loader.Live,Is.Zero);Assert.That(UnityEngine.Object.FindObjectsOfType<InventoryView>(true).Length,Is.Zero);
                SampleFixture.Trace(nameof(FiftyCacheCycles_LeaseCountDoesNotGrow),"50 cycles; one actual lease and instance; clear zero");
            }finally{await f.UI.ShutdownAsync();}
        });
    }
}
#endif
