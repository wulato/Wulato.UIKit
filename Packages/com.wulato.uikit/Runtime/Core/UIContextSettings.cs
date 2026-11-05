using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;

namespace Wulato.UIKit
{
    /// <summary>Editable build-time settings, copied once for each Context.</summary>
    public sealed class UIContextSettings
    {
        public TimeSpan LoadTimeout { get; set; } = TimeSpan.FromSeconds(30);
        public TimeSpan OpenTimeout { get; set; } = TimeSpan.FromSeconds(10);
        public TimeSpan CloseTimeout { get; set; } = TimeSpan.FromSeconds(2);
        public TimeSpan EnterTimeout { get; set; } = TimeSpan.FromSeconds(2);
        public TimeSpan ExitTimeout { get; set; } = TimeSpan.FromSeconds(2);
        public TimeSpan ShutdownBudget { get; set; } = TimeSpan.FromSeconds(5);
        public int CacheCapacity { get; set; } = 8;
        public UIDiagnosticsLevel DiagnosticsLevel { get; set; } = UIDiagnosticsLevel.Basic;
        public UIContextSettingsSnapshot CopyValidated()
        {
            if (LoadTimeout.Ticks<=0) throw new ArgumentOutOfRangeException(nameof(LoadTimeout),"Duration must be finite and positive.");
            if (OpenTimeout.Ticks<=0) throw new ArgumentOutOfRangeException(nameof(OpenTimeout),"Duration must be finite and positive.");
            if (CloseTimeout.Ticks<=0) throw new ArgumentOutOfRangeException(nameof(CloseTimeout),"Duration must be finite and positive.");
            if (EnterTimeout.Ticks<=0) throw new ArgumentOutOfRangeException(nameof(EnterTimeout),"Duration must be finite and positive.");
            if (ExitTimeout.Ticks<=0) throw new ArgumentOutOfRangeException(nameof(ExitTimeout),"Duration must be finite and positive.");
            if (ShutdownBudget.Ticks<=0) throw new ArgumentOutOfRangeException(nameof(ShutdownBudget),"Duration must be finite and positive.");
            if(CacheCapacity<0) throw new ArgumentOutOfRangeException(nameof(CacheCapacity));
            if(!Enum.IsDefined(typeof(UIDiagnosticsLevel),DiagnosticsLevel)) throw new ArgumentOutOfRangeException(nameof(DiagnosticsLevel));
            return new UIContextSettingsSnapshot(this);
        }
    }
    /// <summary>Immutable, validated runtime configuration; no source settings reference is retained.</summary>
    public sealed class UIContextSettingsSnapshot
    {
        public TimeSpan LoadTimeout { get; }
        public TimeSpan OpenTimeout { get; }
        public TimeSpan CloseTimeout { get; }
        public TimeSpan EnterTimeout { get; }
        public TimeSpan ExitTimeout { get; }
        public TimeSpan ShutdownBudget { get; }
        public int CacheCapacity { get; }
        public UIDiagnosticsLevel DiagnosticsLevel { get; }
        internal UIContextSettingsSnapshot(UIContextSettings source)
        {
            LoadTimeout=source.LoadTimeout;
            OpenTimeout=source.OpenTimeout;
            CloseTimeout=source.CloseTimeout;
            EnterTimeout=source.EnterTimeout;
            ExitTimeout=source.ExitTimeout;
            ShutdownBudget=source.ShutdownBudget;
            CacheCapacity=source.CacheCapacity;DiagnosticsLevel=source.DiagnosticsLevel;
        }
    }
}
