using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Controls;
using UnityEngine.InputSystem.UI;
namespace Wulato.UIKit.Verification.InputSystem
{
    public class InputSystemProbeModule : InputSystemUIInputModule
    {
        public InputGateProbe Gate;
        public override void Process() {if(Gate==null)base.Process();else Gate.Process(()=>base.Process());}
        public void ResetForGate() {DeactivateModule();enabled=false;enabled=true;}
        public bool AllReleased()
        {
            // Observe resolved UI bindings, not unrelated gameplay buttons/devices.
            foreach(var reference in new[]{leftClick,rightClick,middleClick,submit,cancel,move})
            {
                if(reference==null || reference.action==null)continue;
                foreach(var control in reference.action.controls)
                {
                    if(control is ButtonControl b && b.isPressed)return false;
                    if(control is Vector2Control v && v.ReadValue().sqrMagnitude>0.01f)return false;
                    if(control is AxisControl a && !(control is ButtonControl) && UnityEngine.Mathf.Abs(a.ReadValue())>0.1f)return false;
                }
            }
            return true;
        }
    }
}
