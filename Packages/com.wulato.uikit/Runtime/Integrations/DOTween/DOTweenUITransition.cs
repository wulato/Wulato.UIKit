using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using DG.Tweening;
using UnityEngine;
using TweenEngine=DG.Tweening.DOTween;
namespace Wulato.UIKit.Integrations.DOTween
{
    /// <summary>Owns only its per-play sequences; never changes global DOTween settings or kills unrelated tweens.</summary>
    public sealed class DOTweenUITransition:IUITransition,INavigationTransition
    {
        sealed class Playback
        {
            internal readonly UniTaskCompletionSource Completion=new UniTaskCompletionSource();
            internal Sequence Tween;internal CancellationTokenRegistration Cancellation;internal bool Active=true;
        }
        readonly Dictionary<object,Playback> active=new Dictionary<object,Playback>();
        readonly DOTweenTransitionSettings incoming,outgoing;
        public UITransitionWrites Writes=>incoming.Writes;
        public UITransitionWrites FromWrites=>outgoing.Writes;
        public UITransitionWrites ToWrites=>incoming.Writes;
        public DOTweenUITransition(DOTweenTransitionSettings incoming,DOTweenTransitionSettings outgoing=null)
        {this.incoming=(incoming??throw new ArgumentNullException(nameof(incoming))).Copy();this.outgoing=(outgoing??incoming).Copy();}
        public UniTask EnterAsync(UITransitionContext context,CancellationToken token)=>Play(context,context,null,true,token);
        public UniTask ExitAsync(UITransitionContext context,CancellationToken token)=>Play(context,context,null,false,token);
        public UniTask PlayAsync(UINavigationTransitionContext context,CancellationToken token)=>Play(context,context.To,context.From,true,token);
        bool Current(object key,Playback playback)=>playback.Active&&active.TryGetValue(key,out var value)&&ReferenceEquals(value,playback);
        UniTask Play(object key,UITransitionContext to,UITransitionContext from,bool entering,CancellationToken token)
        {
            if(!PlayerLoopHelper.IsMainThread)throw new InvalidOperationException("DOTween transitions require the main thread.");
            if(key==null||to==null)throw new ArgumentNullException(nameof(key));
            if(token.IsCancellationRequested)return UniTask.FromCanceled(token);
            if(active.ContainsKey(key))throw new InvalidOperationException("This transition is already playing.");
            var p=new Playback();active.Add(key,p);
            try
            {
                p.Tween=TweenEngine.Sequence().Pause().SetAutoKill(true).SetUpdate(UpdateType.Normal,true).SetRecyclable(false);
                p.Tween.OnComplete(()=>Finish(key,p,null));
                p.Tween.OnKill(()=>{if(Current(key,p))Finish(key,p,new OperationCanceledException());});
                var registration=token.Register(()=>
                {
                    if(PlayerLoopHelper.IsMainThread)Cancel(key,p,token);
                    else UniTask.Post(()=>Cancel(key,p,token));
                });
                if(p.Active)p.Cancellation=registration;else registration.Dispose();
                if(Current(key,p))Add(key,p,to,incoming,entering);
                if(from!=null&&Current(key,p))Add(key,p,from,outgoing,false);
                if(Current(key,p))
                {
                    if(p.Tween.Duration(false)<=0){p.Tween.OnKill(null);p.Tween.Kill(false);Finish(key,p,null);}
                    else p.Tween.Play();
                }
            }
            catch(Exception error){if(p.Tween!=null&&p.Tween.IsActive()){p.Tween.OnKill(null);p.Tween.Kill(false);}Finish(key,p,error);}
            return p.Completion.Task;
        }
        void Add(object key,Playback p,UITransitionContext c,DOTweenTransitionSettings s,bool entering)
        {
            var root=c.AnimationRoot;var opacity=c.AnimationOpacity;
            var scale=root.localScale;var position=root.anchoredPosition;float alpha=opacity.alpha;
            bool fade=(s.Effects&DOTweenUIEffects.Fade)!=0,resize=(s.Effects&DOTweenUIEffects.Scale)!=0,slide=(s.Effects&DOTweenUIEffects.Slide)!=0;
            if(entering)
            {
                if(fade&&Current(key,p))opacity.alpha=0;
                if(resize&&Current(key,p))root.localScale=scale*s.Scale;
                if(slide&&Current(key,p))root.anchoredPosition=position+s.Offset;
            }
            if(s.Duration==0)
            {
                if(fade&&Current(key,p))opacity.alpha=entering?alpha:0;
                if(resize&&Current(key,p))root.localScale=entering?scale:scale*s.Scale;
                if(slide&&Current(key,p))root.anchoredPosition=entering?position:position+s.Offset;
                return;
            }
            if(fade&&Current(key,p))p.Tween.Join(TweenEngine.To(()=>opacity.alpha,v=>{if(Current(key,p)&&opacity)opacity.alpha=v;},entering?alpha:0,s.Duration).SetEase(s.Ease));
            if(resize&&Current(key,p))p.Tween.Join(TweenEngine.To(()=>root.localScale,v=>{if(Current(key,p)&&root)root.localScale=v;},entering?scale:scale*s.Scale,s.Duration).SetEase(s.Ease));
            if(slide&&Current(key,p))p.Tween.Join(TweenEngine.To(()=>root.anchoredPosition,v=>{if(Current(key,p)&&root)root.anchoredPosition=v;},entering?position:position+s.Offset,s.Duration).SetEase(s.Ease));
        }
        void Finish(object key,Playback p,Exception error)
        {
            if(!Current(key,p))return;p.Active=false;active.Remove(key);p.Cancellation.Dispose();
            if(error is OperationCanceledException canceled)p.Completion.TrySetCanceled(canceled.CancellationToken);
            else if(error!=null)p.Completion.TrySetException(error);
            else p.Completion.TrySetResult();
        }
        void Cancel(object key,Playback p,CancellationToken token=default)
        {
            if(!Current(key,p))return;
            // Remove identity before Kill/callback publication; a continuation may start a new play immediately.
            p.Active=false;active.Remove(key);p.Tween?.Kill(false);p.Cancellation.Dispose();p.Completion.TrySetCanceled(token);
        }
        public void Stop(UITransitionContext context){if(active.TryGetValue(context,out var p))Cancel(context,p);}
        public void Stop(UINavigationTransitionContext context){if(active.TryGetValue(context,out var p))Cancel(context,p);}
        public void Reset(UITransitionContext context)=>context.RestoreBaseline();
        public void Reset(UINavigationTransitionContext context){context.From?.RestoreBaseline();context.To.RestoreBaseline();}
    }
}
