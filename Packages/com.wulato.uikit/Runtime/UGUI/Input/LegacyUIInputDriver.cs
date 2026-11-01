using System;
using UnityEngine;
using UnityEngine.EventSystems;
namespace Wulato.UIKit
{
    public class LegacyUIInputDriver : StandaloneInputModule, IUIInputDriver
    {
        public UIInputGate Gate {get;set;}
        /// <summary>Optional held-state provider when a custom BaseInput supplies synthetic input.</summary>
        public Func<bool> ReleaseState {get;set;}
        public override void Process() { if(Gate==null)base.Process();else Gate.Process(()=>base.Process()); }
        public void InvalidatePointers() {foreach(var pointer in m_PointerData.Values)UIInputGate.InvalidatePointerRaycast(pointer);}
        public void ResetForGate() => DeactivateModule();
        public bool AllReleased()
        {
            if(ReleaseState!=null)return ReleaseState();
            return !input.GetMouseButton(0)&&!input.GetMouseButton(1)&&!input.GetMouseButton(2)&&input.touchCount==0
                &&input.GetAxisRaw(horizontalAxis)==0&&input.GetAxisRaw(verticalAxis)==0
                &&!Input.GetButton(submitButton)&&!Input.GetButton(cancelButton);
        }
    }
}
