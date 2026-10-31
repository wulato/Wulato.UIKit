using System;
using UnityEngine;
using UnityEngine.EventSystems;
namespace Wulato.UIKit.Verification
{
    // Verification prototype only: owns its nodes, never reaches into module private state.
    public class InputGateProbe : MonoBehaviour
    {
        public EventSystem EventSystem;
        public InputEventSpy[] Owners;
        public Action ResetModule;
        public Func<bool> AllReleased;
        public int OwnerId;
        bool locked, quarantined;
        bool Enabled { get {return Environment.GetEnvironmentVariable("UIKIT_RED_INPUT")!="1";} }
        public void SetLocked(bool value)
        {
            if(!Enabled)return;
            if(locked==value)return;
            locked=value;
            if(value) Revoke();
            ApplyNodes();
        }
        public void TransferOwner(int id)
        {
            if(!Enabled) {OwnerId=id;EventSystem.SetSelectedGameObject(Owners[id].gameObject);return;}
            Revoke();OwnerId=id;ApplyNodes();
        }
        void ApplyNodes()
        {
            for(int i=0;i<Owners.Length;i++)
            {
                var c=Owners[i].GetComponent<CanvasGroup>();
                c.blocksRaycasts=!locked && !quarantined && i==OwnerId;
                c.interactable=c.blocksRaycasts;
            }
        }
        void Revoke()
        {
            // These are normal cancellation notifications, not synthetic input injection.
            foreach(var owner in Owners)
            foreach(var e in new System.Collections.Generic.List<PointerEventData>(owner.ActivePointers.Values))
            {
                if(e.pointerPress) ExecuteEvents.Execute(e.pointerPress,e,ExecuteEvents.pointerUpHandler);
                if(e.dragging && e.pointerDrag) ExecuteEvents.Execute(e.pointerDrag,e,ExecuteEvents.endDragHandler);
                e.eligibleForClick=false;e.pointerPress=null;e.rawPointerPress=null;e.pointerDrag=null;e.dragging=false;
                owner.ActivePointers.Remove(e.pointerId*4+(int)e.button);
            }
            EventSystem.SetSelectedGameObject(null);
            ResetModule();quarantined=true;
        }
        public bool AllowProcessing()
        {
            if(!Enabled)return true;
            if(locked)return false;
            if(quarantined)
            {
                if(!AllReleased())return false;
                ResetModule();quarantined=false;ApplyNodes();
                var target=Owners[OwnerId];
                if(target && target.gameObject.activeInHierarchy)EventSystem.SetSelectedGameObject(target.gameObject);
                return false; // Neutral frame drains the previous epoch.
            }
            return true;
        }
    }
}
