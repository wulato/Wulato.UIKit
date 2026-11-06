using System;
using System.Collections;
using System.IO;
using System.Threading;
using Cysharp.Threading.Tasks;
using DG.Tweening;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using Wulato.UIKit.Integrations.DOTween;
using Wulato.UIKit.Tests.Support;
using Wulato.UIKit.Navigation;
using TweenEngine=DG.Tweening.DOTween;
namespace Wulato.UIKit.DOTween.Tests
{
    public sealed class TweenView:UIView
    {
        internal UISessionScope Scope;
        protected override UniTask OnOpenAsync(UISessionScope scope,CancellationToken token){Scope=scope;return UniTask.CompletedTask;}
    }
    public sealed class TweenPageA:UIView { }
    public sealed class TweenPageB:UIView { }
    internal sealed class Surface:IDisposable
    {
        internal readonly UIFlowTestHarness Fixture;
        internal readonly UITransitionContext Context;
        internal Surface()
        {
            var registry=new UIRegistryBuilder();registry.Add<TweenView>(new UIViewTypeKey("tween"),UIViewAsset.Resources("tween"),UILayer.Modal);
            Fixture=new UIFlowTestHarness(registry.Build());Fixture.UI.OpenAsync<TweenView>().GetAwaiter().GetResult();
            var go=new GameObject("AnimationRoot",typeof(RectTransform),typeof(CanvasGroup));go.transform.SetParent(Fixture.LastView.transform,false);
            Context=new UITransitionContext(((TweenView)Fixture.LastView).Scope,(RectTransform)go.transform,go.GetComponent<CanvasGroup>());
        }
        public void Dispose()=>Fixture.Dispose();
    }
    [Category("dotween")]
    public sealed class DOTweenTransitionTests
    {
        static DOTweenTransitionSettings Settings(float duration=10,DOTweenUIEffects effects=DOTweenUIEffects.Fade)=>new DOTweenTransitionSettings{Duration=duration,Effects=effects};
        static async UniTask Canceled(UniTask task){try{await task;Assert.Fail("Expected cancellation");}catch(OperationCanceledException){}}
        [Serializable] sealed class CaseTrace {public string version,caseName,status;public int activeSequences;}
        [TearDown] public void Trace()
        {
            var dir=Environment.GetEnvironmentVariable("UIKIT_EVIDENCE_DIR");if(string.IsNullOrEmpty(dir))return;
            var payload=JsonUtility.ToJson(new CaseTrace{version=TweenEngine.Version,caseName=TestContext.CurrentContext.Test.FullName,status=TestContext.CurrentContext.Result.Outcome.Status.ToString(),activeSequences=TweenEngine.TotalActiveSequences()});
            File.WriteAllText(Path.Combine(dir,TestContext.CurrentContext.Test.Name+".trace.json"),payload);
            File.WriteAllText(Path.Combine(dir,"dotween.trace.json"),payload);
        }
        static async UniTask CoreFailure(bool timeout)
        {
            var registry=new UIRegistryBuilder();registry.Add<TweenPageA>(new UIViewTypeKey("a"),UIViewAsset.Resources("a"));registry.Add<TweenPageB>(new UIViewTypeKey("b"),UIViewAsset.Resources("b"));
            var pair=new DOTweenUITransition(Settings(.15f,DOTweenUIEffects.Fade|DOTweenUIEffects.Scale|DOTweenUIEffects.Slide));
            using(var f=new UIFlowTestHarness(registry.Build(),pair))using(var caller=new CancellationTokenSource())
            {
                var root=await f.UI.OpenAsync<TweenPageA>();var request=f.UI.PushAsync<TweenPageB>(caller.Token);await UniTask.NextFrame();
                if(timeout)f.Clock.Advance(3);else caller.Cancel();
                try{await request;Assert.Fail("Expected failure before page commit");}
                catch(OperationCanceledException){Assert.That(timeout,Is.False);}
                catch(UIException error){Assert.That(timeout,Is.True);Assert.That(error.Code,Is.EqualTo(UIErrorCode.Timeout));}
                Assert.That(root.State,Is.EqualTo(UIState.Open));Assert.That(f.UI.GetService<IUINavigator>().History.Count,Is.EqualTo(1));Assert.That(f.UI.GetDiagnostics().PendingCleanupCount,Is.Zero);Assert.That(f.UI.GetDiagnostics().InputLockCount,Is.Zero);Assert.That(f.Releases,Is.EqualTo(1));
            }
        }
        [UnityTest] public IEnumerator CorePageCancellation_RollsBackTheActualTweenPair()=>UniTask.ToCoroutine(()=>CoreFailure(false));
        [UnityTest] public IEnumerator CorePageTimeout_StopsWithoutOrphaningTheActualTweenPair()=>UniTask.ToCoroutine(()=>CoreFailure(true));
        [UnityTest] public IEnumerator BusinessAutoKillDefaultFalse_DoesNotRetainCompletedOwnedSequences()=>UniTask.ToCoroutine(async()=>
        {
            bool original=TweenEngine.defaultAutoKill;TweenEngine.defaultAutoKill=false;
            try
            {
                using(var s=new Surface())
                {
                    int before=TweenEngine.TotalActiveSequences();var t=new DOTweenUITransition(Settings(.01f));await t.EnterAsync(s.Context,CancellationToken.None);await UniTask.NextFrame();
                    Assert.That(TweenEngine.TotalActiveSequences(),Is.EqualTo(before),"The adapter owns and must release its completed sequence independently of business defaults.");
                }
            }finally{TweenEngine.defaultAutoKill=original;}
        });
        [Test] public void SdkIdentityAndPresetFactories_AreExplicitAndContextLocal()
        {
            Assert.That(TweenEngine.Version,Is.EqualTo("1.3.045"));
            var preset=ScriptableObject.CreateInstance<DOTweenTransitionPreset>();var pair=ScriptableObject.CreateInstance<DOTweenNavigationPreset>();try
            {
                Assert.That(preset.CreateTransition(),Is.Not.SameAs(preset.CreateTransition()));Assert.That(pair.CreateTransition(),Is.Not.SameAs(pair.CreateTransition()));
                preset.Settings.Duration=float.NaN;Assert.Throws<ArgumentException>(()=>preset.CreateTransition());
            }finally{UnityEngine.Object.DestroyImmediate(preset);UnityEngine.Object.DestroyImmediate(pair);}
        }
        [Test] public void ZeroDuration_CompletesBeforeReturnAndStopPreservesSuccess()
        {
            using(var s=new Surface())
            {
                var t=new DOTweenUITransition(Settings(0));var task=t.EnterAsync(s.Context,CancellationToken.None);t.Stop(s.Context);t.Stop(s.Context);
                Assert.That(task.Status,Is.EqualTo(UniTaskStatus.Succeeded));task.GetAwaiter().GetResult();
            }
        }
        [UnityTest] public IEnumerator StopReturns_OperationCanceledAndNoMoreWrites()=>UniTask.ToCoroutine(async()=>
        {
            using(var s=new Surface())
            {
                var t=new DOTweenUITransition(Settings(10,DOTweenUIEffects.Scale));var task=t.EnterAsync(s.Context,CancellationToken.None);
                await UniTask.NextFrame();t.Stop(s.Context);var scale=s.Context.AnimationRoot.localScale;Assert.That(task.Status,Is.EqualTo(UniTaskStatus.Canceled));await Canceled(task);
                await UniTask.NextFrame();await UniTask.NextFrame();Assert.That(s.Context.AnimationRoot.localScale,Is.EqualTo(scale));
            }
        });
        [UnityTest] public IEnumerator FadeScaleSlide_RestoreOnlyDeclaredBaselines()=>UniTask.ToCoroutine(async()=>
        {
            using(var s=new Surface())
            {
                var c=s.Context;c.AnimationRoot.localScale=new Vector3(2,3,1);c.AnimationRoot.anchoredPosition=new Vector2(11,13);c.AnimationOpacity.alpha=.65f;
                var t=new DOTweenUITransition(Settings(.025f,DOTweenUIEffects.Fade|DOTweenUIEffects.Scale|DOTweenUIEffects.Slide));c.Capture(t.Writes);
                await t.EnterAsync(c,CancellationToken.None);Assert.That(c.AnimationOpacity.alpha,Is.EqualTo(.65f).Within(.001f));Assert.That(c.AnimationRoot.localScale,Is.EqualTo(new Vector3(2,3,1)));
                await t.ExitAsync(c,CancellationToken.None);c.AnimationRoot.localRotation=Quaternion.Euler(0,0,33);t.Reset(c);
                Assert.That(c.AnimationOpacity.alpha,Is.EqualTo(.65f));Assert.That(c.AnimationRoot.localScale,Is.EqualTo(new Vector3(2,3,1)));Assert.That(c.AnimationRoot.anchoredPosition,Is.EqualTo(new Vector2(11,13)));Assert.That(Quaternion.Angle(c.AnimationRoot.localRotation,Quaternion.Euler(0,0,33)),Is.LessThan(.001f));
            }
        });
        [UnityTest] public IEnumerator TimeScaleZero_UsesUnscaledUpdate()=>UniTask.ToCoroutine(async()=>
        {
            float old=Time.timeScale;Time.timeScale=0;try
            {using(var s=new Surface()){var t=new DOTweenUITransition(Settings(.025f));await t.EnterAsync(s.Context,CancellationToken.None);Assert.That(s.Context.AnimationOpacity.alpha,Is.EqualTo(1));}}
            finally{Time.timeScale=old;}
        });
        [UnityTest] public IEnumerator CompletionContinuation_AutoKillCannotCancelTheNewPlayback()=>UniTask.ToCoroutine(async()=>
        {
            using(var s=new Surface())
            {
                var t=new DOTweenUITransition(Settings(.01f));var first=t.EnterAsync(s.Context,CancellationToken.None);UniTask next=default;bool restarted=false;
                var awaiter=first.GetAwaiter();awaiter.OnCompleted(()=>{awaiter.GetResult();next=t.EnterAsync(s.Context,CancellationToken.None);restarted=true;});
                await UniTask.WaitUntil(()=>restarted);Assert.That(next.Status,Is.Not.EqualTo(UniTaskStatus.Canceled));
                if(next.Status==UniTaskStatus.Pending){t.Stop(s.Context);await Canceled(next);}else await next;
            }
        });
        [UnityTest] public IEnumerator StopContinuation_RestartRemainsStoppable()=>UniTask.ToCoroutine(async()=>
        {
            using(var s=new Surface())
            {
                var t=new DOTweenUITransition(Settings());var first=t.EnterAsync(s.Context,CancellationToken.None);UniTask second=default;
                var awaiter=first.GetAwaiter();awaiter.OnCompleted(()=>{try{awaiter.GetResult();}catch(OperationCanceledException){}second=t.EnterAsync(s.Context,CancellationToken.None);});
                t.Stop(s.Context);Assert.That(second.Status,Is.EqualTo(UniTaskStatus.Pending));await UniTask.NextFrame();t.Stop(s.Context);await Canceled(second);
            }
        });
        [UnityTest] public IEnumerator LateWorkerCancellation_DoesNotKillAReplacementPlayback()=>UniTask.ToCoroutine(async()=>
        {
            using(var s=new Surface())using(var ct=new CancellationTokenSource())
            {
                var t=new DOTweenUITransition(Settings());var first=t.EnterAsync(s.Context,ct.Token);
                System.Threading.Tasks.Task.Run(()=>ct.Cancel()).GetAwaiter().GetResult();t.Stop(s.Context);await Canceled(first);
                var next=t.EnterAsync(s.Context,CancellationToken.None);await UniTask.NextFrame();Assert.That(next.Status,Is.EqualTo(UniTaskStatus.Pending));t.Stop(s.Context);await Canceled(next);
            }
        });
        [UnityTest] public IEnumerator PairStop_CancelsBothSurfacesAndKeepsOtherContextAndTweenAlive()=>UniTask.ToCoroutine(async()=>
        {
            using(var a=new Surface())using(var b=new Surface())using(var other=new Surface())
            {
                float unrelated=0;var sentinel=TweenEngine.To(()=>unrelated,v=>unrelated=v,1,10);float global=TweenEngine.timeScale;
                try
                {
                    var t=new DOTweenUITransition(Settings());var pair=new UINavigationTransitionContext(a.Context,b.Context,Guid.NewGuid());a.Context.Capture(t.FromWrites);b.Context.Capture(t.ToWrites);
                    var play=t.PlayAsync(pair,CancellationToken.None);var live=t.EnterAsync(other.Context,CancellationToken.None);await UniTask.NextFrame();t.Stop(pair);await Canceled(play);
                    var aa=a.Context.AnimationOpacity.alpha;var bb=b.Context.AnimationOpacity.alpha;await UniTask.NextFrame();Assert.That(a.Context.AnimationOpacity.alpha,Is.EqualTo(aa));Assert.That(b.Context.AnimationOpacity.alpha,Is.EqualTo(bb));
                    Assert.That(live.Status,Is.EqualTo(UniTaskStatus.Pending));Assert.That(sentinel.IsActive(),Is.True);Assert.That(TweenEngine.timeScale,Is.EqualTo(global));t.Reset(pair);Assert.That(a.Context.AnimationOpacity.alpha,Is.EqualTo(1));t.Stop(other.Context);await Canceled(live);
                }finally{sentinel.Kill(false);}
            }
        });
        [UnityTest] public IEnumerator PairCompletion_UsesTheLongestParallelDuration()=>UniTask.ToCoroutine(async()=>
        {
            using(var a=new Surface())using(var b=new Surface())
            {
                var t=new DOTweenUITransition(Settings(.025f),Settings(.08f));var pair=new UINavigationTransitionContext(a.Context,b.Context,Guid.NewGuid());
                double start=Time.realtimeSinceStartupAsDouble;await t.PlayAsync(pair,CancellationToken.None);
                Assert.That(Time.realtimeSinceStartupAsDouble-start,Is.GreaterThanOrEqualTo(.07));Assert.That(b.Context.AnimationOpacity.alpha,Is.EqualTo(1));Assert.That(a.Context.AnimationOpacity.alpha,Is.EqualTo(0));
            }
        });
    }
}
