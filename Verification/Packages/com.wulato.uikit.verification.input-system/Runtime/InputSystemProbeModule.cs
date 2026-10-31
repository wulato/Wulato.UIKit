using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Controls;
using UnityEngine.InputSystem.UI;
namespace Wulato.UIKit.Verification.InputSystem
{
    public class InputSystemProbeModule : InputSystemUIInputModule
    {
        public InputGateProbe Gate;
        public override void Process() {if(Gate==null || Gate.AllowProcessing())base.Process();}
        public void ResetForGate() {DeactivateModule();enabled=false;enabled=true;}
        public bool AllReleased()
        {
            foreach(var device in UnityEngine.InputSystem.InputSystem.devices)
            foreach(var control in device.allControls)
            {
                if(control is ButtonControl b && b.isPressed)return false;
                if(control is StickControl s && s.ReadValue().sqrMagnitude>0.01f)return false;
            }
            return true;
        }
    }
}
