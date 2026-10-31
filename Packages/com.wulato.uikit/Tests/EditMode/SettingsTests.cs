using System;
using NUnit.Framework;
namespace Wulato.UIKit.Tests
{
    [Category("contracts")]
    public sealed class SettingsTests
    {
        [Test] public void Defaults_MatchApprovedBudgets()
        {
            var s=new UIContextSettings().CopyValidated();
            Assert.That(new[]{s.LoadTimeout.TotalSeconds,s.OpenTimeout.TotalSeconds,s.CloseTimeout.TotalSeconds,s.EnterTimeout.TotalSeconds,s.ExitTimeout.TotalSeconds,s.ShutdownBudget.TotalSeconds},Is.EqualTo(new double[]{30,10,2,2,2,5}));
            Assert.That(s.CacheCapacity,Is.EqualTo(8));Assert.That(s.DiagnosticsLevel,Is.EqualTo(UIDiagnosticsLevel.Basic));
        }
        [Test] public void EveryBudget_RejectsZeroAndNegative()
        {
            Action<UIContextSettings,TimeSpan>[] set={ (s,t)=>s.LoadTimeout=t,(s,t)=>s.OpenTimeout=t,(s,t)=>s.CloseTimeout=t,(s,t)=>s.EnterTimeout=t,(s,t)=>s.ExitTimeout=t,(s,t)=>s.ShutdownBudget=t };
            foreach(var assign in set) foreach(var t in new[]{TimeSpan.Zero,TimeSpan.FromTicks(-1),TimeSpan.MinValue})
            { var s=new UIContextSettings();assign(s,t);Assert.Throws<ArgumentOutOfRangeException>(()=>s.CopyValidated()); }
        }
        [Test] public void Capacity_RejectsNegativeAcceptsZero()
        {
            Assert.Throws<ArgumentOutOfRangeException>(()=>new UIContextSettings{CacheCapacity=-1}.CopyValidated());
            Assert.That(new UIContextSettings{CacheCapacity=0}.CopyValidated().CacheCapacity,Is.Zero);
        }
        [Test] public void InvalidDiagnosticsLevel_IsRejected()
        { Assert.Throws<ArgumentOutOfRangeException>(()=>new UIContextSettings{DiagnosticsLevel=(UIDiagnosticsLevel)99}.CopyValidated()); }
        [Test] public void Snapshot_DoesNotFollowSourceMutation()
        {
            var source=new UIContextSettings();var snapshot=source.CopyValidated();
            source.LoadTimeout=TimeSpan.FromSeconds(1);source.CacheCapacity=0;source.DiagnosticsLevel=UIDiagnosticsLevel.Off;
            Assert.That(snapshot.LoadTimeout.TotalSeconds,Is.EqualTo(30));Assert.That(snapshot.CacheCapacity,Is.EqualTo(8));Assert.That(snapshot.DiagnosticsLevel,Is.EqualTo(UIDiagnosticsLevel.Basic));
            foreach(var p in typeof(UIContextSettingsSnapshot).GetProperties()) Assert.That(p.CanWrite,Is.False,p.Name);
        }
        [Test] public void TimeSpan_CannotRepresentNonFiniteDurations()
        {
            Assert.Throws<ArgumentException>(()=>TimeSpan.FromSeconds(double.NaN));
            Assert.Throws<OverflowException>(()=>TimeSpan.FromSeconds(double.PositiveInfinity));
            Assert.Throws<OverflowException>(()=>TimeSpan.FromSeconds(double.NegativeInfinity));
        }
    }
}
