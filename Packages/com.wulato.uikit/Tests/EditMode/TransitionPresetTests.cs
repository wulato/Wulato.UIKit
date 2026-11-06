using NUnit.Framework;
using UnityEditor;
using UnityEngine;
namespace Wulato.UIKit.Tests.EditMode
{
    [Category("contracts")]
    public sealed class TransitionPresetTests
    {
        [Test] public void HostPresets_AreOptionalAndSdkIndependent()
        {
            Assert.That(typeof(UITransitionPreset).GetMethod("CreateTransition").ReturnType,Is.EqualTo(typeof(IUITransition)));
            Assert.That(typeof(UINavigationTransitionPreset).GetMethod("CreateTransition").ReturnType,Is.EqualTo(typeof(INavigationTransition)));
            var go=new GameObject("preset-host");try
            {
                var host=go.AddComponent<UIContextHost>();var so=new SerializedObject(host);
                Assert.That(so.FindProperty("transitionPreset").objectReferenceValue,Is.Null);
                Assert.That(so.FindProperty("navigationPreset").objectReferenceValue,Is.Null);
            }finally{Object.DestroyImmediate(go);}
            Assert.That(typeof(IUIContext).Assembly.GetType("Wulato.UIKit.UITransitionRunner"),Is.Null);
        }
    }
}
