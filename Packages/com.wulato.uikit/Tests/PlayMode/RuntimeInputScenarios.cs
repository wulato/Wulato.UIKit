using System.Collections;
using Cysharp.Threading.Tasks;
using Wulato.UIKit.Navigation;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.TestTools;
using UnityEngine.UI;
using System;
using Object=UnityEngine.Object;
using System.Linq;
namespace Wulato.UIKit.Tests.Input
{
    public sealed class ManualInputEventSystem : EventSystem
    {
        protected override void Update() { } // Tests advance the real module explicitly once per injected frame.
    }
    public class RuntimeInputScene
    {
        public GameObject Root;
        public EventSystem Events;
        public RuntimeInputIntents Gate;
        public InputEventSpy[] Spies;
        public RuntimeInputScene()
        {
            Root=new GameObject("InputPreflight");
            var es=new GameObject("EventSystem");es.transform.SetParent(Root.transform);
            Events=es.AddComponent<ManualInputEventSystem>();Events.enabled=false; // Process is advanced exactly once per injected frame.
            var canvasObject=new GameObject("Canvas",typeof(RectTransform),typeof(Canvas),typeof(GraphicRaycaster));canvasObject.transform.SetParent(Root.transform);
            canvasObject.GetComponent<Canvas>().renderMode=RenderMode.ScreenSpaceOverlay;
            Spies=new InputEventSpy[2];
            for(int i=0;i<2;i++)
            {
                var o=new GameObject("Owner"+i,typeof(RectTransform),typeof(CanvasGroup),typeof(Image),typeof(InputEventSpy));o.transform.SetParent(canvasObject.transform,false);
                var t=o.GetComponent<RectTransform>();t.anchorMin=new Vector2(i*0.5f,0);t.anchorMax=new Vector2((i+1)*0.5f,1);t.offsetMin=t.offsetMax=Vector2.zero;
                Spies[i]=o.GetComponent<InputEventSpy>();
            }
            Gate=new RuntimeInputIntents(Events,Spies);
            Canvas.ForceUpdateCanvases();
        }
        public Vector2 Point(int owner) {return new Vector2(Screen.width*(owner==0?0.25f:0.75f),Screen.height*0.5f);}
    }
    public class RevokeOnInput : MonoBehaviour, IPointerUpHandler, IBeginDragHandler
    {
        public RuntimeInputIntents Gate;
        public bool Transfer,OnDragBegin;
        void Revoke() {if(Transfer)Gate.TransferOwner(1);else Gate.SetLocked(true);}
        public void OnPointerUp(PointerEventData e) {if(!OnDragBegin)Revoke();}
        public void OnBeginDrag(PointerEventData e) {if(OnDragBegin)Revoke();}
    }
    public abstract class RuntimeInputScenarios
    {
        protected RuntimeInputScene Scene;
        protected BaseInputModule Module;
        float previousScale;
        protected abstract void Configure();
        protected abstract void Inject(RawInput state);
        protected virtual void DisposeBackend() {}
        [SetUp] public void Setup()
        {
            previousScale=Time.timeScale;Scene=new RuntimeInputScene();Configure();
            Scene.Gate.Initialize(Module);Module.ActivateModule();Scene.Events.SetSelectedGameObject(Scene.Spies[0].gameObject);
        }
        [TearDown] public void Cleanup()
        {
            if(Scene!=null)
            {
                foreach(var s in Scene.Spies)if(s)s.Save(TestContext.CurrentContext.Test.MethodName);
                Scene.Gate.Dispose();UnityEngine.Object.DestroyImmediate(Scene.Root);
            }
            DisposeBackend();Time.timeScale=previousScale;
        }
        protected IEnumerator Step(RawInput raw)
        {
            yield return null;
            Inject(raw);Module.UpdateModule();Module.Process();
        }
        RawInput At(int owner=0,bool down=false,bool touch=false,bool submit=false,bool cancel=false,Vector2 move=default)
        {return new RawInput {Position=Scene.Point(owner),Pointer=down,Touch=touch,Submit=submit,Cancel=cancel,Move=move};}
        protected InputEventSpy A {get{return Scene.Spies[0];}}
        protected InputEventSpy B {get{return Scene.Spies[1];}}
        IEnumerator FreshClick(int owner=0)
        {yield return Step(At(owner,true));yield return Step(At(owner));}
        IEnumerator FreshSubmit(int owner=0)
        {yield return Step(At(owner,submit:true));yield return Step(At(owner));}
        async UniTask BackFrame(RawInput raw)
        {
            await UniTask.NextFrame();Inject(raw);Module.UpdateModule();Module.Process();await UniTask.NextFrame();
        }
        [UnityTest,Category("focus_controls")] public IEnumerator Focus_ModalRestoresSavedSelectableAndFallsBackWhenInactive()=>UniTask.ToCoroutine(async()=>
        {
            var f=new RuntimeBackFixture(Scene);try
            {
                await f.UI.OpenAsync<RuntimeBackHome>();await BackFrame(At());
                var home=UnityEngine.Object.FindObjectOfType<RuntimeBackHome>();
                var first=home.GetComponentInChildren<Button>();
                var extra=new GameObject("Saved focus",typeof(RectTransform),typeof(Image),typeof(Button));extra.transform.SetParent(home.transform,false);
                await BackFrame(At());Scene.Events.SetSelectedGameObject(extra);
                var modal=await f.UI.OpenAsync<RuntimeBackModal>();await BackFrame(At());
                Assert.That(Scene.Events.currentSelectedGameObject.transform.IsChildOf(UnityEngine.Object.FindObjectOfType<RuntimeBackModal>().transform),Is.True);
                Assert.That(home.GetComponent<CanvasGroup>().interactable,Is.False);
                await modal.CloseAsync();await BackFrame(At());Assert.That(Scene.Events.currentSelectedGameObject,Is.SameAs(extra));
                modal=await f.UI.OpenAsync<RuntimeBackModal>();await BackFrame(At());extra.SetActive(false);
                await modal.CloseAsync();await BackFrame(At());Assert.That(Scene.Events.currentSelectedGameObject,Is.SameAs(first.gameObject));
                Assert.That(home.GetComponent<CanvasGroup>().interactable,Is.True);
            }finally{await f.DisposeAsync();}
        });
        [UnityTest,Category("focus_controls")] public IEnumerator Focus_LockedSelectionRestoresWithoutReplayingHeldSubmit()=>UniTask.ToCoroutine(async()=>
        {
            var f=new RuntimeBackFixture(Scene);try
            {
                await f.UI.OpenAsync<RuntimeBackHome>();await BackFrame(At());
                var button=UnityEngine.Object.FindObjectOfType<RuntimeBackHome>().GetComponentInChildren<Button>();var clicks=0;
                button.onClick.AddListener(()=>clicks++);
                using(f.Arbiter.AcquireLock(f.UI.ContextId))
                {await BackFrame(At(submit:true));Assert.That(Scene.Events.currentSelectedGameObject,Is.Null);Assert.That(clicks,Is.Zero);}
                await BackFrame(At(submit:true));Assert.That(clicks,Is.Zero);
                await BackFrame(At());Assert.That(Scene.Events.currentSelectedGameObject,Is.SameAs(button.gameObject));
                await BackFrame(At(submit:true));Assert.That(clicks,Is.EqualTo(1));await BackFrame(At());
            }finally{await f.DisposeAsync();}
        });
        [UnityTest,Category("input_runtime")] public IEnumerator Cancel_ClosesModalThenBacksPageOnlyAfterFreshEdges()=>UniTask.ToCoroutine(async()=>
        {
            var f=new RuntimeBackFixture(Scene);try
            {
                await f.UI.OpenAsync<RuntimeBackHome>();await f.UI.PushAsync<RuntimeBackPage>();var modal=await f.UI.OpenAsync<RuntimeBackModal>();var nav=f.UI.GetService<IUINavigator>();
                await BackFrame(At());Assert.That(Scene.Events.currentSelectedGameObject,Is.Not.Null);
                await BackFrame(At(cancel:true));Assert.That(modal.State,Is.EqualTo(UIState.Closed));Assert.That((await modal.Closed).Reason,Is.EqualTo(UICloseReason.Back));
                Assert.That(nav.History.Count,Is.EqualTo(2));Assert.That(RuntimeBackPage.Checks,Is.Zero);
                await BackFrame(At(cancel:true));await BackFrame(At(cancel:true));Assert.That(nav.History.Count,Is.EqualTo(2));
                await BackFrame(At());await BackFrame(At(cancel:true));Assert.That(RuntimeBackPage.Checks,Is.EqualTo(1),"One fresh edge must invoke CanClose once.");
                await UniTask.WaitUntil(()=>nav.History.Count==1).Timeout(TimeSpan.FromSeconds(3));Assert.That(nav.History.Count,Is.EqualTo(1));
                await BackFrame(At());await BackFrame(At(cancel:true));Assert.That(nav.History.Count,Is.EqualTo(1));Assert.That(f.UI.GetDiagnostics().Sessions[0].State,Is.EqualTo(UIState.Open));
            }finally{await f.DisposeAsync();}
        });
        [UnityTest,Category("input_runtime")] public IEnumerator Cancel_VetoAndLockNeverFallThroughOrReplayHeldInput()=>UniTask.ToCoroutine(async()=>
        {
            var f=new RuntimeBackFixture(Scene);try
            {
                await f.UI.OpenAsync<RuntimeBackHome>();await f.UI.PushAsync<RuntimeBackPage>();var modal=await f.UI.OpenAsync<RuntimeBackModal>();var nav=f.UI.GetService<IUINavigator>();
                RuntimeBackModal.Allow=false;await BackFrame(At());await BackFrame(At(cancel:true));Assert.That(modal.State,Is.EqualTo(UIState.Open));Assert.That(RuntimeBackModal.Checks,Is.EqualTo(1));Assert.That(RuntimeBackPage.Checks,Is.Zero);
                await BackFrame(At(cancel:true));Assert.That(RuntimeBackModal.Checks,Is.EqualTo(1));await BackFrame(At());RuntimeBackModal.Allow=true;
                var locked=f.Arbiter.AcquireLock(f.UI.ContextId);try{await BackFrame(At(cancel:true));}finally{locked.Dispose();}
                await BackFrame(At(cancel:true));Assert.That(modal.State,Is.EqualTo(UIState.Open));
                await BackFrame(At());await BackFrame(At(cancel:true));Assert.That(modal.State,Is.EqualTo(UIState.Closed));Assert.That(nav.History.Count,Is.EqualTo(2));
                RuntimeBackPage.Allow=false;await BackFrame(At());await BackFrame(At(cancel:true));Assert.That(nav.History.Count,Is.EqualTo(2));Assert.That(await f.UI.BackAsync(),Is.False);Assert.That(nav.History.Count,Is.EqualTo(2));
            }finally{await f.DisposeAsync();}
        });
        [UnityTest,Category("input_runtime")] public IEnumerator Cancel_OnlyTouchesTheOwningContextAcrossModalTransfer()=>UniTask.ToCoroutine(async()=>
        {
            var f=new RuntimeBackFixture(Scene);try
            {
                await f.UI.OpenAsync<RuntimeBackHome>();await f.UI.PushAsync<RuntimeBackPage>();var a=await f.UI.OpenAsync<RuntimeBackModal>();
                var peer=f.CreatePeer();await peer.OpenAsync<RuntimeBackHome>();await peer.PushAsync<RuntimeBackPage>();var b=await peer.OpenAsync<RuntimeBackModal>();
                await BackFrame(At());await BackFrame(At(cancel:true));Assert.That(b.State,Is.EqualTo(UIState.Closed));Assert.That(a.State,Is.EqualTo(UIState.Open));
                await BackFrame(At(cancel:true));Assert.That(a.State,Is.EqualTo(UIState.Open));
                await BackFrame(At());await BackFrame(At(cancel:true));Assert.That(a.State,Is.EqualTo(UIState.Closed));
                Assert.That(f.UI.GetService<IUINavigator>().History.Count,Is.EqualTo(2));Assert.That(peer.GetService<IUINavigator>().History.Count,Is.EqualTo(2));Assert.That(RuntimeBackPage.Checks,Is.Zero);
            }finally{await f.DisposeAsync();}
        });
        [UnityTest,Category("controls_input")] public IEnumerator ScrollRect_ModalInterruptsDragAndRequiresFreshPress()=>UniTask.ToCoroutine(async()=>
        {
            var f=new RuntimeBackFixture(Scene);InputEventSpy spy=null;
            try
            {
                await f.UI.OpenAsync<RuntimeBackHome>();await f.UI.PushAsync<RuntimeBackPage>();
                foreach(var owner in Scene.Spies)owner.GetComponent<Image>().raycastTarget=false;
                var page=UnityEngine.Object.FindObjectOfType<RuntimeBackPage>();
                var pageRect=(RectTransform)page.transform;pageRect.anchorMin=Vector2.zero;pageRect.anchorMax=Vector2.one;pageRect.offsetMin=pageRect.offsetMax=Vector2.zero;
                var viewport=new GameObject("ScrollRect",typeof(RectTransform),typeof(Image),typeof(ScrollRect),typeof(InputEventSpy));viewport.transform.SetParent(page.transform,false);
                var rect=(RectTransform)viewport.transform;rect.anchorMin=Vector2.zero;rect.anchorMax=Vector2.one;rect.offsetMin=rect.offsetMax=Vector2.zero;
                var content=new GameObject("Content",typeof(RectTransform));content.transform.SetParent(viewport.transform,false);
                var cr=(RectTransform)content.transform;cr.anchorMin=new Vector2(0,1);cr.anchorMax=Vector2.one;cr.pivot=new Vector2(.5f,1);cr.sizeDelta=new Vector2(0,3000);
                var scroll=viewport.GetComponent<ScrollRect>();scroll.content=cr;scroll.viewport=rect;scroll.horizontal=false;scroll.inertia=false;scroll.movementType=ScrollRect.MovementType.Clamped;
                spy=viewport.GetComponent<InputEventSpy>();Canvas.ForceUpdateCanvases();
                var raw=At();raw.Position=new Vector2(Screen.width*.5f,Screen.height*.5f);await BackFrame(raw);
                var hits=new System.Collections.Generic.List<RaycastResult>();Scene.Events.RaycastAll(new PointerEventData(Scene.Events){position=raw.Position},hits);
                Assert.That(hits.Count,Is.GreaterThan(0));Assert.That(hits[0].gameObject,Is.SameAs(viewport),"Fixture must raycast the real ScrollRect before injecting a press");
                raw.Pointer=true;await BackFrame(raw);raw.Position+=Vector2.up*30;await BackFrame(raw);raw.Position+=Vector2.up*30;await BackFrame(raw);
                Assert.That(spy.Count("beginDrag"),Is.EqualTo(1));var moved=cr.anchoredPosition.y;Assert.That(moved,Is.GreaterThan(0));
                var modal=await f.UI.OpenAsync<RuntimeBackModal>();
                Assert.That(spy.Count("endDrag"),Is.EqualTo(1));Assert.That(spy.Count("drop"),Is.Zero);
                raw.Position+=Vector2.up*60;await BackFrame(raw);Assert.That(cr.anchoredPosition.y,Is.EqualTo(moved).Within(.1f));
                await modal.CloseAsync();await BackFrame(raw);Assert.That(spy.Count("beginDrag"),Is.EqualTo(1));
                raw.Pointer=false;await BackFrame(raw);raw.Pointer=true;await BackFrame(raw);raw.Position+=Vector2.up*60;await BackFrame(raw);
                Assert.That(spy.Count("beginDrag"),Is.EqualTo(2));raw.Pointer=false;await BackFrame(raw);
            }
            finally{if(spy)spy.Save(TestContext.CurrentContext.Test.MethodName);await f.DisposeAsync();}
        });
        [UnityTest,Category("input_runtime")] public IEnumerator ArbiterDisposed_CancelsHeldPressAndDragExactlyOnce()
        {
            yield return Step(At(down:true));var drag=At(down:true);drag.Position+=new Vector2(40,0);yield return Step(drag);
            Assert.That(A.Count("beginDrag"),Is.EqualTo(1));((IDisposable)Scene.Gate.Arbiter).Dispose();
            Assert.That(A.Count("up"),Is.EqualTo(1));Assert.That(A.Count("endDrag"),Is.EqualTo(1));
            Assert.That(A.Count("click")+A.Count("drop"),Is.Zero);
        }
        [UnityTest,Category("input_runtime")] public IEnumerator Unlocked_ClickSubmitCancelAndMove_Control()
        {
            yield return FreshClick();yield return FreshSubmit();yield return Step(At(cancel:true));yield return Step(At());yield return Step(At(move:Vector2.right));
            Assert.That(A.Count("click"),Is.EqualTo(1));Assert.That(A.Count("submit"),Is.EqualTo(1));Assert.That(A.Count("cancel"),Is.EqualTo(1));Assert.That(A.Count("move"),Is.GreaterThan(0));
        }
        [UnityTest,Category("input_runtime")] public IEnumerator Locked_SubmitCancelAreDiscarded()
        {
            Scene.Gate.SetLocked(true);yield return Step(At(submit:true,cancel:true));
            Assert.That(A.Count("submit")+A.Count("cancel"),Is.Zero);
            Scene.Gate.SetLocked(false);yield return Step(At(submit:true,cancel:true));yield return Step(At());
            Assert.That(A.Count("submit")+A.Count("cancel"),Is.Zero);
            yield return FreshSubmit();yield return Step(At(cancel:true));yield return Step(At());
            Assert.That(A.Count("submit"),Is.EqualTo(1));Assert.That(A.Count("cancel"),Is.EqualTo(1));
        }
        [UnityTest,Category("input_runtime")] public IEnumerator PressedBeforeLock_ReleaseAfterUnlockDoesNotClick()
        {
            yield return Step(At(down:true));Assert.That(A.Count("down"),Is.EqualTo(1));
            Scene.Gate.SetLocked(true);Scene.Gate.SetLocked(false);yield return Step(At(down:true));yield return Step(At());
            Assert.That(A.Count("click"),Is.Zero);Assert.That(A.Count("up"),Is.EqualTo(1));
            yield return FreshClick();Assert.That(A.Count("click"),Is.EqualTo(1));
        }
        [UnityTest,Category("input_runtime")] public IEnumerator HeldSubmit_DoesNotReplayOnUnlock()
        {
            Scene.Gate.SetLocked(true);yield return Step(At(submit:true));Scene.Gate.SetLocked(false);
            yield return Step(At(submit:true));yield return Step(At(submit:true));yield return Step(At());
            Assert.That(A.Count("submit"),Is.Zero);yield return FreshSubmit();Assert.That(A.Count("submit"),Is.EqualTo(1));
        }
        [UnityTest,Category("input_runtime")] public IEnumerator HeldCancel_DoesNotReplayOnUnlock()
        {
            Scene.Gate.SetLocked(true);yield return Step(At(cancel:true));Scene.Gate.SetLocked(false);
            yield return Step(At(cancel:true));yield return Step(At());Assert.That(A.Count("cancel"),Is.Zero);
            yield return Step(At(cancel:true));yield return Step(At());Assert.That(A.Count("cancel"),Is.EqualTo(1));
        }
        [UnityTest,Category("input_runtime")] public IEnumerator DragOwnerTransfer_DoesNotDropOnNewOwner()
        {
            yield return Step(At(down:true));var drag=At(down:true);drag.Position+=new Vector2(40,0);yield return Step(drag);
            Assert.That(A.Count("beginDrag"),Is.EqualTo(1));Scene.Gate.TransferOwner(1);
            Assert.That(A.Count("endDrag"),Is.EqualTo(1));
            yield return Step(At(1,true));yield return Step(At(1));
            Assert.That(A.Count("click")+A.Count("drop")+B.Count("click")+B.Count("drop"),Is.Zero);
            yield return FreshClick(1);Assert.That(B.Count("click"),Is.EqualTo(1));Assert.That(A.Count("click"),Is.Zero);
        }
        [UnityTest,Category("input_runtime")] public IEnumerator TouchAcrossLock_DoesNotClickAndFreshTouchWorks()
        {
            yield return Step(At(touch:true));Assert.That(A.Count("down"),Is.EqualTo(1));Scene.Gate.SetLocked(true);Scene.Gate.SetLocked(false);
            yield return Step(At(touch:true));yield return Step(At());yield return Step(At());
            Assert.That(A.Count("click"),Is.Zero);yield return Step(At(touch:true));yield return Step(At());
            Assert.That(A.Count("click"),Is.EqualTo(1));
        }
        [UnityTest,Category("input_runtime")] public IEnumerator Locked_PointerAndTouchAreDiscarded()
        {
            Scene.Gate.SetLocked(true);yield return Step(At(down:true));yield return Step(At());yield return Step(At(touch:true));yield return Step(At());
            Assert.That(A.Count("down")+A.Count("click"),Is.Zero);Scene.Gate.SetLocked(false);yield return Step(At());yield return FreshClick();Assert.That(A.Count("click"),Is.EqualTo(1));
        }
        [UnityTest,Category("input_runtime")] public IEnumerator HeldMove_RequiresNeutralBeforeNewOwner()
        {
            Scene.Gate.SetLocked(true);yield return Step(At(move:Vector2.right));Scene.Gate.TransferOwner(1);Scene.Gate.SetLocked(false);
            yield return Step(At(1,move:Vector2.right));Assert.That(B.Count("move"),Is.Zero);
            yield return Step(At(1));yield return Step(At(1,move:Vector2.right));Assert.That(B.Count("move"),Is.EqualTo(1));Assert.That(A.Count("move"),Is.Zero);
        }
        [UnityTest,Category("input_runtime")] public IEnumerator Focus_IsClearedAndRestoredToCurrentOwner()
        {
            Assert.That(Scene.Events.currentSelectedGameObject,Is.EqualTo(A.gameObject));Scene.Gate.SetLocked(true);
            Assert.That(Scene.Events.currentSelectedGameObject,Is.Null);Scene.Gate.TransferOwner(1);Scene.Gate.SetLocked(false);yield return Step(At(1));
            Assert.That(Scene.Events.currentSelectedGameObject,Is.EqualTo(B.gameObject));yield return FreshSubmit(1);Assert.That(B.Count("submit"),Is.EqualTo(1));Assert.That(A.Count("submit"),Is.Zero);
        }
        [UnityTest,Category("input_runtime")] public IEnumerator InactiveFocus_IsNotRestored()
        {
            Scene.Gate.SetLocked(true);A.gameObject.SetActive(false);Scene.Gate.SetLocked(false);yield return Step(At());Assert.That(Scene.Events.currentSelectedGameObject,Is.Null);
            A.gameObject.SetActive(true);Scene.Gate.TransferOwner(0);yield return Step(At());yield return FreshSubmit();Assert.That(A.Count("submit"),Is.EqualTo(1));
        }
        [UnityTest,Category("input_runtime")] public IEnumerator TimeScaleZero_LockAndFreshInputWork()
        {
            Time.timeScale=0;Scene.Gate.SetLocked(true);yield return Step(At(submit:true));Scene.Gate.SetLocked(false);yield return Step(At());yield return FreshClick();yield return FreshSubmit();
            Assert.That(A.Count("click"),Is.EqualTo(1));Assert.That(A.Count("submit"),Is.EqualTo(1));
        }
        [UnityTest,Category("input_runtime")] public IEnumerator HeldSubmit_OwnerTransferDoesNotReplay()
        {
            yield return Step(At(submit:true));Assert.That(A.Count("submit"),Is.EqualTo(1));
            Scene.Gate.TransferOwner(1);Assert.That(Scene.Events.currentSelectedGameObject,Is.Null);
            yield return Step(At(1,submit:true));yield return Step(At(1,submit:true));yield return Step(At(1));
            Assert.That(A.Count("submit"),Is.EqualTo(1));Assert.That(B.Count("submit"),Is.Zero);
            yield return FreshSubmit(1);Assert.That(A.Count("submit"),Is.EqualTo(1));Assert.That(B.Count("submit"),Is.EqualTo(1));
        }
        [UnityTest,Category("input_runtime")] public IEnumerator HeldCancel_OwnerTransferDoesNotReplay()
        {
            yield return Step(At(cancel:true));Assert.That(A.Count("cancel"),Is.EqualTo(1));
            Scene.Gate.TransferOwner(1);Assert.That(Scene.Events.currentSelectedGameObject,Is.Null);
            yield return Step(At(1,cancel:true));yield return Step(At(1,cancel:true));yield return Step(At(1));
            Assert.That(A.Count("cancel"),Is.EqualTo(1));Assert.That(B.Count("cancel"),Is.Zero);
            yield return Step(At(1,cancel:true));yield return Step(At(1));Assert.That(A.Count("cancel"),Is.EqualTo(1));Assert.That(B.Count("cancel"),Is.EqualTo(1));
        }
        [UnityTest,Category("input_runtime")] public IEnumerator QueuedSubmit_OwnerTransferBeforeProcessDiscardsEdge()
        {
            yield return null;Inject(At(submit:true));Scene.Gate.TransferOwner(1);Module.UpdateModule();Module.Process();
            Assert.That(A.Count("submit")+B.Count("submit"),Is.Zero);
            yield return Step(At(1,submit:true));yield return Step(At(1));yield return FreshSubmit(1);
            Assert.That(A.Count("submit"),Is.Zero);Assert.That(B.Count("submit"),Is.EqualTo(1));
        }
        [UnityTest,Category("input_runtime")] public IEnumerator QueuedCancel_OwnerTransferBeforeProcessDiscardsEdge()
        {
            yield return null;Inject(At(cancel:true));Scene.Gate.TransferOwner(1);Module.UpdateModule();Module.Process();
            Assert.That(A.Count("cancel")+B.Count("cancel"),Is.Zero);
            yield return Step(At(1,cancel:true));yield return Step(At(1));yield return Step(At(1,cancel:true));yield return Step(At(1));
            Assert.That(A.Count("cancel"),Is.Zero);Assert.That(B.Count("cancel"),Is.EqualTo(1));
        }
        [UnityTest,Category("input_runtime")] public IEnumerator InitialNonOwnerCannotClick_OwnerStillWorks()
        {
            yield return FreshClick(1);Assert.That(B.Count("down")+B.Count("click"),Is.Zero);
            yield return FreshClick();Assert.That(A.Count("click"),Is.EqualTo(1));
        }
        [UnityTest,Category("input_runtime")] public IEnumerator PointerUpLock_DiscardsSameProcessClick()
        {
            var callback=A.gameObject.AddComponent<RevokeOnInput>();callback.Gate=Scene.Gate;
            yield return FreshClick();Assert.That(A.Count("up"),Is.EqualTo(1));Assert.That(A.Count("click"),Is.Zero);
            UnityEngine.Object.DestroyImmediate(callback);Scene.Gate.SetLocked(false);yield return Step(At());
            yield return FreshClick();Assert.That(A.Count("click"),Is.EqualTo(1));
        }
        [UnityTest,Category("input_runtime")] public IEnumerator PointerUpTransfer_DiscardsSameProcessClick()
        {
            var callback=A.gameObject.AddComponent<RevokeOnInput>();callback.Gate=Scene.Gate;callback.Transfer=true;
            yield return FreshClick();Assert.That(A.Count("up"),Is.EqualTo(1));Assert.That(A.Count("click")+B.Count("click"),Is.Zero);
            UnityEngine.Object.DestroyImmediate(callback);yield return Step(At(1));yield return FreshClick(1);
            Assert.That(A.Count("click"),Is.Zero);Assert.That(B.Count("click"),Is.EqualTo(1));
        }
        [UnityTest,Category("input_runtime")] public IEnumerator BeginDragLock_CancelsWithoutDrop_ThenFreshClickWorks()
        {
            var callback=A.gameObject.AddComponent<RevokeOnInput>();callback.Gate=Scene.Gate;callback.OnDragBegin=true;
            yield return Step(At(down:true));var drag=At(down:true);drag.Position+=new Vector2(40,0);yield return Step(drag);
            Assert.That(A.Count("beginDrag"),Is.EqualTo(1));Assert.That(A.Count("endDrag"),Is.EqualTo(1));
            yield return Step(At());Assert.That(A.Count("click")+A.Count("drop")+B.Count("drop"),Is.Zero);
            UnityEngine.Object.DestroyImmediate(callback);Scene.Gate.SetLocked(false);yield return Step(At());yield return FreshClick();
            Assert.That(A.Count("click"),Is.EqualTo(1));
        }
        IEnumerator FreshSideClick(int owner,bool middle=false)
        {
            yield return Step(new RawInput {Position=Scene.Point(owner),RightPointer=!middle,MiddlePointer=middle});
            yield return Step(new RawInput {Position=Scene.Point(owner)});
        }
        IEnumerator RevokeThenNewSidePress(bool transfer)
        {
            var callback=A.gameObject.AddComponent<RevokeOnInput>();callback.Gate=Scene.Gate;callback.Transfer=transfer;
            yield return Step(new RawInput {Position=Scene.Point(0),Pointer=true});
            Assert.That(A.Count("down"),Is.EqualTo(1));
            yield return Step(new RawInput {Position=Scene.Point(0),RightPointer=true,MiddlePointer=true});
            Assert.That(A.Count("down"),Is.EqualTo(1),"No new side-button event after left-up revoked ownership");
            Assert.That(A.Count("click"),Is.Zero);Assert.That(A.Count("up"),Is.EqualTo(1));
            UnityEngine.Object.DestroyImmediate(callback);yield return Step(new RawInput {Position=Scene.Point(transfer?1:0)});
            if(!transfer)Scene.Gate.SetLocked(false);
            yield return Step(new RawInput {Position=Scene.Point(transfer?1:0)});yield return FreshSideClick(transfer?1:0);
            Assert.That(Scene.Spies[transfer?1:0].Count("click"),Is.EqualTo(1));
        }
        IEnumerator RevokeDuringTripleRelease(bool transfer)
        {
            var callback=A.gameObject.AddComponent<RevokeOnInput>();callback.Gate=Scene.Gate;callback.Transfer=transfer;
            yield return Step(new RawInput {Position=Scene.Point(0),Pointer=true,RightPointer=true,MiddlePointer=true});
            Assert.That(A.Count("down"),Is.EqualTo(3));
            yield return Step(new RawInput {Position=Scene.Point(0)});
            Assert.That(A.Count("click"),Is.Zero,"No side-button click after left-up revoked ownership");
            Assert.That(A.Count("up"),Is.EqualTo(3),"One release per prior press, no duplicated cancellation");
            foreach(var button in new[]{"Left","Right","Middle"})
                Assert.That(A.PointerEvents.FindAll(e=>e=="up:"+button).Count,Is.EqualTo(1),button+" must terminate exactly once");
            UnityEngine.Object.DestroyImmediate(callback);if(!transfer)Scene.Gate.SetLocked(false);
            yield return Step(new RawInput {Position=Scene.Point(transfer?1:0)});yield return FreshSideClick(transfer?1:0,true);
            Assert.That(Scene.Spies[transfer?1:0].Count("click"),Is.EqualTo(1));
        }
        [UnityTest,Category("input_runtime")] public IEnumerator LeftUpLock_RejectsLaterSideButtons() {yield return RevokeThenNewSidePress(false);}
        [UnityTest,Category("input_runtime")] public IEnumerator LeftUpTransfer_RejectsLaterSideButtons() {yield return RevokeThenNewSidePress(true);}
        [UnityTest,Category("input_runtime")] public IEnumerator TripleReleaseLock_NoDuplicateUpOrLateClick() {yield return RevokeDuringTripleRelease(false);}
        [UnityTest,Category("input_runtime")] public IEnumerator TripleReleaseTransfer_NoDuplicateUpOrLateClick() {yield return RevokeDuringTripleRelease(true);}
    }
}
