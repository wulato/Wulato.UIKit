using Wulato.UIKit.Dialogs;
namespace Wulato.UIKit.Verification.Addressables
{
    public sealed class BundleRegistryProvider : UIRegistryProvider
    {
        public bool MixedSources;
        public override void Populate(UIRegistryBuilder b)
        {
            if(MixedSources)b.Add<AddressableProbeView>(new UIViewTypeKey("direct-probe"),UIViewAsset.Direct(UnityEngine.Resources.Load<UnityEngine.GameObject>("UIKitAddressablesFallback")));
            b.Add<BundleHomeView,int>(new UIViewTypeKey("bundle-home"),UIViewAsset.External("addressables","uikit/home"),closePolicy:UIClosePolicy.CacheOne);
            b.Add<BundlePageView>(new UIViewTypeKey("bundle-page"),MixedSources?UIViewAsset.Resources("UIKitAddressablesPage"):UIViewAsset.External("addressables","uikit/page"),closePolicy:UIClosePolicy.CacheOne);
            b.AddDialog<BundleDialogView,BundleQuestion,bool>(new UIViewTypeKey("bundle-dialog"),UIViewAsset.External("addressables","uikit/dialog"),UILayer.Modal);
            b.Add<BundleRewardView>(new UIViewTypeKey("bundle-reward"),UIViewAsset.External("addressables","uikit/reward"),UILayer.Modal,UIMultiplicity.Multi);
        }
    }
}
