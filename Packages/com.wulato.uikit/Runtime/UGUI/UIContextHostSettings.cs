using System;
using UnityEngine;
namespace Wulato.UIKit
{
    [Serializable]
    public sealed class UIContextHostSettings
    {
        [Min(0)] public double loadTimeout=30,openTimeout=10,closeTimeout=2,enterTimeout=2,exitTimeout=2,shutdownBudget=5;
        [Min(0)] public int cacheCapacity=8;
        public UIDiagnosticsLevel diagnosticsLevel=UIDiagnosticsLevel.Basic;
        static TimeSpan Duration(double seconds)
        {
            if(double.IsNaN(seconds)||double.IsInfinity(seconds)||seconds<=0)throw new ArgumentOutOfRangeException(nameof(seconds),"Duration must be finite and positive.");
            return TimeSpan.FromSeconds(seconds);
        }
        public UIContextSettings ToSettings()
        {
            var value=new UIContextSettings{LoadTimeout=Duration(loadTimeout),OpenTimeout=Duration(openTimeout),CloseTimeout=Duration(closeTimeout),EnterTimeout=Duration(enterTimeout),ExitTimeout=Duration(exitTimeout),ShutdownBudget=Duration(shutdownBudget),CacheCapacity=cacheCapacity,DiagnosticsLevel=diagnosticsLevel};
            value.CopyValidated();return value;
        }
    }
}
