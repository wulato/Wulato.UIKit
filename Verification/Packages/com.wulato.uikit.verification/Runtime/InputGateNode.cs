using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
namespace Wulato.UIKit.Verification
{
    // Framework-owned cancellation bookkeeping. Event spies remain unfiltered observers.
    public sealed class InputGateNode : MonoBehaviour, IPointerDownHandler, IPointerUpHandler,
        IBeginDragHandler, IDragHandler, IEndDragHandler
    {
        public InputGateProbe Gate;
        public readonly Dictionary<int,PointerEventData> Presses=new Dictionary<int,PointerEventData>();
        static int Key(PointerEventData e) {return e.pointerId*4+(int)e.button;}
        void Observe(PointerEventData e) {Gate.ObserveInFlight(e);}
        public void OnPointerDown(PointerEventData e)
        {
            Observe(e);
            Presses[Key(e)]=new PointerEventData(Gate.EventSystem) {pointerId=e.pointerId,button=e.button,position=e.position,
                pressPosition=e.position,pointerPress=gameObject,rawPointerPress=gameObject,pointerDrag=gameObject,eligibleForClick=false};
        }
        public void OnPointerUp(PointerEventData e)
        {
            Observe(e);
            if(Presses.TryGetValue(Key(e),out var snapshot))snapshot.pointerPress=null;
            if(!e.dragging)Presses.Remove(Key(e));
        }
        public void OnBeginDrag(PointerEventData e) {Observe(e);if(Presses.TryGetValue(Key(e),out var p))p.dragging=true;}
        public void OnDrag(PointerEventData e) {Observe(e);if(Presses.TryGetValue(Key(e),out var p))p.position=e.position;}
        public void OnEndDrag(PointerEventData e) {Observe(e);Presses.Remove(Key(e));}
    }
}
