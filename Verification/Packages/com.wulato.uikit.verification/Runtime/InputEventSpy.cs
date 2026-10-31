using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEngine.EventSystems;
namespace Wulato.UIKit.Verification
{
    public class InputEventSpy : MonoBehaviour, IPointerDownHandler, IPointerUpHandler, IPointerClickHandler,
        IBeginDragHandler, IDragHandler, IEndDragHandler, IDropHandler, IMoveHandler, ISubmitHandler, ICancelHandler, ISelectHandler, IDeselectHandler
    {
        public readonly List<string> Events=new List<string>();
        public EventSystem EventsSystem;
        public readonly Dictionary<int,PointerEventData> ActivePointers=new Dictionary<int,PointerEventData>();
        int Key(PointerEventData e) {return e.pointerId*4+(int)e.button;}
        PointerEventData Snapshot(PointerEventData e)
        {
            return new PointerEventData(EventsSystem) {pointerId=e.pointerId,button=e.button,position=e.position,pressPosition=e.position,pointerPress=gameObject,rawPointerPress=gameObject,pointerDrag=gameObject,eligibleForClick=false};
        }
        public int Count(string name) { return Events.FindAll(e=>e==name).Count; }
        public void OnPointerDown(PointerEventData e) { Events.Add("down");ActivePointers[Key(e)]=Snapshot(e); }
        public void OnPointerUp(PointerEventData e) { Events.Add("up"); if(!e.dragging)ActivePointers.Remove(Key(e)); }
        public void OnPointerClick(PointerEventData e) { Events.Add("click"); }
        public void OnBeginDrag(PointerEventData e) { Events.Add("beginDrag");if(ActivePointers.TryGetValue(Key(e),out var p))p.dragging=true; }
        public void OnDrag(PointerEventData e) { Events.Add("drag");if(ActivePointers.TryGetValue(Key(e),out var p))p.position=e.position; }
        public void OnEndDrag(PointerEventData e) { Events.Add("endDrag");ActivePointers.Remove(Key(e)); }
        public void OnDrop(PointerEventData e) { Events.Add("drop"); }
        public void OnMove(AxisEventData e) { Events.Add("move"); }
        public void OnSubmit(BaseEventData e) { Events.Add("submit"); }
        public void OnCancel(BaseEventData e) { Events.Add("cancel"); }
        public void OnSelect(BaseEventData e) { Events.Add("select"); }
        public void OnDeselect(BaseEventData e) { Events.Add("deselect"); }
        public void Save(string test)
        {
            var d=Environment.GetEnvironmentVariable("UIKIT_EVIDENCE_DIR"); if(string.IsNullOrEmpty(d))return;
            File.WriteAllText(Path.Combine(d,test+"-"+name+".trace.json"),JsonUtility.ToJson(new Trace {owner=name,events=Events.ToArray()},true));
        }
        [Serializable] class Trace {public string owner;public string[] events;}
    }
}
