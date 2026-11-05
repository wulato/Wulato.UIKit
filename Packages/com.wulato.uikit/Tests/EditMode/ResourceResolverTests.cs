using System;
using System.Collections.Generic;
using System.Reflection;
using System.Threading;
using Cysharp.Threading.Tasks;
using NUnit.Framework;
using UnityEngine;
using Wulato.UIKit.Tests.EditMode;
namespace Wulato.UIKit.Tests
{
    public sealed class ResolverRegistry : UIRegistryProvider
    {
        public override void Populate(UIRegistryBuilder builder) => builder.Add<PlainRegistryView>(new UIViewTypeKey("route"),UIViewAsset.External("missing-resolver-provider","asset"));
    }
    [Category("contracts")]
    public sealed class ResourceResolverTests
    {
        sealed class Loader : IUIViewLoader
        {
            internal int Calls;internal UIViewAsset Asset;internal Transform Parent;internal CancellationToken Token;
            public UniTask<IUIViewLease> CreateAsync(UIViewAsset asset,Transform parent,CancellationToken token)
            {Calls++;Asset=asset;Parent=parent;Token=token;return UniTask.FromResult<IUIViewLease>(null);}
        }
        static UIRegistrySnapshot Mapping(string id)
        {var b=new UIRegistryBuilder();b.Add<PlainRegistryView>(new UIViewTypeKey("route"),UIViewAsset.External(id," exact/key "));return b.Build();}
        [Test] public void MissingBackend_IsValidatedBeforeAnyFactoryRuns()
        {
            int created=0;UIResourceResolver.RegisterProvider("resolver-validation",()=>{created++;return new Loader();});
            var b=new UIRegistryBuilder();b.Add<PlainRegistryView>(new UIViewTypeKey("a"),UIViewAsset.External("resolver-validation","a"));
            b.Add<TypedRegistryView,int>(new UIViewTypeKey("b"),UIViewAsset.External("missing-resolver-provider","b"));
            Assert.That(Assert.Throws<InvalidOperationException>(()=>new UIResourceResolver(b.Build())).Message,Does.Contain("missing-resolver-provider"));Assert.That(created,Is.Zero);
        }
        [Test] public void ContextSnapshots_CreateIndependentLoadersAndIgnoreLaterRegistration()
        {
            var made=new List<Loader>();Func<IUIViewLoader> factory=()=>{var l=new Loader();made.Add(l);return l;};
            UIResourceResolver.RegisterProvider("resolver-snapshot",factory);UIResourceResolver.RegisterProvider("resolver-snapshot",factory);
            var mapping=Mapping("resolver-snapshot");var a=new UIResourceResolver(mapping);var b=new UIResourceResolver(mapping);
            Assert.That(made.Count,Is.EqualTo(2));Assert.That(made[0],Is.Not.SameAs(made[1]));
            var replacement=new Loader();UIResourceResolver.RegisterProvider("resolver-snapshot",()=>replacement);
            a.CreateAsync(mapping.Entries[0].Asset,null,default).GetAwaiter().GetResult();b.CreateAsync(mapping.Entries[0].Asset,null,default).GetAwaiter().GetResult();
            Assert.That(made[0].Calls,Is.EqualTo(1));Assert.That(made[1].Calls,Is.EqualTo(1));Assert.That(replacement.Calls,Is.Zero);
            new UIResourceResolver(mapping).CreateAsync(mapping.Entries[0].Asset,null,default).GetAwaiter().GetResult();Assert.That(replacement.Calls,Is.EqualTo(1));
        }
        [Test] public void Snapshot_DoesNotGainProviderRegisteredAfterBuild()
        {
            var resolver=new UIResourceResolver(new UIRegistryBuilder().Build());int created=0;
            UIResourceResolver.RegisterProvider("resolver-late",()=>{created++;return new Loader();});
            Assert.Throws<InvalidOperationException>(()=>resolver.CreateAsync(UIViewAsset.External("resolver-late","a"),null,default).GetAwaiter().GetResult());Assert.That(created,Is.Zero);
        }
        [Test] public void Routing_PreservesExactAssetParentAndCancellationToken()
        {
            var loader=new Loader();UIResourceResolver.RegisterProvider("resolver-exact",()=>loader);var mapping=Mapping("resolver-exact");var resolver=new UIResourceResolver(mapping);
            var parent=new GameObject("resolver-parent");parent.SetActive(false);
            try {using(var ct=new CancellationTokenSource())
            {
                var asset=mapping.Entries[0].Asset;resolver.CreateAsync(asset,parent.transform,ct.Token).GetAwaiter().GetResult();
                Assert.That(loader.Asset,Is.SameAs(asset));Assert.That(loader.Asset.AssetId,Is.EqualTo(" exact/key "));Assert.That(loader.Parent,Is.SameAs(parent.transform));Assert.That(loader.Token,Is.EqualTo(ct.Token));
                ct.Cancel();Assert.Throws<OperationCanceledException>(()=>resolver.CreateAsync(asset,parent.transform,ct.Token).GetAwaiter().GetResult());Assert.That(loader.Calls,Is.EqualTo(1));
                Assert.Throws<InvalidOperationException>(()=>resolver.CreateAsync(UIViewAsset.External("RESOLVER-EXACT","a"),parent.transform,default).GetAwaiter().GetResult());
            }}finally{UnityEngine.Object.DestroyImmediate(parent);}
        }
        [Test] public void NullFactoryResult_IsRejectedBeforeContextAllocation()
        {UIResourceResolver.RegisterProvider("resolver-null",()=>null);Assert.Throws<InvalidOperationException>(()=>new UIResourceResolver(Mapping("resolver-null")));}
        [Test] public void MissingBackend_BuildDoesNotAllocateHierarchyOrRunServices()
        { CheckBuilder(false); }
        [Test] public void ExplicitLoader_RemainsAvailableForCustomBackends()
        { CheckBuilder(true); }
        static void CheckBuilder(bool explicitLoader)
        {
            var root=new GameObject("resolver-root",typeof(RectTransform),typeof(UIRoot));root.GetComponent<Canvas>().renderMode=RenderMode.ScreenSpaceOverlay;
            var events=EditModeUIFixture.CreateEvents("resolver-events");var registry=ScriptableObject.CreateInstance<UIRegistry>();var provider=ScriptableObject.CreateInstance<ResolverRegistry>();IUIContext ui=null;int services=0;
            typeof(UIRegistry).GetField("providers",BindingFlags.Instance|BindingFlags.NonPublic).SetValue(registry,new List<UIRegistryProvider>{provider});
            try
            {
                var builder=new UIContextBuilder(root.GetComponent<UIRoot>(),registry).WithServiceFactory(_=>{services++;return new object();});
                if(explicitLoader)ui=builder.WithLoader(new Loader()).Build();
                else
                {
                    Assert.That(Assert.Throws<InvalidOperationException>(()=>builder.Build()).Message,Does.Contain("missing-resolver-provider"));Assert.That(root.transform.childCount,Is.Zero);Assert.That(services,Is.Zero);
                    ui=builder.WithLoader(new Loader()).Build(); // Failed validation never claimed the root/input domain.
                }
                Assert.That(services,Is.EqualTo(1));
            }
            finally{ui?.ShutdownAsync().GetAwaiter().GetResult();UnityEngine.Object.DestroyImmediate(root);EditModeUIFixture.DestroyEvents(events);UnityEngine.Object.DestroyImmediate(registry);UnityEngine.Object.DestroyImmediate(provider);}
        }
    }
}
