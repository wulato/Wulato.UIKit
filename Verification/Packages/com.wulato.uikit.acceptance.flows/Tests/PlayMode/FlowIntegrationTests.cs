using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading;
using Cysharp.Threading.Tasks;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.TestTools;
using Wulato.UIKit.Navigation;
using Wulato.UIKit.Dialogs;
using Wulato.UIKit.PopupQueue;
using Wulato.UIKit.Presentation;
using Wulato.UIKit.Tests.Support;
namespace Wulato.UIKit.Acceptance.Flows
{
    public class Home:UIView{}
    public class Settings:UIView{}
    public class Details:UIView<int>{}
    public sealed class Question:IDialogRequest<bool>{}
    public class Confirm:UIView<DialogArguments<Question,bool>>
    {
        internal static IDialogResponder<bool> Responder;
        protected override UniTask OnOpenAsync(DialogArguments<Question,bool> args,UISessionScope scope,CancellationToken ct){Responder=args.Responder;return UniTask.CompletedTask;}
    }
    public class Reward:UIView{}
    public class RegistryProvider:UIRegistryProvider
    {
        public GameObject Home,Settings,Confirm,Reward,Details;
        public override void Populate(UIRegistryBuilder b)
        {
            b.Add<Home>(new UIViewTypeKey("home"),UIViewAsset.Direct(Home),closePolicy:UIClosePolicy.CacheOne);
            b.Add<Details,int>(new UIViewTypeKey("details"),UIViewAsset.Direct(Details));
            b.Add<Settings>(new UIViewTypeKey("settings"),UIViewAsset.Direct(Settings));
            b.AddDialog<Confirm,Question,bool>(new UIViewTypeKey("confirm"),UIViewAsset.Direct(Confirm),UILayer.Modal);
            b.Add<Reward>(new UIViewTypeKey("reward"),UIViewAsset.Direct(Reward),UILayer.Modal);
        }
    }
    public class FlowIntegrationTests:SessionTestBase
    {
        sealed class PresenterFactory:IPresenterFactory
        {
            public int Created,Released;
            public IUIPresenter Create(UIViewBase view,UIArguments args,UISessionScope scope){Created++;return new Presenter(this);}
            sealed class Presenter:IUIPresenter
            {readonly PresenterFactory owner;public Presenter(PresenterFactory owner){this.owner=owner;}public void Bind(){}public void Dispose(){owner.Released++;}}
        }
        sealed class Request:IPopupRequest
        {public UIViewTypeKey ViewType=>new UIViewTypeKey("reward");public UIArguments Arguments=>UIArguments.None;}
        sealed class Fixture
        {
            public IUIContext UI;public readonly PresenterFactory Presenters=new PresenterFactory();
            readonly GameObject sources,root,events;readonly UIRegistry registry;readonly RegistryProvider provider;readonly EventSystem previous;
            public Fixture(INavigationTransition transition=null)
            {
                Confirm.Responder=null;previous=EventSystem.current;sources=new GameObject("flow sources");sources.SetActive(false);
                provider=ScriptableObject.CreateInstance<RegistryProvider>();provider.Home=Prefab<Home>();provider.Settings=Prefab<Settings>();provider.Confirm=Prefab<Confirm>();provider.Reward=Prefab<Reward>();provider.Details=Prefab<Details>();
                registry=ScriptableObject.CreateInstance<UIRegistry>();typeof(UIRegistry).GetField("providers",BindingFlags.Instance|BindingFlags.NonPublic).SetValue(registry,new List<UIRegistryProvider>{provider});
                events=new GameObject("flow events",typeof(EventSystem));
#if ENABLE_INPUT_SYSTEM && !ENABLE_LEGACY_INPUT_MANAGER
                events.AddComponent(Type.GetType("Wulato.UIKit.InputSystem.InputSystemUIInputDriver, Wulato.UIKit.InputSystem",true));
#else
                events.AddComponent<LegacyUIInputDriver>();
#endif
                EventSystem.current=events.GetComponent<EventSystem>();root=new GameObject("flow root",typeof(RectTransform),typeof(UIRoot));root.GetComponent<Canvas>().renderMode=RenderMode.ScreenSpaceOverlay;
                UI=new UIContextBuilder(root.GetComponent<UIRoot>(),registry).WithNavigationTransition(transition).UsePresentation(Presenters)
                    .Build();
            }
            GameObject Prefab<T>()where T:UIViewBase
            {var go=new GameObject(typeof(T).Name,typeof(RectTransform));go.transform.SetParent(sources.transform,false);go.AddComponent<T>();return go;}
            public async UniTask DisposeAsync()
            {
                if(UI!=null)await UI.ShutdownAsync();
                UnityEngine.Object.DestroyImmediate(root);UnityEngine.Object.DestroyImmediate(events);UnityEngine.Object.DestroyImmediate(sources);
                UnityEngine.Object.DestroyImmediate(registry);UnityEngine.Object.DestroyImmediate(provider);if(previous)EventSystem.current=previous;
            }
        }
        static UniTask WaitFor(IUIContext ui,string key)=>UniTask.WaitUntil(()=>ui.GetDiagnostics().Sessions.Any(s=>s.Type.Value==key&&s.State==UIState.Open));
        [UnityTest,Category("flow_integration")] public IEnumerator FacadePageOperations_KeepModalOwnershipAndOneHistory() => UniTask.ToCoroutine(async()=>
        {
            var f=new Fixture();try
            {
                var nav=f.UI.GetService<IUINavigator>();await f.UI.OpenAsync<Home>();await f.UI.PushAsync<Settings>();
                var dialog=f.UI.GetService<IUIDialogs>().ShowAsync(new Question());await WaitFor(f.UI,"confirm");var modal=f.UI.GetDiagnostics().InputOwner.Value.SessionId;
                await f.UI.ReplaceAsync<Details,int>(8);await f.UI.PushAsync<Settings>();Assert.That(await f.UI.PopToAsync<Details>(),Is.True);
                Assert.That(nav.History.Select(p=>p.Type.Value),Is.EqualTo(new[]{"home","details"}));Assert.That(nav.History[1].Arguments.Get<int>(),Is.EqualTo(8));
                Assert.That(f.UI.GetDiagnostics().InputOwner.Value.SessionId,Is.EqualTo(modal));await f.UI.OpenAsync<Home>();
                Assert.That(nav.History.Count,Is.EqualTo(1));Assert.That(f.UI.GetDiagnostics().InputOwner.Value.SessionId,Is.EqualTo(modal));
                Confirm.Responder.TrySubmit(false);Assert.That((await dialog).Value,Is.False);Assert.That(await f.UI.BackAsync(),Is.False);
                await f.UI.ShutdownAsync();Assert.That(f.Presenters.Created,Is.EqualTo(f.Presenters.Released));Assert.That(f.UI.GetDiagnostics().ActiveLeaseCount,Is.Zero);Assert.That(f.UI.GetDiagnostics().InputLockCount,Is.Zero);
            }finally{await f.DisposeAsync();}
        });
        [UnityTest,Category("flow_integration")] public IEnumerator ModalRemainsOwnerAcrossPageReplacement() => UniTask.ToCoroutine(async()=>
        {
            var f=new Fixture();try
            {
                var nav=f.UI.GetService<IUINavigator>();await nav.PushAsync<Home>();
                var dialog=f.UI.GetService<IUIDialogs>().ShowAsync(new Question());await WaitFor(f.UI,"confirm");
                var modal=f.UI.GetDiagnostics().InputOwner.Value.SessionId;await nav.PushAsync<Settings>();
                Assert.That(f.UI.GetDiagnostics().InputOwner.Value.SessionId,Is.EqualTo(modal));
                Assert.That(nav.History.Count,Is.EqualTo(2));Confirm.Responder.TrySubmit(true);Assert.That((await dialog).Value,Is.True);
                Assert.That(await nav.BackAsync(),Is.True);await f.UI.ShutdownAsync();
                Assert.That(f.Presenters.Created,Is.EqualTo(f.Presenters.Released));Assert.That(f.UI.GetDiagnostics().ActiveLeaseCount,Is.Zero);
            }finally{await f.DisposeAsync();}
        });
        [UnityTest,Category("flow_integration")] public IEnumerator PausedQueueAndDialog_StopWithoutStrandedWaiters() => UniTask.ToCoroutine(async()=>
        {
            var f=new Fixture();try
            {
                await f.UI.GetService<IUINavigator>().PushAsync<Home>();var queue=f.UI.GetService<IUIPopupQueue>();
                var a=queue.AcquirePause();var b=queue.AcquirePause();var item=queue.Enqueue(new Request(),deduplicationKey:"reward");
                var dialog=f.UI.GetService<IUIDialogs>().ShowAsync(new Question());await WaitFor(f.UI,"confirm");
                await f.UI.ShutdownAsync();Assert.That((await dialog).Status,Is.EqualTo(DialogStatus.ContextStopped));
                Assert.That((await item).Status,Is.EqualTo(PopupQueueStatus.ContextStopped));a.Dispose();b.Dispose();await queue.StopAsync();
                Assert.That(f.Presenters.Created,Is.EqualTo(f.Presenters.Released));Assert.That(f.UI.GetDiagnostics().InputLockCount,Is.Zero);
            }finally{await f.DisposeAsync();}
        });
        sealed class Pair:INavigationTransition
        {
            internal readonly UniTaskCompletionSource Started=new UniTaskCompletionSource();
            internal readonly UniTaskCompletionSource Gate=new UniTaskCompletionSource();
            public UITransitionWrites FromWrites=>UITransitionWrites.None;public UITransitionWrites ToWrites=>UITransitionWrites.None;
            public UniTask PlayAsync(UINavigationTransitionContext context,CancellationToken ct){Started.TrySetResult();return Gate.Task;}
            public void Stop(UINavigationTransitionContext context){Gate.TrySetCanceled();}public void Reset(UINavigationTransitionContext context){}
        }
        [UnityTest,Category("flow_integration")] public IEnumerator ShutdownDuringNavigationPair_ReleasesProductionResources() => UniTask.ToCoroutine(async()=>
        {
            var pair=new Pair();var f=new Fixture(pair);try
            {
                var open=f.UI.GetService<IUINavigator>().PushAsync<Home>();await pair.Started.Task;await UniTask.Yield();var stop=f.UI.ShutdownAsync();
                try{await open;Assert.Fail();}catch(OperationCanceledException){}await stop;
                Assert.That(f.UI.GetDiagnostics().ActiveLeaseCount,Is.Zero);Assert.That(f.UI.GetDiagnostics().InputLockCount,Is.Zero);
                Assert.That(f.Presenters.Created,Is.EqualTo(f.Presenters.Released));
            }finally{await f.DisposeAsync();}
        });
#if UNITY_EDITOR
        [UnityTest,Category("flow_samples")] public IEnumerator BusinessFlowScene_StartsAndRenders() => UniTask.ToCoroutine(async()=>
        {
            const string path="Assets/Samples/Wulato UIKit/BusinessFlow/BusinessFlowScene.unity";
            await UnityEditor.SceneManagement.EditorSceneManager.LoadSceneAsyncInPlayMode(path,new UnityEngine.SceneManagement.LoadSceneParameters(UnityEngine.SceneManagement.LoadSceneMode.Additive));
            var scene=UnityEngine.SceneManagement.SceneManager.GetSceneByPath(path);
            Assert.That(scene.IsValid()&&scene.isLoaded,Is.True);
            var type=Type.GetType("Wulato.UIKit.Samples.BusinessFlow.BusinessFlowDemo, Wulato.UIKit.Samples.BusinessFlow",true);
            var demo=scene.GetRootGameObjects().SelectMany(go=>go.GetComponents<MonoBehaviour>()).Single(component=>component!=null&&component.GetType()==type);IUIContext ui=null;
            await UniTask.WaitUntil(()=>{ui=(IUIContext)type.GetProperty("Context").GetValue(demo);return ui!=null&&ui.GetDiagnostics().Sessions.Any(s=>s.Type.Value=="flow-home"&&s.State==UIState.Open);});
            Canvas.ForceUpdateCanvases();
            var labels=UnityEngine.Object.FindObjectsOfType<UnityEngine.UI.Text>();
            Assert.That(labels.Any(label=>label.text=="Home"&&label.font!=null&&label.rectTransform.rect.width>0),Is.True);
            Assert.That(ui.GetDiagnostics().InputOwner.HasValue,Is.True);
            await ui.ShutdownAsync();Assert.That(ui.GetDiagnostics().ActiveLeaseCount,Is.Zero);
            await UnityEngine.SceneManagement.SceneManager.UnloadSceneAsync(scene);
        });
#endif
    }
}
