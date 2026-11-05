using UnityEngine;
using UnityEngine.EventSystems;
namespace Wulato.UIKit
{
    internal sealed class UIInputCancelTracker : MonoBehaviour,ICancelHandler
    {
        internal UIInputNode Node;
        public void OnCancel(BaseEventData e)=>Node?.OnCancel(e);
    }
}
