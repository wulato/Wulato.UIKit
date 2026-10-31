using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.TestTools;
using UnityEngine.UI;
namespace Wulato.UIKit.Verification.Tests
{
    public struct RawInput
    {
        public bool Pointer,Touch,Submit,Cancel;
        public Vector2 Position,Move;
    }
    public class InputProbeSceneFactory
    {
        public GameObject Root;
        public EventSystem Events;
        public InputGateProbe Gate;
        public InputEventSpy[] Spies;
        public InputProbeSceneFactory()
        {
            Root=new GameObject("InputPreflight");
            var es=new GameObject("EventSystem");es.transform.SetParent(Root.transform);
            Events=es.AddComponent<EventSystem>();Events.enabled=false; // Process is advanced exactly once per injected frame.
            var canvasObject=new GameObject("Canvas",typeof(RectTransform),typeof(Canvas),typeof(GraphicRaycaster));canvasObject.transform.SetParent(Root.transform);
            canvasObject.GetComponent<Canvas>().renderMode=RenderMode.ScreenSpaceOverlay;
            Spies=new InputEventSpy[2];
            for(int i=0;i<2;i++)
            {
                var o=new GameObject("Owner"+i,typeof(RectTransform),typeof(CanvasGroup),typeof(Image),typeof(InputEventSpy));o.transform.SetParent(canvasObject.transform,false);
                var t=o.GetComponent<RectTransform>();t.anchorMin=new Vector2(i*0.5f,0);t.anchorMax=new Vector2((i+1)*0.5f,1);t.offsetMin=t.offsetMax=Vector2.zero;
                Spies[i]=o.GetComponent<InputEventSpy>();Spies[i].EventsSystem=Events;
            }
            Gate=es.AddComponent<InputGateProbe>();Gate.EventSystem=Events;Gate.Owners=Spies;
            Canvas.ForceUpdateCanvases();
        }
        public Vector2 Point(int owner) {return new Vector2(Screen.width*(owner==0?0.25f:0.75f),Screen.height*0.5f);}
    }
    public abstract class InputGateScenarios
    {
        protected InputProbeSceneFactory Scene;
        protected BaseInputModule Module;
        float previousScale;
        protected abstract void Configure();
        protected abstract void Inject(RawInput state);
        protected virtual void DisposeBackend() {}
        [SetUp] public void Setup()
        {
            previousScale=Time.timeScale;Scene=new InputProbeSceneFactory();Configure();
            Module.ActivateModule();Scene.Events.SetSelectedGameObject(Scene.Spies[0].gameObject);
        }
        [TearDown] public void Cleanup()
        {
            if(Scene!=null)
            {
                foreach(var s in Scene.Spies)if(s)s.Save(TestContext.CurrentContext.Test.MethodName);
                Object.DestroyImmediate(Scene.Root);
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
        [UnityTest,Category("input_preflight")] public IEnumerator Unlocked_ClickSubmitCancelAndMove_Control()
        {
            yield return FreshClick();yield return FreshSubmit();yield return Step(At(cancel:true));yield return Step(At());yield return Step(At(move:Vector2.right));
            Assert.That(A.Count("click"),Is.EqualTo(1));Assert.That(A.Count("submit"),Is.EqualTo(1));Assert.That(A.Count("cancel"),Is.EqualTo(1));Assert.That(A.Count("move"),Is.GreaterThan(0));
        }
        [UnityTest,Category("input_preflight")] public IEnumerator Locked_SubmitCancelAreDiscarded()
        {
            Scene.Gate.SetLocked(true);yield return Step(At(submit:true,cancel:true));
            Assert.That(A.Count("submit")+A.Count("cancel"),Is.Zero);
            Scene.Gate.SetLocked(false);yield return Step(At(submit:true,cancel:true));yield return Step(At());
            Assert.That(A.Count("submit")+A.Count("cancel"),Is.Zero);
            yield return FreshSubmit();yield return Step(At(cancel:true));yield return Step(At());
            Assert.That(A.Count("submit"),Is.EqualTo(1));Assert.That(A.Count("cancel"),Is.EqualTo(1));
        }
        [UnityTest,Category("input_preflight")] public IEnumerator PressedBeforeLock_ReleaseAfterUnlockDoesNotClick()
        {
            yield return Step(At(down:true));Assert.That(A.Count("down"),Is.EqualTo(1));
            Scene.Gate.SetLocked(true);Scene.Gate.SetLocked(false);yield return Step(At(down:true));yield return Step(At());
            Assert.That(A.Count("click"),Is.Zero);Assert.That(A.Count("up"),Is.EqualTo(1));
            yield return FreshClick();Assert.That(A.Count("click"),Is.EqualTo(1));
        }
        [UnityTest,Category("input_preflight")] public IEnumerator HeldSubmit_DoesNotReplayOnUnlock()
        {
            Scene.Gate.SetLocked(true);yield return Step(At(submit:true));Scene.Gate.SetLocked(false);
            yield return Step(At(submit:true));yield return Step(At(submit:true));yield return Step(At());
            Assert.That(A.Count("submit"),Is.Zero);yield return FreshSubmit();Assert.That(A.Count("submit"),Is.EqualTo(1));
        }
        [UnityTest,Category("input_preflight")] public IEnumerator HeldCancel_DoesNotReplayOnUnlock()
        {
            Scene.Gate.SetLocked(true);yield return Step(At(cancel:true));Scene.Gate.SetLocked(false);
            yield return Step(At(cancel:true));yield return Step(At());Assert.That(A.Count("cancel"),Is.Zero);
            yield return Step(At(cancel:true));yield return Step(At());Assert.That(A.Count("cancel"),Is.EqualTo(1));
        }
        [UnityTest,Category("input_preflight")] public IEnumerator DragOwnerTransfer_DoesNotDropOnNewOwner()
        {
            yield return Step(At(down:true));var drag=At(down:true);drag.Position+=new Vector2(40,0);yield return Step(drag);
            Assert.That(A.Count("beginDrag"),Is.EqualTo(1));Scene.Gate.TransferOwner(1);
            Assert.That(A.Count("endDrag"),Is.EqualTo(1));
            yield return Step(At(1,true));yield return Step(At(1));
            Assert.That(A.Count("click")+A.Count("drop")+B.Count("click")+B.Count("drop"),Is.Zero);
            yield return FreshClick(1);Assert.That(B.Count("click"),Is.EqualTo(1));Assert.That(A.Count("click"),Is.Zero);
        }
        [UnityTest,Category("input_preflight")] public IEnumerator TouchAcrossLock_DoesNotClickAndFreshTouchWorks()
        {
            yield return Step(At(touch:true));Assert.That(A.Count("down"),Is.EqualTo(1));Scene.Gate.SetLocked(true);Scene.Gate.SetLocked(false);
            yield return Step(At(touch:true));yield return Step(At());yield return Step(At());
            Assert.That(A.Count("click"),Is.Zero);yield return Step(At(touch:true));yield return Step(At());
            Assert.That(A.Count("click"),Is.EqualTo(1));
        }
        [UnityTest,Category("input_preflight")] public IEnumerator Locked_PointerAndTouchAreDiscarded()
        {
            Scene.Gate.SetLocked(true);yield return Step(At(down:true));yield return Step(At());yield return Step(At(touch:true));yield return Step(At());
            Assert.That(A.Count("down")+A.Count("click"),Is.Zero);Scene.Gate.SetLocked(false);yield return Step(At());yield return FreshClick();Assert.That(A.Count("click"),Is.EqualTo(1));
        }
        [UnityTest,Category("input_preflight")] public IEnumerator HeldMove_RequiresNeutralBeforeNewOwner()
        {
            Scene.Gate.SetLocked(true);yield return Step(At(move:Vector2.right));Scene.Gate.TransferOwner(1);Scene.Gate.SetLocked(false);
            yield return Step(At(1,move:Vector2.right));Assert.That(B.Count("move"),Is.Zero);
            yield return Step(At(1));yield return Step(At(1,move:Vector2.right));Assert.That(B.Count("move"),Is.EqualTo(1));Assert.That(A.Count("move"),Is.Zero);
        }
        [UnityTest,Category("input_preflight")] public IEnumerator Focus_IsClearedAndRestoredToCurrentOwner()
        {
            Assert.That(Scene.Events.currentSelectedGameObject,Is.EqualTo(A.gameObject));Scene.Gate.SetLocked(true);
            Assert.That(Scene.Events.currentSelectedGameObject,Is.Null);Scene.Gate.TransferOwner(1);Scene.Gate.SetLocked(false);yield return Step(At(1));
            Assert.That(Scene.Events.currentSelectedGameObject,Is.EqualTo(B.gameObject));yield return FreshSubmit(1);Assert.That(B.Count("submit"),Is.EqualTo(1));Assert.That(A.Count("submit"),Is.Zero);
        }
        [UnityTest,Category("input_preflight")] public IEnumerator InactiveFocus_IsNotRestored()
        {
            Scene.Gate.SetLocked(true);A.gameObject.SetActive(false);Scene.Gate.SetLocked(false);yield return Step(At());Assert.That(Scene.Events.currentSelectedGameObject,Is.Null);
            A.gameObject.SetActive(true);Scene.Gate.TransferOwner(0);yield return Step(At());yield return FreshSubmit();Assert.That(A.Count("submit"),Is.EqualTo(1));
        }
        [UnityTest,Category("input_preflight")] public IEnumerator TimeScaleZero_LockAndFreshInputWork()
        {
            Time.timeScale=0;Scene.Gate.SetLocked(true);yield return Step(At(submit:true));Scene.Gate.SetLocked(false);yield return Step(At());yield return FreshClick();yield return FreshSubmit();
            Assert.That(A.Count("click"),Is.EqualTo(1));Assert.That(A.Count("submit"),Is.EqualTo(1));
        }
        [UnityTest,Category("input_preflight")] public IEnumerator HeldSubmit_OwnerTransferDoesNotReplay()
        {
            yield return Step(At(submit:true));Assert.That(A.Count("submit"),Is.EqualTo(1));
            Scene.Gate.TransferOwner(1);Assert.That(Scene.Events.currentSelectedGameObject,Is.Null);
            yield return Step(At(1,submit:true));yield return Step(At(1,submit:true));yield return Step(At(1));
            Assert.That(A.Count("submit"),Is.EqualTo(1));Assert.That(B.Count("submit"),Is.Zero);
            yield return FreshSubmit(1);Assert.That(A.Count("submit"),Is.EqualTo(1));Assert.That(B.Count("submit"),Is.EqualTo(1));
        }
        [UnityTest,Category("input_preflight")] public IEnumerator HeldCancel_OwnerTransferDoesNotReplay()
        {
            yield return Step(At(cancel:true));Assert.That(A.Count("cancel"),Is.EqualTo(1));
            Scene.Gate.TransferOwner(1);Assert.That(Scene.Events.currentSelectedGameObject,Is.Null);
            yield return Step(At(1,cancel:true));yield return Step(At(1,cancel:true));yield return Step(At(1));
            Assert.That(A.Count("cancel"),Is.EqualTo(1));Assert.That(B.Count("cancel"),Is.Zero);
            yield return Step(At(1,cancel:true));yield return Step(At(1));Assert.That(A.Count("cancel"),Is.EqualTo(1));Assert.That(B.Count("cancel"),Is.EqualTo(1));
        }
        [UnityTest,Category("input_preflight")] public IEnumerator QueuedSubmit_OwnerTransferBeforeProcessDiscardsEdge()
        {
            yield return null;Inject(At(submit:true));Scene.Gate.TransferOwner(1);Module.UpdateModule();Module.Process();
            Assert.That(A.Count("submit")+B.Count("submit"),Is.Zero);
            yield return Step(At(1,submit:true));yield return Step(At(1));yield return FreshSubmit(1);
            Assert.That(A.Count("submit"),Is.Zero);Assert.That(B.Count("submit"),Is.EqualTo(1));
        }
        [UnityTest,Category("input_preflight")] public IEnumerator QueuedCancel_OwnerTransferBeforeProcessDiscardsEdge()
        {
            yield return null;Inject(At(cancel:true));Scene.Gate.TransferOwner(1);Module.UpdateModule();Module.Process();
            Assert.That(A.Count("cancel")+B.Count("cancel"),Is.Zero);
            yield return Step(At(1,cancel:true));yield return Step(At(1));yield return Step(At(1,cancel:true));yield return Step(At(1));
            Assert.That(A.Count("cancel"),Is.Zero);Assert.That(B.Count("cancel"),Is.EqualTo(1));
        }
    }
}
