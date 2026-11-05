using UnityEngine;
using Wulato.UIKit.Dialogs;
namespace Wulato.UIKit.Samples.BusinessFlow
{
    public sealed class BusinessFlowRegistryProvider:UIRegistryProvider
    {
        public GameObject Home,Settings,Confirm,Reward;
        public override void Populate(UIRegistryBuilder b)
        {
            b.Add<HomePage,FlowActions>(new UIViewTypeKey("flow-home"),UIViewAsset.Direct(Home),closePolicy:UIClosePolicy.CacheOne);
            b.Add<SettingsPage,FlowActions>(new UIViewTypeKey("flow-settings"),UIViewAsset.Direct(Settings));
            b.AddDialog<ConfirmPage,ConfirmRequest,bool>(new UIViewTypeKey("flow-confirm"),UIViewAsset.Direct(Confirm),UILayer.Modal);
            b.Add<RewardPage,int>(new UIViewTypeKey("flow-reward"),UIViewAsset.Direct(Reward),UILayer.Modal,closePolicy:UIClosePolicy.CacheOne);
        }
    }
}
