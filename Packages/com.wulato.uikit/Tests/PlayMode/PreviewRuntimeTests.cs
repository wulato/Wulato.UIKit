using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using Cysharp.Threading.Tasks;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.TestTools;
namespace Wulato.UIKit.Tests.Preview
{
    public sealed class PreviewRegistry:UIRegistryProvider
    {
        public override void Populate(UIRegistryBuilder b)=>b.Add<Wulato.UIKit.Tests.Support.T4ProbeView>(new UIViewTypeKey("preview-probe"),UIViewAsset.Resources("T4/Probe"),closePolicy:UIClosePolicy.CacheOne);
    }
    public sealed class PreviewTypedView:UIView<string>
    {
        public static string Received;
        protected override UniTask OnOpenAsync(string args,UISessionScope scope,System.Threading.CancellationToken token){Received=args;return UniTask.CompletedTask;}
    }
    public sealed class PreviewTypedRegistry:UIRegistryProvider
    {
        public static GameObject Prefab;
        public override void Populate(UIRegistryBuilder b)=>b.Add<PreviewTypedView,string>(new UIViewTypeKey("typed-preview"),UIViewAsset.Direct(Prefab));
    }
    [Category("preview_runtime")]
    public class PreviewRuntimeTests
    {
        [UnityTest] public IEnumerator Adapter_TypedNullMismatchAndConfigureFailure_ReleaseOnce()=>UniTask.ToCoroutine(async()=>{
            var type=AppDomain.CurrentDomain.GetAssemblies().Select(a=>a.GetType("Wulato.UIKit.Editor.Authoring.UIPreviewRuntime")).First(t=>t!=null);
            var profileType=type.Assembly.GetType("Wulato.UIKit.Editor.Authoring.UIPreviewProfile");
            var adapterType=AppDomain.CurrentDomain.GetAssemblies().Select(a=>a.GetType("Wulato.UIKit.Tests.Preview.PreviewTestAdapter")).First(t=>t!=null);
            for(int mode=0;mode<4;mode++)
            {
                var profile=ScriptableObject.CreateInstance(profileType);var adapter=ScriptableObject.CreateInstance(adapterType);
                adapterType.GetField("mode").SetValue(adapter,mode);adapterType.GetField("Configures").SetValue(null,0);adapterType.GetField("Releases").SetValue(null,0);
                var registry=ScriptableObject.CreateInstance<UIRegistry>();var provider=ScriptableObject.CreateInstance<PreviewTypedRegistry>();
                PreviewTypedRegistry.Prefab=new GameObject("TypedPrefab",typeof(RectTransform),typeof(PreviewTypedView));
                typeof(UIRegistry).GetField("providers",BindingFlags.Instance|BindingFlags.NonPublic).SetValue(registry,new List<UIRegistryProvider>{provider});
                profileType.GetField("registry").SetValue(profile,registry);profileType.GetField("view").SetValue(profile,new UIViewTypeKey("typed-preview"));profileType.GetField("adapter").SetValue(profile,adapter);
                var root=new GameObject("TypedRoot",typeof(RectTransform),typeof(UIRoot));root.GetComponent<Canvas>().renderMode=RenderMode.ScreenSpaceOverlay;var host=root.AddComponent<UIContextHost>();
                typeof(UIContextHost).GetField("root",BindingFlags.Instance|BindingFlags.NonPublic).SetValue(host,root.GetComponent<UIRoot>());typeof(UIContextHost).GetField("registry",BindingFlags.Instance|BindingFlags.NonPublic).SetValue(host,registry);typeof(UIContextHost).GetField("initializeOnStart",BindingFlags.Instance|BindingFlags.NonPublic).SetValue(host,false);
                var events=new GameObject("TypedEvents",typeof(EventSystem));
#if ENABLE_INPUT_SYSTEM && !ENABLE_LEGACY_INPUT_MANAGER
                events.AddComponent(Type.GetType("Wulato.UIKit.InputSystem.InputSystemUIInputDriver, Wulato.UIKit.InputSystem",true));
#else
                events.AddComponent<LegacyUIInputDriver>();
#endif
                var runtime=Activator.CreateInstance(type,new object[]{profile,host});PreviewTypedView.Received="not null";Exception failure=null;
                try{try{await (UniTask)type.GetMethod("OpenAsync").Invoke(runtime,null);}catch(Exception e){failure=e;}
                    if(mode==0||mode==3){Assert.That(failure,Is.Null);Assert.That(PreviewTypedView.Received,Is.Null);}else Assert.That(failure,Is.TypeOf<ArgumentException>());
                }
                finally
                {
                    if(mode==3)
                    {
                        var source=(System.Threading.CancellationTokenSource)type.GetField("cancellation",BindingFlags.Instance|BindingFlags.NonPublic).GetValue(runtime);
                        source.Token.Register(()=>throw new ArgumentException("cancel callback failed"));Exception shutdownError=null;
                        try{await (UniTask<UIShutdownCompletion>)type.GetMethod("ShutdownAsync").Invoke(runtime,null);}catch(Exception e){shutdownError=e;}
                        Assert.That(shutdownError,Is.Not.Null);Assert.That(host.Context.GetDiagnostics().State,Is.EqualTo(UIContextState.Stopped),"Cancellation callbacks must not prevent Context shutdown.");
                    }
                    else {await (UniTask<UIShutdownCompletion>)type.GetMethod("ShutdownAsync").Invoke(runtime,null);await (UniTask<UIShutdownCompletion>)type.GetMethod("ShutdownAsync").Invoke(runtime,null);}
                    Assert.That(adapterType.GetField("Configures").GetValue(null),Is.EqualTo(1));Assert.That(adapterType.GetField("Releases").GetValue(null),Is.EqualTo(1));
                    UnityEngine.Object.Destroy(root);UnityEngine.Object.Destroy(events);UnityEngine.Object.Destroy(registry);UnityEngine.Object.Destroy(provider);UnityEngine.Object.Destroy(profile);UnityEngine.Object.Destroy(adapter);UnityEngine.Object.Destroy(PreviewTypedRegistry.Prefab);await UniTask.NextFrame();
                }
            }
            var dir=Environment.GetEnvironmentVariable("UIKIT_EVIDENCE_DIR");if(!string.IsNullOrEmpty(dir))File.WriteAllText(Path.Combine(dir,"Adapter_TypedNullMismatchAndConfigureFailure_ReleaseOnce.trace.json"),"{\"events\":[\"typed-null\",\"mismatch\",\"configure-failed\",\"released-once\"]}");
        });
        [UnityTest] public IEnumerator Runtime_ClosesAndReopensWithNewSession()=>UniTask.ToCoroutine(async()=>{
            var type=AppDomain.CurrentDomain.GetAssemblies().Select(a=>a.GetType("Wulato.UIKit.Editor.Authoring.UIPreviewRuntime")).FirstOrDefault(t=>t!=null);
            Assert.That(type,Is.Not.Null,"Preview runtime not implemented");
            var profileType=type.Assembly.GetType("Wulato.UIKit.Editor.Authoring.UIPreviewProfile");
            var profile=ScriptableObject.CreateInstance(profileType);var registry=ScriptableObject.CreateInstance<UIRegistry>();var provider=ScriptableObject.CreateInstance<PreviewRegistry>();
            typeof(UIRegistry).GetField("providers",BindingFlags.Instance|BindingFlags.NonPublic).SetValue(registry,new List<UIRegistryProvider>{provider});
            profileType.GetField("registry").SetValue(profile,registry);profileType.GetField("view").SetValue(profile,new UIViewTypeKey("preview-probe"));
            var root=new GameObject("PreviewRuntimeTest",typeof(RectTransform),typeof(UIRoot));root.GetComponent<Canvas>().renderMode=RenderMode.ScreenSpaceOverlay;
            var host=root.AddComponent<UIContextHost>();typeof(UIContextHost).GetField("root",BindingFlags.Instance|BindingFlags.NonPublic).SetValue(host,root.GetComponent<UIRoot>());
            typeof(UIContextHost).GetField("registry",BindingFlags.Instance|BindingFlags.NonPublic).SetValue(host,registry);
            typeof(UIContextHost).GetField("initializeOnStart",BindingFlags.Instance|BindingFlags.NonPublic).SetValue(host,false);
            var events=new GameObject("PreviewEvents",typeof(EventSystem));
#if ENABLE_INPUT_SYSTEM && !ENABLE_LEGACY_INPUT_MANAGER
            events.AddComponent(Type.GetType("Wulato.UIKit.InputSystem.InputSystemUIInputDriver, Wulato.UIKit.InputSystem",true));
#else
            events.AddComponent<LegacyUIInputDriver>();
#endif
            var runtime=Activator.CreateInstance(type,new object[]{profile,host});
            try
            {
                await (UniTask)type.GetMethod("OpenAsync").Invoke(runtime,null);var first=(IUISession)type.GetProperty("Session").GetValue(runtime);
                Assert.That(first.State,Is.EqualTo(UIState.Open));await (UniTask)type.GetMethod("CloseAsync").Invoke(runtime,null);
                await (UniTask)type.GetMethod("OpenAsync").Invoke(runtime,null);var next=(IUISession)type.GetProperty("Session").GetValue(runtime);
                Assert.That(next.Id,Is.Not.EqualTo(first.Id));
            }
            finally
            {
                await (UniTask<UIShutdownCompletion>)type.GetMethod("ShutdownAsync").Invoke(runtime,null);
                UnityEngine.Object.Destroy(root);UnityEngine.Object.Destroy(events);UnityEngine.Object.Destroy(registry);UnityEngine.Object.Destroy(provider);UnityEngine.Object.Destroy(profile);
            }
            var dir=Environment.GetEnvironmentVariable("UIKIT_EVIDENCE_DIR");if(!string.IsNullOrEmpty(dir))File.WriteAllText(Path.Combine(dir,"Runtime_ClosesAndReopensWithNewSession.trace.json"),"{\"events\":[\"open\",\"close\",\"new-session\",\"shutdown\"]}");
        });
    }
}
