using UnityEngine;
using UnityEngine.EventSystems;
namespace Wulato.UIKit
{
    // Only installed on native drag targets, preserving ScrollRect parent routing.
    internal sealed class UIInputDragTracker : MonoBehaviour,IBeginDragHandler,IDragHandler,IEndDragHandler
    {
        internal UIInputNode Node;
        public void OnBeginDrag(PointerEventData e)=>Node?.OnBeginDrag(e);
        public void OnDrag(PointerEventData e)=>Node?.OnDrag(e);
        public void OnEndDrag(PointerEventData e)=>Node?.OnEndDrag(e);
    }
}
