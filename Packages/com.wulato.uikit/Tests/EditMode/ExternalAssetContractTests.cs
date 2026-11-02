using System;
using System.Reflection;
using System.Threading;
using Cysharp.Threading.Tasks;
using NUnit.Framework;
using UnityEngine;
namespace Wulato.UIKit.Tests
{
    [Category("contracts")]
    public sealed class ExternalAssetContractTests
    {
        [Test] public void ExternalSnapshot_PreservesExactIdentifiersAndIsolation()
        {
            var source=UIViewAsset.External("addressables"," UI/Case ");var registry=new UIRegistryBuilder();
            registry.Add<PlainRegistryView>(new UIViewTypeKey("external"),source);
            var snapshot=registry.Build();
            JsonUtility.FromJsonOverwrite("{\"providerId\":\"other\",\"assetId\":\"changed\"}",source);
            var copy=snapshot.Entries[0].Asset;
            Assert.That((int)copy.Kind,Is.EqualTo(2));
            Assert.That(copy.ProviderId,Is.EqualTo("addressables"));
            Assert.That(copy.AssetId,Is.EqualTo(" UI/Case "));
            JsonUtility.FromJsonOverwrite("{\"assetId\":\"export-mutated\"}",copy);
            Assert.That(snapshot.Entries[0].Asset.AssetId,Is.EqualTo(" UI/Case "));
            Assert.That((int)UIViewAsset.Resources("old").Kind,Is.EqualTo(1));
        }
        [Test] public void External_InvalidIdentifiersAreRejectedBeforeRegistration()
        {
            foreach(var invalid in new[]{null,""," \t"})
            { Assert.Throws<ArgumentException>(()=>UIViewAsset.External(invalid,"id")); Assert.Throws<ArgumentException>(()=>UIViewAsset.External("provider",invalid)); }
            var source=UIViewAsset.External("provider","id");JsonUtility.FromJsonOverwrite("{\"assetId\":\" \"}",source);
            Assert.Throws<ArgumentException>(()=>new UIRegistryBuilder().Add<PlainRegistryView>(new UIViewTypeKey("bad"),source));
        }
        [Test] public void DefaultLoaders_RejectExternalWithoutCreatingInstance()
        {
            var parent=new GameObject("external-contract-parent");parent.SetActive(false);
            try
            {
                foreach(var loader in new IUIViewLoader[]{new PrefabUIViewLoader(),new ResourcesUIViewLoader()})
                {
                    var operation=loader.CreateAsync(UIViewAsset.External("addressables","unhandled"),parent.transform,CancellationToken.None);
                    Assert.Throws<ArgumentException>(()=>operation.GetAwaiter().GetResult());
                    Assert.That(parent.transform.childCount,Is.Zero);
                }
            }
            finally { UnityEngine.Object.DestroyImmediate(parent); }
        }
    }
}
