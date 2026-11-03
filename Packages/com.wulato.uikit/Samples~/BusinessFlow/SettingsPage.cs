using System.Threading;
using Cysharp.Threading.Tasks;
namespace Wulato.UIKit.Samples.BusinessFlow
{
    public sealed class SettingsPage:UIView<FlowActions>
    {
        FlowActions actions;UISessionScope session;
        void Run(System.Action action){if(session!=null&&session.IsCurrent)action?.Invoke();}
        protected override void OnCreate()
        {
            FlowUI.Label(transform,"Settings",150);
            FlowUI.Button(transform,"Settings",80,()=>Run(actions.Settings));FlowUI.Button(transform,"Confirm",15,()=>Run(actions.Confirm));
            FlowUI.Button(transform,"Reward queue",-50,()=>Run(actions.Rewards));FlowUI.Button(transform,"Back",-115,()=>Run(actions.Back));
        }
        protected override UniTask OnOpenAsync(FlowActions args,UISessionScope scope,CancellationToken ct){actions=args;session=scope;return UniTask.CompletedTask;}
        protected override void OnClosed(UICloseReason reason){session=null;actions=default;}
    }
}
