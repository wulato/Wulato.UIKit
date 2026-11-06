using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using Cysharp.Threading.Tasks;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.TestTools;
namespace Wulato.UIKit.Verification.Addressables.Tests
{
    public sealed class PreviewRouteRegistry:UIRegistryProvider
    {
        public override void Populate(UIRegistryBuilder b)=>b.Add<AddressableProbeView>(new UIViewTypeKey("preview-addressable"),UIViewAsset.External("addressables","uikit/probe"));
    }
    [Category("preview_addressables")]
    public sealed class PreviewRoutingTests
    {
        [UnityTest] public IEnumerator DefaultAddressablesRoute_OpensWithoutLoaderAdapter()=>UniTask.ToCoroutine(async()=>
        {
            var type=Type.GetType("Wulato.UIKit.Editor.Authoring.UIPreviewRuntime, Wulato.UIKit.Editor",true);
            var profileType=type.Assembly.GetType("Wulato.UIKit.Editor.Authoring.UIPreviewProfile",true);var profile=ScriptableObject.CreateInstance(profileType);
            var registry=ScriptableObject.CreateInstance<UIRegistry>();var provider=ScriptableObject.CreateInstance<PreviewRouteRegistry>();
            typeof(UIRegistry).GetField("providers",BindingFlags.Instance|BindingFlags.NonPublic).SetValue(registry,new List<UIRegistryProvider>{provider});
            profileType.GetField("registry").SetValue(profile,registry);profileType.GetField("view").SetValue(profile,new UIViewTypeKey("preview-addressable"));Assert.That(profileType.GetField("adapter").GetValue(profile),Is.Null);
            var root=new GameObject("Preview route",typeof(RectTransform),typeof(UIRoot));root.GetComponent<Canvas>().renderMode=RenderMode.ScreenSpaceOverlay;var host=root.AddComponent<UIContextHost>();
            typeof(UIContextHost).GetField("root",BindingFlags.Instance|BindingFlags.NonPublic).SetValue(host,root.GetComponent<UIRoot>());typeof(UIContextHost).GetField("registry",BindingFlags.Instance|BindingFlags.NonPublic).SetValue(host,registry);typeof(UIContextHost).GetField("initializeOnStart",BindingFlags.Instance|BindingFlags.NonPublic).SetValue(host,false);
            var previous=EventSystem.current;var events=new GameObject("Preview route events",typeof(EventSystem));
#if ENABLE_INPUT_SYSTEM && !ENABLE_LEGACY_INPUT_MANAGER
            events.AddComponent(Type.GetType("Wulato.UIKit.InputSystem.InputSystemUIInputDriver, Wulato.UIKit.InputSystem",true));
#else
            events.AddComponent<LegacyUIInputDriver>();
#endif
            EventSystem.current=events.GetComponent<EventSystem>();var runtime=Activator.CreateInstance(type,new object[]{profile,host});
            try
            {
                await (UniTask)type.GetMethod("OpenAsync").Invoke(runtime,null);var session=(IUISession)type.GetProperty("Session").GetValue(runtime);
                Assert.That(root.GetComponentInChildren<AddressableProbeView>(true),Is.Not.Null);Assert.That(session.State,Is.EqualTo(UIState.Open));
                await (UniTask<UIShutdownCompletion>)type.GetMethod("ShutdownAsync").Invoke(runtime,null);
                Assert.That(host.UI.GetDiagnostics().ActiveLeaseCount,Is.Zero);Assert.That(host.UI.GetDiagnostics().InputLockCount,Is.Zero);Assert.That(host.UI.GetDiagnostics().TotalErrorCount,Is.Zero);
            }
            finally
            {
                await (UniTask<UIShutdownCompletion>)type.GetMethod("ShutdownAsync").Invoke(runtime,null);UnityEngine.Object.Destroy(root);UnityEngine.Object.Destroy(events);UnityEngine.Object.Destroy(registry);UnityEngine.Object.Destroy(provider);UnityEngine.Object.Destroy(profile);if(previous)EventSystem.current=previous;
            }
            var output=Environment.GetEnvironmentVariable("UIKIT_EVIDENCE_DIR");if(!string.IsNullOrEmpty(output))File.WriteAllText(Path.Combine(output,"DefaultAddressablesRoute_OpensWithoutLoaderAdapter.trace.json"),"{\"events\":[\"real-bundle\",\"default-route\",\"no-adapter\",\"shutdown-empty\"]}");
        });
    }
}
