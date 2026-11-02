using System;
using System.Collections;
using System.Threading;
using Cysharp.Threading.Tasks;
using NUnit.Framework;
using UnityEngine.TestTools;
using UnityEngine;
using Wulato.UIKit.Tests.Support;
namespace Wulato.UIKit.Tests.PlayMode
{
    [Category("transitions")]
    public class NavigationTransactionTests : SessionTestBase
    {
        sealed class Participant : IUITransactionParticipant
        {
            public int State;public bool Fail,FailCommit;
            public void Prepare(){if(Fail)throw new InvalidOperationException("prepare");}
            public void Commit(){State=1;if(FailCommit)throw new InvalidOperationException("commit");}
            public void Rollback(){State=0;}
        }
        sealed class Pair : INavigationTransition
        {
            public int Plays,Stops,Resets;public Func<UINavigationTransitionContext,CancellationToken,UniTask> Run;
            public UITransitionWrites FromWrites=>UITransitionWrites.Opacity;
            public UITransitionWrites ToWrites=>UITransitionWrites.Opacity;
            public UniTask PlayAsync(UINavigationTransitionContext context,CancellationToken ct)
            {Plays++;return Run?.Invoke(context,ct)??UniTask.CompletedTask;}
            public void Stop(UINavigationTransitionContext context){Stops++;}
            public void Reset(UINavigationTransitionContext context){Resets++;}
        }
        [UnityTest] public IEnumerator PagePair_CommitsStateWithoutCoreDefault() => UniTask.ToCoroutine(async()=>
        {
            var transition=new TransitionTests.ControlledTransition{BlockEnter=false};
            await using(var f=new CleanupRuntimeFixture(transactions:true,transition:transition))
            {
                var participant=new Participant();var pair=new Pair();IUISession page=null;
                await f.UI.GetService<IUIPageTransactionAccess>().ExecuteAsync(async(steps,ct)=>
                {
                    page=await steps.PrepareAsync(new UIViewTypeKey("probe"),UIArguments.None,UITransitionOwner.External,ct);
                    pair.Run=(context,token)=>{Assert.That(context.From,Is.Null);Assert.That(page.State,Is.EqualTo(UIState.Opening));return UniTask.CompletedTask;};
                    await steps.AnimatePairAsync(null,page,pair,ct);steps.CommitReplacement(null,page,participant);
                },TimeSpan.FromSeconds(2));
                Assert.That(participant.State,Is.EqualTo(1));Assert.That(page.State,Is.EqualTo(UIState.Open));
                Assert.That(pair.Plays,Is.EqualTo(1));Assert.That(transition.Enters+transition.Exits,Is.Zero);await page.CloseAsync();
            }
        });
        [UnityTest] public IEnumerator ParticipantPrepareFails_RestoresOldPage() => UniTask.ToCoroutine(async()=>
        {
            await using(var f=new CleanupRuntimeFixture(transactions:true,multi:true))
            {
                var old=await f.UI.OpenAsync<ProbeView>();var participant=new Participant{Fail=true};
                try{await f.UI.GetService<IUIPageTransactionAccess>().ExecuteAsync(async(steps,ct)=>
                {var next=await steps.PrepareAsync(new UIViewTypeKey("probe"),UIArguments.None,UITransitionOwner.External,ct);steps.CommitReplacement(old,next,participant);},TimeSpan.FromSeconds(2));Assert.Fail();}
                catch(UIException error){Assert.That(error.InnerException,Is.TypeOf<InvalidOperationException>());Assert.That(error.InnerException.Message,Is.EqualTo("prepare"));}
                Assert.That(f.Loader.CreateCount,Is.EqualTo(2));Assert.That(participant.State,Is.Zero);Assert.That(old.State,Is.EqualTo(UIState.Open));
                Assert.That(f.UI.GetDiagnostics().InputOwner.Value.SessionId,Is.EqualTo(old.Id));await old.CloseAsync();
            }
        });
        [UnityTest] public IEnumerator ParticipantCommitFailure_RestoresPublishedStateAndOldOwner() => UniTask.ToCoroutine(async()=>
        {
            await using(var f=new CleanupRuntimeFixture(transactions:true,multi:true))
            {
                var old=await f.UI.OpenAsync<ProbeView>();var participant=new Participant{FailCommit=true};IUISession next=null;
                try
                {
                    await f.UI.GetService<IUIPageTransactionAccess>().ExecuteAsync(async(steps,ct)=>
                    {next=await steps.PrepareAsync(new UIViewTypeKey("probe"),UIArguments.None,UITransitionOwner.External,ct);steps.CommitReplacement(old,next,participant);},TimeSpan.FromSeconds(2));Assert.Fail();
                }
                catch(UIException error){Assert.That(error.InnerException.Message,Is.EqualTo("commit"));}
                Assert.That(participant.State,Is.Zero);Assert.That(old.State,Is.EqualTo(UIState.Open));Assert.That(next.State,Is.EqualTo(UIState.Closed));
                Assert.That(f.UI.GetDiagnostics().InputOwner.Value.SessionId,Is.EqualTo(old.Id));Assert.That(f.Errors,Is.Not.Empty);await old.CloseAsync();
            }
        });
        [UnityTest] public IEnumerator Pair_RestoresBothDeclaredSurfacesWithoutOverwritingBusinessLayout() => UniTask.ToCoroutine(async()=>
        {
            await using(var f=new CleanupRuntimeFixture(transactions:true,multi:true))
            {
                var old=await f.UI.OpenAsync<ProbeView>();var pair=new Pair();UINavigationTransitionContext surfaces=null;RectTransform business=null;IUISession next=null;
                pair.Run=(context,ct)=>
                {
                    surfaces=context;context.From.AnimationOpacity.alpha=0.2f;context.To.AnimationOpacity.alpha=0.3f;
                    business=(RectTransform)f.Loader.LastView.transform;business.sizeDelta=new Vector2(321,456);
                    Assert.That(f.Input.Locks,Is.GreaterThan(0));return UniTask.CompletedTask;
                };
                await f.UI.GetService<IUIPageTransactionAccess>().ExecuteAsync(async(steps,ct)=>
                {next=await steps.PrepareAsync(new UIViewTypeKey("probe"),UIArguments.None,UITransitionOwner.External,ct);await steps.AnimatePairAsync(old,next,pair,ct);steps.CommitReplacement(old,next,new Participant());},TimeSpan.FromSeconds(2));
                Assert.That(surfaces.From.AnimationOpacity.alpha,Is.EqualTo(1));Assert.That(surfaces.To.AnimationOpacity.alpha,Is.EqualTo(1));
                Assert.That(business.sizeDelta,Is.EqualTo(new Vector2(321,456)));Assert.That(pair.Resets,Is.EqualTo(1));
                await old.CloseAsync();await next.CloseAsync();
            }
        });
        [UnityTest] public IEnumerator CallerCancelAfterCommit_StillClosesOldPage() => UniTask.ToCoroutine(async()=>
        {
            await using(var f=new CleanupRuntimeFixture(transactions:true,multi:true))using(var caller=new CancellationTokenSource())
            {
                var old=await f.UI.OpenAsync<ProbeView>();IUISession next=null;
                await f.UI.GetService<IUIPageTransactionAccess>().ExecuteAsync(async(steps,ct)=>
                {next=await steps.PrepareAsync(new UIViewTypeKey("probe"),UIArguments.None,UITransitionOwner.External,ct);steps.CommitReplacement(old,next,new Participant());caller.Cancel();await steps.CloseAsync(old,UITransitionOwner.External);},TimeSpan.FromSeconds(2),caller.Token);
                Assert.That(old.State,Is.EqualTo(UIState.Closed));Assert.That(next.State,Is.EqualTo(UIState.Open));await next.CloseAsync();
            }
        });
        [UnityTest] public IEnumerator CancellationInsideInputCommit_RollsBackBeforePublication() => UniTask.ToCoroutine(async()=>
        {
            await using(var f=new CleanupRuntimeFixture(transactions:true,multi:true))using(var caller=new CancellationTokenSource())
            {
                var old=await f.UI.OpenAsync<ProbeView>();var participant=new Participant();IUISession next=null;
                f.Input.Committing=grant=>caller.Cancel();Exception failure=null;
                try
                {
                    await f.UI.GetService<IUIPageTransactionAccess>().ExecuteAsync(async(steps,ct)=>
                    {next=await steps.PrepareAsync(new UIViewTypeKey("probe"),UIArguments.None,UITransitionOwner.External,ct);steps.CommitReplacement(old,next,participant);},TimeSpan.FromSeconds(2),caller.Token);
                }
                catch(Exception error){failure=error;}
                // Cancellation callbacks can run continuations inline; inspect outside the backend callback.
                await UniTask.NextFrame();f.Input.Committing=null;
                Assert.That(failure,Is.InstanceOf<OperationCanceledException>());
                Assert.That(participant.State,Is.Zero);Assert.That(old.State,Is.EqualTo(UIState.Open));Assert.That(next.State,Is.EqualTo(UIState.Closed));
                Assert.That(f.UI.GetDiagnostics().InputOwner.Value.SessionId,Is.EqualTo(old.Id));await old.CloseAsync();
            }
        });
        [UnityTest] public IEnumerator Pair_ShowWindowIsVisibleButNotInteractive() => UniTask.ToCoroutine(async()=>
        {
            await using(var f=new CleanupRuntimeFixture(transactions:true))
            {
                var gate=new UniTaskCompletionSource();var pair=new Pair{Run=(c,ct)=>gate.Task};IUISession page=null;
                var request=f.UI.GetService<IUIPageTransactionAccess>().ExecuteAsync(async(steps,ct)=>
                {page=await steps.PrepareAsync(new UIViewTypeKey("probe"),UIArguments.None,UITransitionOwner.External,ct);await steps.AnimatePairAsync(null,page,pair,ct);steps.CommitReplacement(null,page,new Participant());},TimeSpan.FromSeconds(2));
                await UniTask.NextFrame();var shell=f.Loader.LastView.transform.parent.parent.GetComponent<CanvasGroup>();
                Assert.That(shell.alpha,Is.EqualTo(1));Assert.That(shell.interactable,Is.False);Assert.That(page.State,Is.EqualTo(UIState.Opening));
                gate.TrySetResult();await request;await page.CloseAsync();
            }
        });
        [UnityTest] public IEnumerator Pair_PlayEntryAlreadyHasVisibleNonInteractiveTarget() => UniTask.ToCoroutine(async()=>
        {
            await using(var f=new CleanupRuntimeFixture(transactions:true))
            {
                var pair=new Pair{Run=(context,ct)=>
                {
                    var shell=f.Loader.LastView.transform.parent.parent.GetComponent<CanvasGroup>();
                    Assert.That(shell.alpha,Is.EqualTo(1),"paired adapter must see its target at PlayAsync entry");
                    Assert.That(shell.interactable,Is.False);return UniTask.CompletedTask;
                }};
                IUISession page=null;
                await f.UI.GetService<IUIPageTransactionAccess>().ExecuteAsync(async(steps,ct)=>
                {page=await steps.PrepareAsync(new UIViewTypeKey("probe"),UIArguments.None,UITransitionOwner.External,ct);await steps.AnimatePairAsync(null,page,pair,ct);steps.CommitReplacement(null,page,new Participant());},TimeSpan.FromSeconds(2));
                await page.CloseAsync();
            }
        });
        [UnityTest] public IEnumerator PairTimeout_StopsAndQuarantinesUncooperativeOperation() => UniTask.ToCoroutine(async()=>
        {
            await using(var f=new CleanupRuntimeFixture(transactions:true))
            {
                var gate=new UniTaskCompletionSource();var pair=new Pair{Run=(c,ct)=>gate.Task};
                var request=f.UI.GetService<IUIPageTransactionAccess>().ExecuteAsync(async(steps,ct)=>
                {var page=await steps.PrepareAsync(new UIViewTypeKey("probe"),UIArguments.None,UITransitionOwner.External,ct);await steps.AnimatePairAsync(null,page,pair,ct);},TimeSpan.FromSeconds(2));
                await UniTask.NextFrame();f.Clock.Advance(3);
                try{await request;Assert.Fail();}catch(UIException error){Assert.That(error.Code,Is.EqualTo(UIErrorCode.Timeout));}
                Assert.That(pair.Stops,Is.EqualTo(1));Assert.That(f.UI.GetDiagnostics().Sessions,Is.Empty);
                Assert.That(f.UI.GetDiagnostics().PendingCleanupCount,Is.GreaterThan(0));Assert.That(f.Loader.ReleaseCount,Is.Zero);
                gate.TrySetResult();await UniTask.NextFrame();Assert.That(f.Loader.ReleaseCount,Is.EqualTo(1));
            }
        });
        [UnityTest] public IEnumerator PairFailure_IsTransitionFailed() => UniTask.ToCoroutine(async()=>
        {
            await using(var f=new CleanupRuntimeFixture(transactions:true))
            {
                var pair=new Pair{Run=(context,ct)=>UniTask.FromException(new InvalidOperationException("pair-failure"))};
                try{await f.UI.GetService<IUIPageTransactionAccess>().ExecuteAsync(async(steps,ct)=>
                {var page=await steps.PrepareAsync(new UIViewTypeKey("probe"),UIArguments.None,UITransitionOwner.External,ct);await steps.AnimatePairAsync(null,page,pair,ct);},TimeSpan.FromSeconds(2));Assert.Fail();}
                catch(UIException error){Assert.That(error.Code,Is.EqualTo(UIErrorCode.TransitionFailed));Assert.That(error.InnerException.Message,Is.EqualTo("pair-failure"));}
                Assert.That(f.Loader.ReleaseCount,Is.EqualTo(1));Assert.That(f.UI.GetDiagnostics().Sessions,Is.Empty);
            }
        });
    }
}
