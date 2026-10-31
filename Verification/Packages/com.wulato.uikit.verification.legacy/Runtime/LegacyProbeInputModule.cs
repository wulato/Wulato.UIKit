using UnityEngine;
using UnityEngine.EventSystems;
namespace Wulato.UIKit.Verification.Legacy
{
    // BaseInput only exposes button edges. Its injection provider also supplies held state.
    public interface ILegacyReleaseState {bool AllReleased();}
    public class LegacyProbeInputModule : StandaloneInputModule
    {
        public InputGateProbe Gate;
        public override void Process() {if(Gate==null || Gate.AllowProcessing())base.Process();}
        public void ResetForGate() { DeactivateModule(); }
        public bool AllReleased()
        {
            if(inputOverride is ILegacyReleaseState source)return source.AllReleased();
            return !input.GetMouseButton(0) && !input.GetMouseButton(1) && !input.GetMouseButton(2)
                && input.touchCount==0 && input.GetAxisRaw(horizontalAxis)==0 && input.GetAxisRaw(verticalAxis)==0
                && !Input.GetButton(submitButton) && !Input.GetButton(cancelButton);
        }
    }
}
