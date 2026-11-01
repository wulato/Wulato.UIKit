using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
namespace Wulato.UIKit
{
    public sealed class UIInputGate : MonoBehaviour
    {
        internal EventSystem EventSystem;
        readonly List<UIViewBase> views=new List<UIViewBase>();
        readonly Dictionary<UIViewBase,CanvasGroup> groups=new Dictionary<UIViewBase,CanvasGroup>();
        readonly Dictionary<UIViewBase,GameObject> selections=new Dictionary<UIViewBase,GameObject>();
        readonly List<UIInputNode> nodes=new List<UIInputNode>();
        readonly Dictionary<UIViewBase,(bool added,bool interactable,bool raycasts)> original=new Dictionary<UIViewBase,(bool,bool,bool)>();
        readonly HashSet<CanvasGroup> ownedGroups=new HashSet<CanvasGroup>();
        readonly List<UIViewBase> pendingRemovals=new List<UIViewBase>();
        UIViewBase owner;
        bool initialized,released;
        internal Action Cancel;
        internal Action ResetModule;
        internal Action InvalidateCachedPointers;
        internal Func<bool> AllReleased;
        
        bool locked,quarantined,processing,resetPending,revoking;
        readonly HashSet<PointerEventData> inFlight=new HashSet<PointerEventData>();
        bool Enabled => !released;
        internal void Initialize(EventSystem events,IUIInputDriver adapter)
        { EventSystem=events;ResetModule=adapter.ResetForGate;AllReleased=adapter.AllReleased;InvalidateCachedPointers=adapter.InvalidatePointers; }
        internal void Add(UIViewBase view)
        {
            pendingRemovals.Remove(view);
            if(groups.ContainsKey(view))return;
            ownedGroups.RemoveWhere(g=>!g);nodes.RemoveAll(n=>!n);
            var group=view.GetComponent<CanvasGroup>();
            if(!group){group=view.gameObject.AddComponent<CanvasGroup>();ownedGroups.Add(group);}
            bool added=ownedGroups.Contains(group);
            original.Add(view,(added,group.interactable,group.blocksRaycasts));
            views.Add(view);groups.Add(view,group);RefreshNodes();ApplyNodes();
        }
        internal void Remove(UIViewBase view)
        {
            if(!groups.TryGetValue(view,out var group))return;
            if(processing){if(!pendingRemovals.Contains(view))pendingRemovals.Add(view);return;}
            var saved=original[view];original.Remove(view);
            if(group){group.interactable=saved.interactable;group.blocksRaycasts=saved.raycasts;}
            groups.Remove(view);views.Remove(view);selections.Remove(view);
            foreach(var node in nodes)if(node&&node.transform.IsChildOf(view.transform)){node.Gate=null;node.Presses.Clear();}
        }
        void RefreshNodes()
        {
            foreach(var view in views)
            {
                if(!view)continue;
                foreach(var t in view.GetComponentsInChildren<Transform>(true))
                {
                    bool pointer=false,drag=false,cancel=false;
                    foreach(var c in t.GetComponents<MonoBehaviour>())
                    {
                        if(c is UIInputPointerTracker||c is UIInputDragTracker||c is UIInputCancelTracker)continue;
                        pointer|=c is IPointerDownHandler;drag|=c is IDragHandler;cancel|=c is ICancelHandler||c is ISelectHandler;
                    }
                    if(!pointer&&!drag&&!cancel)continue;
                    var node=t.GetComponent<UIInputNode>();
                    if(!node)node=t.gameObject.AddComponent<UIInputNode>();
                    if(node.Gate!=null&&node.Gate!=this)throw new InvalidOperationException("Input target belongs to another domain.");
                    if(!nodes.Contains(node))nodes.Add(node);node.Gate=this;
                    if(pointer){var tracker=t.GetComponent<UIInputPointerTracker>();if(!tracker)tracker=t.gameObject.AddComponent<UIInputPointerTracker>();tracker.Node=node;}
                    if(drag){var tracker=t.GetComponent<UIInputDragTracker>();if(!tracker)tracker=t.gameObject.AddComponent<UIInputDragTracker>();tracker.Node=node;}
                    if(cancel){var tracker=t.GetComponent<UIInputCancelTracker>();if(!tracker)tracker=t.gameObject.AddComponent<UIInputCancelTracker>();tracker.Node=node;}
                }
            }
        }
        internal void SetOwner(UIViewBase value,bool isLocked,Action cancel)
        {
            if(initialized&&(owner!=value||locked!=isLocked))Revoke();
            owner=value;locked=isLocked;Cancel=cancel;initialized=initialized||value!=null;ApplyNodes();
            if(owner&&!locked&&!quarantined&&!IsOwned(EventSystem.currentSelectedGameObject))RestoreSelection();
        }
        internal void Release()
        {
            if(released)return;
            Revoke();released=true;
            foreach(var view in views.ToArray())Remove(view);
            foreach(var group in ownedGroups)if(group)UnityEngine.Object.Destroy(group);
            ownedGroups.Clear();
            foreach(var node in nodes)if(node)
            {
                foreach(var tracker in node.GetComponents<UIInputPointerTracker>())UnityEngine.Object.Destroy(tracker);
                foreach(var tracker in node.GetComponents<UIInputDragTracker>())UnityEngine.Object.Destroy(tracker);
                foreach(var tracker in node.GetComponents<UIInputCancelTracker>())UnityEngine.Object.Destroy(tracker);
                UnityEngine.Object.Destroy(node);
            }
            nodes.Clear();owner=null;Cancel=null;
        }
        bool IsOwned(GameObject target) => owner&&target&&target.activeInHierarchy&&(target.transform==owner.transform||target.transform.IsChildOf(owner.transform));
        void RestoreSelection()
        {
            if(!owner)return;
            selections.TryGetValue(owner,out var target);
            if(!IsOwned(target))
            {
                target=null;
                foreach(var selectable in owner.GetComponentsInChildren<UnityEngine.UI.Selectable>())
                    if(selectable.IsInteractable()){target=selectable.gameObject;break;}
                if(target==null&&owner.GetComponent<ISelectHandler>()!=null)target=owner.gameObject;
            }
            if(IsOwned(target))EventSystem.SetSelectedGameObject(target);
        }
        public void Process(Action process)
        {
            if(released)return;RefreshNodes();
            if(!AllowProcessing())return;
            if(EventSystem.currentSelectedGameObject&&!IsOwned(EventSystem.currentSelectedGameObject))EventSystem.SetSelectedGameObject(null);
            bool navigation=EventSystem.sendNavigationEvents;
            processing=true;
            try {process();}
            finally
            {
                processing=false;EventSystem.sendNavigationEvents=navigation;
                try
                {
                    if(resetPending)
                    {
                        resetPending=false;
                        // Native releases remove their snapshots first. Cancel only presses
                        // that remain, avoiding a second up for later buttons in this Process.
                        try {CancelRemainingPresses();} finally {ResetModule();}
                    }
                }
                finally {inFlight.Clear();foreach(var view in pendingRemovals.ToArray())Remove(view);pendingRemovals.Clear();}
            }
        }
        public void ObserveInFlight(PointerEventData e) {if(processing && !revoking){inFlight.Add(e);if(quarantined)InvalidatePointerRaycast(e);}}
        internal void PointerReleased(PointerEventData e)
        {
            int key=e.pointerId*4+(int)e.button;
            foreach(var node in nodes)if(node&&node.Presses.TryGetValue(key,out var snapshot))snapshot.pointerPress=null;
        }
        void ApplyNodes()
        {
            foreach(var view in views)
            {
                if(!view)continue;
                var group=groups[view];var saved=original[view];
                bool allow=view==owner&&!locked&&!quarantined;
                group.blocksRaycasts=allow&&saved.raycasts;group.interactable=allow&&saved.interactable;
            }
        }
        void Revoke()
        {
            if(revoking)return;
            revoking=true;quarantined=true;
            if(IsOwned(EventSystem.currentSelectedGameObject))selections[owner]=EventSystem.currentSelectedGameObject;
            try
            {
                // Keep native cleanup eligible to deliver its one up/endDrag. Any
                // remaining snapshots are canceled after the entire Process returns.
                if(!processing)CancelRemainingPresses();
                InvalidateCachedPointers?.Invoke();
                // References are used only within the current Process call. In particular,
                // Input System caches isClick before pointerUp, so clear pointerClick itself.
                foreach(var e in inFlight)
                {
                    InvalidatePointerRaycast(e);
                    e.pointerPress=null;e.rawPointerPress=null;
                    e.pointerDrag=null;e.dragging=false;
                }
                EventSystem.SetSelectedGameObject(null);
                if(processing) {EventSystem.sendNavigationEvents=false;resetPending=true;}
                else ResetModule();
            }
            finally {revoking=false;}
        }
        public static void InvalidatePointerRaycast(PointerEventData e)
        {
            e.eligibleForClick=false;e.pointerClick=null;
            e.pointerCurrentRaycast=default;e.delta=Vector2.zero;
        }
        void CancelRemainingPresses()
        {
            bool previous=revoking;revoking=true;
            try
            {
                foreach(var node in nodes.ToArray())
                {
                    if(!node)continue;
                    var snapshots=new List<PointerEventData>(node.Presses.Values);node.Presses.Clear();
                    foreach(var e in snapshots)
                    {
                        if(e.pointerPress)ExecuteEvents.Execute(e.pointerPress,e,ExecuteEvents.pointerUpHandler);
                        if(e.dragging&&e.pointerDrag)ExecuteEvents.Execute(e.pointerDrag,e,ExecuteEvents.endDragHandler);
                    }
                }
            }
            finally {revoking=previous;}
        }
        public bool AllowProcessing()
        {
            if(!Enabled)return true;
            if(locked||!owner)return false;
            if(quarantined)
            {
                if(!AllReleased())return false;
                ResetModule();quarantined=false;ApplyNodes();
                RestoreSelection();
                return false;
            }
            return true;
        }
    }
}
