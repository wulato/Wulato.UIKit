using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
namespace Wulato.UIKit.Verification
{
    public class InputGateProbe : MonoBehaviour
    {
        public EventSystem EventSystem;
        public InputEventSpy[] Owners;
        public Action ResetModule;
        public Func<bool> AllReleased;
        public int OwnerId;
        bool locked,quarantined,processing,resetPending,revoking;
        readonly HashSet<PointerEventData> inFlight=new HashSet<PointerEventData>();
        bool Enabled {get{return Environment.GetEnvironmentVariable("UIKIT_RED_INPUT")!="1";}}
        public void Initialize()
        {
            foreach(var owner in Owners)owner.GetComponent<InputGateNode>().Gate=this;
            ApplyNodes();
        }
        public void Process(Action process)
        {
            if(!AllowProcessing())return;
            bool navigation=EventSystem.sendNavigationEvents;
            processing=true;
            try {process();}
            finally
            {
                processing=false;inFlight.Clear();EventSystem.sendNavigationEvents=navigation;
                if(resetPending) {resetPending=false;ResetModule();}
            }
        }
        public void ObserveInFlight(PointerEventData e) {if(processing && !revoking)inFlight.Add(e);}
        public void SetLocked(bool value)
        {
            if(!Enabled || locked==value)return;
            locked=value;if(value)Revoke();ApplyNodes();
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
                if(!Owners[i])continue;
                var c=Owners[i].GetComponent<CanvasGroup>();
                c.blocksRaycasts=!locked&&!quarantined&&i==OwnerId;c.interactable=c.blocksRaycasts;
            }
        }
        void Revoke()
        {
            if(revoking)return;
            revoking=true;quarantined=true;
            try
            {
                // Snapshot cancellation notifies the old handlers exactly once.
                foreach(var owner in Owners)
                {
                    if(!owner)continue;
                    var node=owner.GetComponent<InputGateNode>();
                    var snapshots=new List<PointerEventData>(node.Presses.Values);node.Presses.Clear();
                    foreach(var e in snapshots)
                    {
                        if(e.pointerPress)ExecuteEvents.Execute(e.pointerPress,e,ExecuteEvents.pointerUpHandler);
                        if(e.dragging&&e.pointerDrag)ExecuteEvents.Execute(e.pointerDrag,e,ExecuteEvents.endDragHandler);
                    }
                }
                // References are used only within the current Process call. In particular,
                // Input System caches isClick before pointerUp, so clear pointerClick itself.
                foreach(var e in inFlight)
                {
                    e.eligibleForClick=false;e.pointerClick=null;e.pointerPress=null;e.rawPointerPress=null;
                    e.pointerDrag=null;e.dragging=false;
                }
                EventSystem.SetSelectedGameObject(null);
                if(processing) {EventSystem.sendNavigationEvents=false;resetPending=true;}
                else ResetModule();
            }
            finally {revoking=false;}
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
                if(target&&target.gameObject.activeInHierarchy)EventSystem.SetSelectedGameObject(target.gameObject);
                return false;
            }
            return true;
        }
    }
}
