using System.Collections.Generic;
using System.Reflection;
using Cysharp.Threading.Tasks;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
namespace Wulato.UIKit.Tests.Input
{
    public sealed class RuntimeBackHome : UIView { }
    public sealed class RuntimeBackPage : UIView
    {
        internal static bool Allow=true;internal static int Checks;
        protected override bool CanClose(UICloseReason reason){Checks++;Assert.That(reason,Is.EqualTo(UICloseReason.Back));return Allow;}
    }
    public sealed class RuntimeBackModal : UIView
    {
        internal static bool Allow=true;internal static int Checks;
        protected override bool CanClose(UICloseReason reason){Checks++;Assert.That(reason,Is.EqualTo(UICloseReason.Back));return Allow;}
    }
    public sealed class RuntimeBackRegistry : UIRegistryProvider
    {
        public GameObject Home,Page,Modal;
        public override void Populate(UIRegistryBuilder b)
        {
            b.Add<RuntimeBackHome>(new UIViewTypeKey("back-home"),UIViewAsset.Direct(Home));
            b.Add<RuntimeBackPage>(new UIViewTypeKey("back-page"),UIViewAsset.Direct(Page));
            b.Add<RuntimeBackModal>(new UIViewTypeKey("back-modal"),UIViewAsset.Direct(Modal),UILayer.Modal);
        }
    }
    // Reuses the actual driver/domain created by the backend fixture, with real Contexts and selectable Views.
    internal sealed class RuntimeBackFixture
    {
        internal readonly IUIInputArbiter Arbiter;
        internal readonly IUIContext UI;
        readonly List<IUIContext> contexts=new List<IUIContext>();
        readonly List<GameObject> roots=new List<GameObject>();
        readonly GameObject sources;
        readonly RuntimeBackRegistry provider;
        readonly UIRegistry registry;
        readonly EventSystem previous;
        internal RuntimeBackFixture(RuntimeInputScene scene)
        {
            RuntimeBackPage.Allow=RuntimeBackModal.Allow=true;RuntimeBackPage.Checks=RuntimeBackModal.Checks=0;
            previous=EventSystem.current;scene.Events.enabled=true;EventSystem.current=scene.Events;Arbiter=scene.Gate.Arbiter;
            for(int i=0;i<2;i++)Arbiter.Revoke(scene.Gate.Contexts[i],scene.Gate.Sessions[i]);
            sources=new GameObject("back sources");sources.SetActive(false);
            provider=ScriptableObject.CreateInstance<RuntimeBackRegistry>();provider.Home=Prefab<RuntimeBackHome>();provider.Page=Prefab<RuntimeBackPage>();provider.Modal=Prefab<RuntimeBackModal>();
            registry=ScriptableObject.CreateInstance<UIRegistry>();typeof(UIRegistry).GetField("providers",BindingFlags.Instance|BindingFlags.NonPublic).SetValue(registry,new List<UIRegistryProvider>{provider});
            UI=CreatePeer();
        }
        GameObject Prefab<T>() where T:UIViewBase
        {
            var go=new GameObject(typeof(T).Name,typeof(RectTransform));go.transform.SetParent(sources.transform,false);go.AddComponent<T>();
            var button=new GameObject("Cancel target",typeof(RectTransform),typeof(Image),typeof(Button));button.transform.SetParent(go.transform,false);return go;
        }
        internal IUIContext CreatePeer()
        {
            var root=new GameObject("back root",typeof(RectTransform),typeof(UIRoot));roots.Add(root);
            root.GetComponent<Canvas>().renderMode=RenderMode.ScreenSpaceOverlay;
            var context=new UIContextBuilder(root.GetComponent<UIRoot>(),registry).WithInputArbiter(Arbiter).Build();contexts.Add(context);return context;
        }
        internal async UniTask DisposeAsync()
        {
            for(int i=contexts.Count-1;i>=0;i--)await contexts[i].ShutdownAsync();
            foreach(var context in contexts){Assert.That(context.GetDiagnostics().ActiveLeaseCount,Is.Zero);Assert.That(context.GetDiagnostics().InputLockCount,Is.Zero);}
            await UniTask.NextFrame();foreach(var root in roots)Object.DestroyImmediate(root);
            Object.DestroyImmediate(sources);Object.DestroyImmediate(registry);Object.DestroyImmediate(provider);if(previous)EventSystem.current=previous;
        }
    }
}
