using System;
using System.Collections.Generic;
using NUnit.Framework;
namespace Wulato.UIKit.Tests
{
    public sealed class PlainRegistryView : UIView { }
    public sealed class TypedRegistryView : UIView<int> { }
    [Category("contracts")]
    public sealed class RegistryTests
    {
        [Test] public void Snapshot_IsIndependentAndChecksExactArguments()
        {
            var builder=new UIRegistryBuilder();
            builder.Add<PlainRegistryView>(new UIViewTypeKey("plain"),UIViewAsset.Resources("UI/Plain"));
            var first=builder.Build();
            builder.Add<TypedRegistryView,int>(new UIViewTypeKey("typed"),UIViewAsset.Resources("UI/Typed"));
            Assert.That(first.Entries.Count,Is.EqualTo(1));
            var all=builder.Build();
            Assert.That(all.Get(new UIViewTypeKey("typed")).Accepts(UIArguments.None),Is.False);
            Assert.That(all.Get(new UIViewTypeKey("typed")).Accepts(UIArguments.From("wrong")),Is.False);
            Assert.That(all.Get(new UIViewTypeKey("typed")).Accepts(UIArguments.From(42)),Is.True);
            Assert.That(first.Get(new UIViewTypeKey("plain")).Accepts(UIArguments.None),Is.True);
            Assert.That(first.Get(new UIViewTypeKey("plain")).Accepts(UIArguments.From<string>(null)),Is.False);
            Assert.Throws<NotSupportedException>(()=>((IList<UIRegistration>)all.Entries).Clear());
        }
        [Test] public void SerializedAssetMutation_DoesNotChangeSnapshot()
        {
            var source=UIViewAsset.Resources("UI/A");var builder=new UIRegistryBuilder();
            builder.Add<PlainRegistryView>(new UIViewTypeKey("asset"),source);var snapshot=builder.Build();
            UnityEngine.JsonUtility.FromJsonOverwrite("{\"resourceId\":\"UI/B\"}",source);
            var exported=snapshot.Get(new UIViewTypeKey("asset")).Asset;
            Assert.That(exported.ResourceId,Is.EqualTo("UI/A"));
            UnityEngine.JsonUtility.FromJsonOverwrite("{\"resourceId\":\"UI/C\"}",exported);
            Assert.That(snapshot.Get(new UIViewTypeKey("asset")).Asset.ResourceId,Is.EqualTo("UI/A"));
        }
        [Test] public void SerializedInvalidAsset_IsRejected()
        {
            foreach(var json in new[]{"{\"kind\":99}","{\"resourceId\":\"\"}","{\"kind\":0,\"prefab\":null}"})
            {
                var source=UIViewAsset.Resources("UI/A");UnityEngine.JsonUtility.FromJsonOverwrite(json,source);
                Assert.Throws<ArgumentException>(()=>new UIRegistryBuilder().Add<PlainRegistryView>(new UIViewTypeKey("bad"),source));
            }
        }
        [Test] public void DuplicateTypeOrKeyAndMultiCache_AreRejected()
        {
            var builder=new UIRegistryBuilder();
            builder.Add<PlainRegistryView>(new UIViewTypeKey("a"),UIViewAsset.Resources("UI/A"));
            Assert.Throws<ArgumentException>(()=>builder.Add<PlainRegistryView>(new UIViewTypeKey("b"),UIViewAsset.Resources("UI/B")));
            Assert.Throws<ArgumentException>(()=>builder.Add<TypedRegistryView,int>(new UIViewTypeKey("a"),UIViewAsset.Resources("UI/B")));
            Assert.Throws<ArgumentException>(()=>new UIRegistryBuilder().Add<PlainRegistryView>(new UIViewTypeKey("m"),UIViewAsset.Resources("UI/M"),multiplicity:UIMultiplicity.Multi,closePolicy:UIClosePolicy.CacheOne));
        }
    }
}
