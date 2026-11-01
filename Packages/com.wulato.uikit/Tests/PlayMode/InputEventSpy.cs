using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEngine.EventSystems;
namespace Wulato.UIKit.Tests.Input
{
    // Pure observation: no gate checks, event filtering, or cancellation state.
    public class InputEventSpy : MonoBehaviour, IPointerDownHandler, IPointerUpHandler, IPointerClickHandler,
        IBeginDragHandler, IDragHandler, IEndDragHandler, IDropHandler, IMoveHandler, ISubmitHandler, ICancelHandler, ISelectHandler, IDeselectHandler
    {
        public readonly List<string> Events=new List<string>();
        public readonly List<string> PointerEvents=new List<string>();
        public int Count(string name) {return Events.FindAll(e=>e==name).Count;}
        public void OnPointerDown(PointerEventData e) {Events.Add("down");PointerEvents.Add("down:"+e.button);}
        public void OnPointerUp(PointerEventData e) {Events.Add("up");PointerEvents.Add("up:"+e.button);}
        public void OnPointerClick(PointerEventData e) {Events.Add("click");PointerEvents.Add("click:"+e.button);}
        public void OnBeginDrag(PointerEventData e) {Events.Add("beginDrag");}
        public void OnDrag(PointerEventData e) {Events.Add("drag");}
        public void OnEndDrag(PointerEventData e) {Events.Add("endDrag");}
        public void OnDrop(PointerEventData e) {Events.Add("drop");}
        public void OnMove(AxisEventData e) {Events.Add("move");}
        public void OnSubmit(BaseEventData e) {Events.Add("submit");}
        public void OnCancel(BaseEventData e) {Events.Add("cancel");}
        public void OnSelect(BaseEventData e) {Events.Add("select");}
        public void OnDeselect(BaseEventData e) {Events.Add("deselect");}
        public void Save(string test)
        {
            var d=Environment.GetEnvironmentVariable("UIKIT_EVIDENCE_DIR");if(string.IsNullOrEmpty(d))return;
            File.WriteAllText(Path.Combine(d,test+"-"+name+".trace.json"),JsonUtility.ToJson(new Trace {owner=name,events=Events.ToArray(),pointerEvents=PointerEvents.ToArray()},true));
        }
        [Serializable] class Trace {public string owner;public string[] events;public string[] pointerEvents;}
    }
}
