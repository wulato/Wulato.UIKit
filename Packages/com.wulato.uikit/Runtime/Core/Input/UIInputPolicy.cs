using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;

namespace Wulato.UIKit
{
    /// <summary>Input qualification is independent of render order.</summary>
    public readonly struct UIInputPolicy
    {
        public bool TakesInput { get; }
        public bool BlocksLower { get; }
        public bool EmergencyPreemption { get; }
        public UIInputPolicy(bool takesInput,bool blocksLower,bool emergencyPreemption=false)
        { TakesInput=takesInput;BlocksLower=blocksLower;EmergencyPreemption=emergencyPreemption; }
    }
}
