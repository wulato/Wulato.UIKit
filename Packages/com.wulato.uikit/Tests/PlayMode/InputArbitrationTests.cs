using System;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.EventSystems;
using Wulato.UIKit.Tests.Input;
using Wulato.UIKit.Tests.Support;
namespace Wulato.UIKit.Tests.PlayMode
{
    public abstract class InputArbitrationFixture : SessionTestBase
    {
        internal RuntimeInputScene Scene;
        internal BaseInputModule Module;
        internal UIInputArbiter Arbiter => (UIInputArbiter)Scene.Gate.Arbiter;
        [SetUp] public void SetupInput()
        {
            Scene=new RuntimeInputScene();
#if UIKIT_VERIFY_INPUT_SYSTEM
            Module=RuntimeInputIntents.AddDriver(Scene.Events,"Wulato.UIKit.InputSystem.InputSystemUIInputDriver");
#else
            Module=RuntimeInputIntents.AddDriver(Scene.Events,"Wulato.UIKit.LegacyUIInputDriver");
            ((LegacyUIInputDriver)Module).ReleaseState=()=>true;
#endif
            Scene.Gate.Initialize(Module);Module.ActivateModule();
            Scene.Events.SetSelectedGameObject(Scene.Spies[0].gameObject);
        }
        [TearDown] public void CleanupInput(){Scene.Gate.Dispose();UnityEngine.Object.DestroyImmediate(Scene.Root);}
        internal UIInputGrant PrepareB(bool emergency=false)=>Arbiter.Prepare(new UIInputCandidate(Scene.Gate.Contexts[1],Scene.Gate.Sessions[1],Scene.Spies[1].GetComponent<InputRuntimeView>(),new UIInputPolicy(true,true,emergency)));
        internal void Neutral(){((IUIInputDriver)Module).Gate.AllReleased=()=>true;Module.Process();}
    }
    [Category("input_runtime")] public class InputArbitrationTests : InputArbitrationFixture
    {
        [Test] public void GateRegistration_PreservesScrollRectDragRouting()
        {
            var scroll=new GameObject("Scroll",typeof(RectTransform),typeof(UnityEngine.UI.ScrollRect));
            scroll.transform.SetParent(Scene.Spies[0].transform,false);
            var button=new GameObject("Button",typeof(RectTransform),typeof(UnityEngine.UI.Button));button.transform.SetParent(scroll.transform,false);
            Assert.That(ExecuteEvents.GetEventHandler<IDragHandler>(button),Is.EqualTo(scroll));
            Module.Process(); // Refresh newly created input targets.
            Assert.That(ExecuteEvents.GetEventHandler<IDragHandler>(button),Is.EqualTo(scroll),"Tracking must not replace ScrollRect's native drag target");
        }
        InputRuntimeView NewCandidateView(string name)
        {
            var go=new GameObject(name,typeof(RectTransform),typeof(CanvasGroup),typeof(InputEventSpy));
            go.transform.SetParent(Scene.Spies[0].transform.parent,false);return go.AddComponent<InputRuntimeView>();
        }
        [Test] public void ModalBarrier_OrdinaryOverlayCannotStealInput()
        {
            Arbiter.Commit(PrepareB());Neutral();
            var overlay=NewCandidateView("Overlay");
            var grant=Arbiter.Prepare(new UIInputCandidate(Scene.Gate.Contexts[1],new UISessionId(20),overlay,new UIInputPolicy(true,false)));
            Assert.That(grant.IsEligible,Is.False);Arbiter.Commit(grant);Neutral();
            Assert.That(Scene.Events.currentSelectedGameObject,Is.EqualTo(Scene.Spies[1].gameObject));
        }
        [Test] public void StackedModals_CloseTopRestoresPreviousModalAndFocus()
        {
            Arbiter.Commit(PrepareB());Neutral();
            var middle=NewCandidateView("Middle");var top=NewCandidateView("Top");
            Arbiter.Commit(Arbiter.Prepare(new UIInputCandidate(Scene.Gate.Contexts[1],new UISessionId(20),middle,new UIInputPolicy(true,true))));Neutral();
            Arbiter.Commit(Arbiter.Prepare(new UIInputCandidate(Scene.Gate.Contexts[1],new UISessionId(21),top,new UIInputPolicy(true,true))));Neutral();
            Arbiter.Revoke(Scene.Gate.Contexts[1],new UISessionId(21));Neutral();
            Assert.That(Scene.Events.currentSelectedGameObject,Is.EqualTo(middle.gameObject));
            Arbiter.Revoke(Scene.Gate.Contexts[1],new UISessionId(20));Neutral();
            Assert.That(Scene.Events.currentSelectedGameObject,Is.EqualTo(Scene.Spies[1].gameObject));
        }
        [Test] public void RevokedTransitionOwner_RetainsBarrierUntilLeaseRelease()
        {
            using(Arbiter.AcquireLock(Scene.Gate.Contexts[0]))
            {
                Arbiter.Revoke(Scene.Gate.Contexts[0],Scene.Gate.Sessions[0]);var grant=PrepareB();
                Assert.That(grant.IsEligible,Is.False);Arbiter.Commit(grant);Neutral();Assert.That(Scene.Events.currentSelectedGameObject,Is.Null);
            }
            Neutral();Assert.That(Scene.Events.currentSelectedGameObject,Is.EqualTo(Scene.Spies[1].gameObject));
        }
        [Test] public void SecondArbiterForSameDomain_RegisterRejected()
        {
            Assert.Throws<InvalidOperationException>(()=>new UIInputArbiter((UIInputDomain)Scene.Gate.Domain));
            using(Arbiter.AcquireLock(Scene.Gate.Contexts[0]))Assert.That(Scene.Events.currentSelectedGameObject,Is.Null);
            Neutral();Assert.That(Scene.Events.currentSelectedGameObject,Is.EqualTo(Scene.Spies[0].gameObject));
        }
        [Test] public void TransitionOwner_RetainsBarrier()
        {
            using(Arbiter.AcquireLock(Scene.Gate.Contexts[0]))
            {
                var grant=PrepareB();Assert.That(grant.IsEligible,Is.False);Arbiter.Commit(grant);Neutral();
                Assert.That(Scene.Events.currentSelectedGameObject,Is.Null);
                Assert.That(Scene.Root.GetComponentsInChildren<UIModalMask>().Length,Is.EqualTo(1));
            }
            Neutral();Assert.That(Scene.Events.currentSelectedGameObject,Is.EqualTo(Scene.Spies[1].gameObject));
        }
        [Test] public void EmergencyTransfer_OldFocusNotRestored()
        {
            var old=Arbiter.AcquireLock(Scene.Gate.Contexts[0]);Arbiter.Commit(PrepareB(true));Neutral();
            Assert.That(Scene.Events.currentSelectedGameObject,Is.EqualTo(Scene.Spies[1].gameObject));
            old.Dispose();Neutral();Assert.That(Scene.Events.currentSelectedGameObject,Is.EqualTo(Scene.Spies[1].gameObject));
        }
        [Test] public void Unregister_ReleasesAllLockLeases_AndRestoresNextOwner()
        {
            var id=new UIContextId(Guid.NewGuid());var reg=Arbiter.Register(id,new UIInputRegistration(10,null,null));
            var candidate=new UIInputCandidate(id,new UISessionId(100),Scene.Spies[1].GetComponent<InputRuntimeView>(),new UIInputPolicy(true,true,true));
            Arbiter.Revoke(Scene.Gate.Contexts[1],Scene.Gate.Sessions[1]);Arbiter.Commit(Arbiter.Prepare(candidate));
            var first=Arbiter.AcquireLock(id);var second=Arbiter.AcquireLock(id);reg.Dispose();first.Dispose();second.Dispose();Neutral();
            Assert.That(Scene.Events.currentSelectedGameObject,Is.EqualTo(Scene.Spies[0].gameObject));
        }
        [Test] public void StaleGrant_IsRejectedWithoutStealingOwner()
        {
            var stale=PrepareB(true);using(Arbiter.AcquireLock(Scene.Gate.Contexts[0]))
                Assert.Throws<InvalidOperationException>(()=>Arbiter.Commit(stale));
            Neutral();Assert.That(Scene.Events.currentSelectedGameObject,Is.EqualTo(Scene.Spies[0].gameObject));
        }
        [Test] public void ModalUnderlay_CannotSubmit()
        {
            Arbiter.Commit(PrepareB());Neutral();
            Assert.That(Scene.Spies[0].GetComponents<CanvasGroup>().Last().interactable,Is.False);
            Assert.That(Scene.Spies[1].GetComponents<CanvasGroup>().Last().interactable,Is.True);
            Assert.That(Scene.Events.currentSelectedGameObject,Is.EqualTo(Scene.Spies[1].gameObject));
            Assert.That(Scene.Events.enabled,Is.False); // Fixture deliberately advances Process manually.
        }
        [Test] public void MaskAndCancel_RouteDistinctReasons_AndLockedMaskCannotClose()
        {
            UICloseReason? reason=null;var id=new UIContextId(Guid.NewGuid());
            using(var reg=Arbiter.Register(id,new UIInputRegistration(10,null,(s,r)=>reason=r)))
            {
                Arbiter.Revoke(Scene.Gate.Contexts[1],Scene.Gate.Sessions[1]);
                Arbiter.Commit(Arbiter.Prepare(new UIInputCandidate(id,new UISessionId(99),Scene.Spies[1].GetComponent<InputRuntimeView>(),new UIInputPolicy(true,true,true))));
                var mask=Scene.Root.GetComponentInChildren<UIModalMask>();
                mask.OnPointerClick(new PointerEventData(Scene.Events));Assert.That(reason,Is.EqualTo(UICloseReason.Mask));
                reason=null;using(Arbiter.AcquireLock(id))mask.OnPointerClick(new PointerEventData(Scene.Events));Assert.That(reason,Is.Null);
                Neutral();Scene.Spies[1].GetComponent<UIInputNode>().OnCancel(new BaseEventData(Scene.Events));Assert.That(reason,Is.EqualTo(UICloseReason.Back));
            }
        }
    }
}
