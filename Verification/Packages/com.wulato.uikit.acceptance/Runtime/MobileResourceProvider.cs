using UnityEngine;
namespace Wulato.UIKit.Acceptance
{
    public sealed class MobileResourceProvider : UIRegistryProvider
    {
        public override void Populate(UIRegistryBuilder builder)=>builder.Add<MobileSmokeView,MobileSmokeArgs>(new UIViewTypeKey("mobile-resource"),UIViewAsset.Resources("UIKitMobileView"),UILayer.Modal,closePolicy:UIClosePolicy.CacheOne);
    }
}
