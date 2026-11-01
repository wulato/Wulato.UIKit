#if UIKIT_VERIFY_INPUT_SYSTEM
using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine.TestTools;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.InputSystem.Controls;
using Wulato.UIKit.Verification.Tests;
using Wulato.UIKit.Tests.Input;
namespace Wulato.UIKit.InputSystem.Tests
{
    public class InputSystemArbitrationTests : RuntimeInputScenarios
    {
        InputTestFixture fixture;
        Mouse mouse;Keyboard keyboard;Gamepad pad;Touchscreen touch;
        RawInput previous;
        protected override void Configure()
        {
            fixture=new InputTestFixture();fixture.Setup();
            mouse=UnityEngine.InputSystem.InputSystem.AddDevice<Mouse>();keyboard=UnityEngine.InputSystem.InputSystem.AddDevice<Keyboard>();pad=UnityEngine.InputSystem.InputSystem.AddDevice<Gamepad>();touch=UnityEngine.InputSystem.InputSystem.AddDevice<Touchscreen>();
            Module=RuntimeInputIntents.AddDriver(Scene.Events,"Wulato.UIKit.InputSystem.InputSystemUIInputDriver");
        }
        protected override void Inject(RawInput state)
        {
            if(!state.Touch && !previous.Touch) UnityEngine.InputSystem.InputSystem.QueueStateEvent(mouse,new MouseState {position=state.Position}.WithButton(MouseButton.Left,state.Pointer).WithButton(MouseButton.Right,state.RightPointer).WithButton(MouseButton.Middle,state.MiddlePointer));
            var keys=new List<Key>();if(state.Submit)keys.Add(Key.Enter);if(state.Cancel)keys.Add(Key.Escape);if(state.Unrelated)keys.Add(Key.Z);
            UnityEngine.InputSystem.InputSystem.QueueStateEvent(keyboard,new KeyboardState(keys.ToArray()));
            UnityEngine.InputSystem.InputSystem.QueueStateEvent(pad,new GamepadState {leftStick=state.Move});
            if(state.Touch && !previous.Touch)fixture.BeginTouch(1,state.Position,queueEventOnly:true,screen:touch);
            else if(state.Touch)fixture.MoveTouch(1,state.Position,queueEventOnly:true,screen:touch);
            else if(previous.Touch)fixture.EndTouch(1,state.Position,queueEventOnly:true,screen:touch);
            UnityEngine.InputSystem.InputSystem.Update();previous=state;
        }
        [UnityTest,Category("input_runtime")] public IEnumerator UnboundKey_DoesNotBlockFreshInputAfterUnlock()
        {
            Scene.Gate.SetLocked(true);yield return Step(new RawInput {Unrelated=true,Position=Scene.Point(0)});
            Scene.Gate.SetLocked(false);yield return Step(new RawInput {Unrelated=true,Position=Scene.Point(0)});
            yield return Step(new RawInput {Unrelated=true,Pointer=true,Position=Scene.Point(0)});
            yield return Step(new RawInput {Unrelated=true,Position=Scene.Point(0)});
            Assert.That(Scene.Spies[0].Count("click"),Is.EqualTo(1));
            Assert.That(keyboard.zKey.isPressed,Is.True,"Unrelated gameplay key must remain held throughout");
        }
        IEnumerator PadStep(bool submit=false,bool cancel=false)
        {
            yield return null;
            UnityEngine.InputSystem.InputSystem.QueueStateEvent(pad,new GamepadState().WithButton(GamepadButton.South,submit).WithButton(GamepadButton.East,cancel));
            UnityEngine.InputSystem.InputSystem.Update();Module.Process();
        }
        [UnityTest,Category("input_runtime")] public IEnumerator GamepadSubmitCancel_HeldTransferRequiresNeutralThenFreshEdge()
        {
            Scene.Gate.SetLocked(true);yield return PadStep(submit:true);Scene.Gate.TransferOwner(1);Scene.Gate.SetLocked(false);
            yield return PadStep(submit:true);Assert.That(B.Count("submit"),Is.Zero);
            yield return PadStep();yield return PadStep(submit:true);yield return PadStep();yield return PadStep(cancel:true);yield return PadStep();
            Assert.That(A.Count("submit")+A.Count("cancel"),Is.Zero);Assert.That(B.Count("submit"),Is.EqualTo(1));Assert.That(B.Count("cancel"),Is.EqualTo(1));
        }
        protected override void DisposeBackend() {if(fixture!=null)fixture.TearDown();}
    }
}
#endif
