using System;
using DG.Tweening;
using UnityEngine;
namespace Wulato.UIKit.Integrations.DOTween
{
    [Flags] public enum DOTweenUIEffects { None=0,Fade=1,Scale=2,Slide=4 }
    [Serializable]
    public sealed class DOTweenTransitionSettings
    {
        public DOTweenUIEffects Effects=DOTweenUIEffects.Fade;
        [Min(0)] public float Duration=.2f;
        public Ease Ease=Ease.OutCubic;
        [Min(0)] public float Scale=.9f;
        public Vector2 Offset=new Vector2(0,-60);
        internal DOTweenTransitionSettings Copy()
        {
            if(float.IsNaN(Duration)||float.IsInfinity(Duration)||Duration<0||float.IsNaN(Scale)||float.IsInfinity(Scale)||Scale<0||
                float.IsNaN(Offset.x)||float.IsInfinity(Offset.x)||float.IsNaN(Offset.y)||float.IsInfinity(Offset.y)||
                (Effects&~(DOTweenUIEffects.Fade|DOTweenUIEffects.Scale|DOTweenUIEffects.Slide))!=0||!Enum.IsDefined(typeof(Ease),Ease))
                throw new ArgumentException("Invalid DOTween transition settings.");
            return new DOTweenTransitionSettings{Effects=Effects,Duration=Duration,Ease=Ease,Scale=Scale,Offset=Offset};
        }
        internal UITransitionWrites Writes=>((Effects&DOTweenUIEffects.Fade)!=0?UITransitionWrites.Opacity:0)|
            ((Effects&DOTweenUIEffects.Scale)!=0?UITransitionWrites.Scale:0)|((Effects&DOTweenUIEffects.Slide)!=0?UITransitionWrites.Position:0);
    }
    [CreateAssetMenu(menuName="Wulato/UIKit/Transitions/DOTween Single")]
    public sealed class DOTweenTransitionPreset:UITransitionPreset
    {
        public DOTweenTransitionSettings Settings=new DOTweenTransitionSettings();
        public override IUITransition CreateTransition()=>new DOTweenUITransition(Settings);
    }
}
