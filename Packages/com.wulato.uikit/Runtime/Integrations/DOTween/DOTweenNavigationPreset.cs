using UnityEngine;
namespace Wulato.UIKit.Integrations.DOTween
{
    [CreateAssetMenu(menuName="Wulato/UIKit/Transitions/DOTween Page Pair")]
    public sealed class DOTweenNavigationPreset:UINavigationTransitionPreset
    {
        public DOTweenTransitionSettings Incoming=new DOTweenTransitionSettings();
        public DOTweenTransitionSettings Outgoing=new DOTweenTransitionSettings();
        public override INavigationTransition CreateTransition()=>new DOTweenUITransition(Incoming,Outgoing);
    }
}
