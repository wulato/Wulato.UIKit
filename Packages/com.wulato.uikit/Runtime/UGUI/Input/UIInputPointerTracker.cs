using UnityEngine;
using UnityEngine.EventSystems;
namespace Wulato.UIKit
{
    // Only installed on native pointer-down targets; does not introduce a drag handler.
    internal sealed class UIInputPointerTracker : MonoBehaviour,IPointerDownHandler,IPointerUpHandler
    {
        internal UIInputNode Node;
        public void OnPointerDown(PointerEventData e)=>Node?.OnPointerDown(e);
        public void OnPointerUp(PointerEventData e)=>Node?.OnPointerUp(e);
    }
}
