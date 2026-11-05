using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine.UI;
namespace Wulato.UIKit.Samples.BusinessFlow
{
    public sealed class RewardPage:UIView<int>
    {
        Text title;
        protected override void OnCreate(){title=FlowUI.Label(transform,"Reward",70);FlowUI.Button(transform,"Close",0,RequestClose);}
        protected override UniTask OnOpenAsync(int args,UISessionScope scope,CancellationToken ct){title.text="Reward batch "+args;return UniTask.CompletedTask;}
    }
}
