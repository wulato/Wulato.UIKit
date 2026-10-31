using System;
using NUnit.Framework;
using UnityEngine;
namespace Wulato.UIKit.Verification.Tests
{
    [Category("init")]
    public class InitializationOrderTests
    {
        GameObject parent;
        LifecycleProbe Spawn(bool inactive)
        {
            LifecycleProbe.Traces.Clear();
            parent=new GameObject("TestParent"); parent.SetActive(!inactive);
            var prefab=Resources.Load<GameObject>("LifecycleFixture"); Assert.That(prefab,Is.Not.Null);
            return UnityEngine.Object.Instantiate(prefab,parent.transform).GetComponent<LifecycleProbe>();
        }
        [TearDown] public void Cleanup() { if(parent) UnityEngine.Object.DestroyImmediate(parent); LifecycleProbe.Traces.Clear(); }
        [Test] public void ActiveParent_ControlAwakePrecedesOnCreate()
        {
            var p=Spawn(false); p.ProbeOnCreate(); p.Save(nameof(ActiveParent_ControlAwakePrecedesOnCreate));
            Assert.That(p.Events,Is.EqualTo(new[]{"Awake","OnEnable","OnCreate"}));
        }
        [Test] public void InactiveParent_DefersAwakeUntilActivation()
        {
            var p=Spawn(Environment.GetEnvironmentVariable("UIKIT_RED_INIT")!="1");
            p.Save("InactiveParent_BeforeOnCreate");
            Assert.That(p.gameObject.activeInHierarchy,Is.False);
            Assert.That(p.Events,Is.Empty); Assert.That(p.SerializedDependency,Is.Not.Null); Assert.That(p.AwakeInitialized,Is.False);
            p.ProbeOnCreate(); Assert.That(p.Events,Is.EqualTo(new[]{"OnCreate"}));
            parent.SetActive(true); p.Save(nameof(InactiveParent_DefersAwakeUntilActivation));
            Assert.That(p.Events,Is.EqualTo(new[]{"OnCreate","Awake","OnEnable"}));
        }
        [Test] public void CanvasGroup_DoesNotDeferAwake()
        {
            var p=Spawn(true); var c=parent.AddComponent<CanvasGroup>(); c.alpha=0;c.interactable=false;c.blocksRaycasts=false;
            parent.SetActive(true); p.ProbeOnCreate(); p.Save(nameof(CanvasGroup_DoesNotDeferAwake));
            Assert.That(p.Events,Is.EqualTo(new[]{"Awake","OnEnable","OnCreate"}));
        }
        [Test] public void Reactivation_DoesNotRepeatAwake()
        {
            var p=Spawn(true);p.ProbeOnCreate();parent.SetActive(true);parent.SetActive(false);parent.SetActive(true);p.Save(nameof(Reactivation_DoesNotRepeatAwake));
            Assert.That(p.Events,Is.EqualTo(new[]{"OnCreate","Awake","OnEnable","OnEnable"}));
        }
    }
}
