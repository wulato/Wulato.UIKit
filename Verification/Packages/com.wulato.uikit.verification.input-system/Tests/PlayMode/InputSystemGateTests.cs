#if UIKIT_VERIFY_INPUT_SYSTEM
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using Wulato.UIKit.Verification.Tests;
namespace Wulato.UIKit.Verification.InputSystem.Tests
{
    public class InputSystemGateTests : InputGateScenarios
    {
        InputTestFixture fixture;
        Mouse mouse;Keyboard keyboard;Gamepad pad;Touchscreen touch;
        RawInput previous;
        protected override void Configure()
        {
            fixture=new InputTestFixture();fixture.Setup();
            mouse=UnityEngine.InputSystem.InputSystem.AddDevice<Mouse>();keyboard=UnityEngine.InputSystem.InputSystem.AddDevice<Keyboard>();pad=UnityEngine.InputSystem.InputSystem.AddDevice<Gamepad>();touch=UnityEngine.InputSystem.InputSystem.AddDevice<Touchscreen>();
            var m=Scene.Events.gameObject.AddComponent<InputSystemProbeModule>();m.Gate=Scene.Gate;Module=m;
            Scene.Gate.ResetModule=m.ResetForGate;Scene.Gate.AllReleased=m.AllReleased;
        }
        protected override void Inject(RawInput state)
        {
            if(!state.Touch && !previous.Touch) UnityEngine.InputSystem.InputSystem.QueueStateEvent(mouse,new MouseState {position=state.Position}.WithButton(MouseButton.Left,state.Pointer));
            var keys=new List<Key>();if(state.Submit)keys.Add(Key.Enter);if(state.Cancel)keys.Add(Key.Escape);
            UnityEngine.InputSystem.InputSystem.QueueStateEvent(keyboard,new KeyboardState(keys.ToArray()));
            UnityEngine.InputSystem.InputSystem.QueueStateEvent(pad,new GamepadState {leftStick=state.Move});
            if(state.Touch && !previous.Touch)fixture.BeginTouch(1,state.Position,queueEventOnly:true,screen:touch);
            else if(state.Touch)fixture.MoveTouch(1,state.Position,queueEventOnly:true,screen:touch);
            else if(previous.Touch)fixture.EndTouch(1,state.Position,queueEventOnly:true,screen:touch);
            UnityEngine.InputSystem.InputSystem.Update();previous=state;
        }
        protected override void DisposeBackend() {if(fixture!=null)fixture.TearDown();}
    }
}
#endif
