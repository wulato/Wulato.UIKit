using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
namespace Wulato.UIKit
{
    // Framework-owned cancellation bookkeeping. Event spies remain unfiltered observers.
    internal sealed class UIInputNode : MonoBehaviour
    {
        public UIInputGate Gate;
        public readonly Dictionary<int,PointerEventData> Presses=new Dictionary<int,PointerEventData>();
        static int Key(PointerEventData e) {return e.pointerId*4+(int)e.button;}
        void Observe(PointerEventData e) {Gate?.ObserveInFlight(e);}
        public void OnCancel(BaseEventData e) {Gate?.Cancel?.Invoke();}
        public void OnPointerDown(PointerEventData e)
        {
            if(Gate==null)return;
            Presses[Key(e)]=new PointerEventData(Gate.EventSystem) {pointerId=e.pointerId,button=e.button,position=e.position,
                pressPosition=e.position,pointerPress=gameObject,rawPointerPress=gameObject,pointerDrag=ExecuteEvents.GetEventHandler<IDragHandler>(gameObject),eligibleForClick=false};
            Observe(e);
        }
        public void OnPointerUp(PointerEventData e)
        {
            Observe(e);Gate?.PointerReleased(e);
            if(Presses.TryGetValue(Key(e),out var snapshot))snapshot.pointerPress=null;
            if(!e.dragging)Presses.Remove(Key(e));
        }
        public void OnBeginDrag(PointerEventData e)
        {
            if(Gate==null)return;
            if(!Presses.TryGetValue(Key(e),out var p))
                Presses[Key(e)]=p=new PointerEventData(Gate.EventSystem){pointerId=e.pointerId,button=e.button,position=e.position,pointerPress=e.pointerPress,pointerDrag=gameObject};
            p.dragging=true;p.pointerDrag=gameObject;Observe(e);
        }
        public void OnDrag(PointerEventData e) {Observe(e);if(Presses.TryGetValue(Key(e),out var p))p.position=e.position;}
        public void OnEndDrag(PointerEventData e) {Observe(e);Presses.Remove(Key(e));}
    }
}
