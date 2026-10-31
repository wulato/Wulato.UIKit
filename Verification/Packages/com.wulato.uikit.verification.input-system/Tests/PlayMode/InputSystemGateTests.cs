#if UIKIT_VERIFY_INPUT_SYSTEM
using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine.TestTools;
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
            var keys=new List<Key>();if(state.Submit)keys.Add(Key.Enter);if(state.Cancel)keys.Add(Key.Escape);if(state.Unrelated)keys.Add(Key.Z);
            UnityEngine.InputSystem.InputSystem.QueueStateEvent(keyboard,new KeyboardState(keys.ToArray()));
            UnityEngine.InputSystem.InputSystem.QueueStateEvent(pad,new GamepadState {leftStick=state.Move});
            if(state.Touch && !previous.Touch)fixture.BeginTouch(1,state.Position,queueEventOnly:true,screen:touch);
            else if(state.Touch)fixture.MoveTouch(1,state.Position,queueEventOnly:true,screen:touch);
            else if(previous.Touch)fixture.EndTouch(1,state.Position,queueEventOnly:true,screen:touch);
            UnityEngine.InputSystem.InputSystem.Update();previous=state;
        }
        [UnityTest,Category("input_preflight")] public IEnumerator UnboundKey_DoesNotBlockFreshInputAfterUnlock()
        {
            Scene.Gate.SetLocked(true);yield return Step(new RawInput {Unrelated=true,Position=Scene.Point(0)});
            Scene.Gate.SetLocked(false);yield return Step(new RawInput {Unrelated=true,Position=Scene.Point(0)});
            yield return Step(new RawInput {Unrelated=true,Pointer=true,Position=Scene.Point(0)});
            yield return Step(new RawInput {Unrelated=true,Position=Scene.Point(0)});
            Assert.That(Scene.Spies[0].Count("click"),Is.EqualTo(1));
            Assert.That(keyboard.zKey.isPressed,Is.True,"Unrelated gameplay key must remain held throughout");
        }
        protected override void DisposeBackend() {if(fixture!=null)fixture.TearDown();}
    }
}
#endif
