#if UIKIT_VERIFY_LEGACY
using UnityEngine;
using UnityEngine.EventSystems;
using Wulato.UIKit.Verification.Tests;
namespace Wulato.UIKit.Verification.Legacy.Tests
{
    public class LegacyRawInput : BaseInput, ILegacyReleaseState
    {
        public RawInput Current,Previous;
        public void Set(RawInput state) {Previous=Current;Current=state;}
        public bool AllReleased() {return !Current.Pointer&&!Current.Touch&&!Current.Submit&&!Current.Cancel&&Current.Move.sqrMagnitude==0;}
        public override bool mousePresent {get{return true;}}
        public override bool GetMouseButton(int b) {return b==0&&Current.Pointer;}
        public override bool GetMouseButtonDown(int b) {return b==0&&Current.Pointer&&!Previous.Pointer;}
        public override bool GetMouseButtonUp(int b) {return b==0&&!Current.Pointer&&Previous.Pointer;}
        public override Vector2 mousePosition {get{return Current.Position;}}
        public override Vector2 mouseScrollDelta {get{return Vector2.zero;}}
        public override bool touchSupported {get{return true;}}
        public override int touchCount {get{return Current.Touch||Previous.Touch?1:0;}}
        public override Touch GetTouch(int index) {return new Touch {fingerId=1,position=Current.Position,rawPosition=Current.Position,deltaPosition=Current.Position-Previous.Position,phase=!Current.Touch?TouchPhase.Ended:!Previous.Touch?TouchPhase.Began:TouchPhase.Moved,type=TouchType.Direct};}
        public override float GetAxisRaw(string axis) {return axis=="Horizontal"?Current.Move.x:Current.Move.y;}
        public override bool GetButtonDown(string b) {return b=="Submit"?Current.Submit&&!Previous.Submit:Current.Cancel&&!Previous.Cancel;}
    }
    public class LegacyInputGateTests : InputGateScenarios
    {
        LegacyRawInput raw;
        protected override void Configure()
        {
            raw=Scene.Events.gameObject.AddComponent<LegacyRawInput>();
            var m=Scene.Events.gameObject.AddComponent<LegacyProbeInputModule>();m.inputOverride=raw;m.forceModuleActive=true;m.Gate=Scene.Gate;Module=m;
            Scene.Gate.ResetModule=m.ResetForGate;Scene.Gate.AllReleased=m.AllReleased;
        }
        protected override void Inject(RawInput state) {raw.Set(state);}
    }
}
#endif
