using UnityEngine;
namespace Wulato.UIKit.Tests.Input
{
    // Test input intents for the package's real-driver acceptance scenarios.
    public struct RawInput
    {
        public bool Pointer,Touch,Submit,Cancel,Unrelated,RightPointer,MiddlePointer;
        public Vector2 Position,Move;
    }
}
