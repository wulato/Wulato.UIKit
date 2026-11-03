using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using NUnit.Framework;
using Wulato.UIKit.Tests.Support;
namespace Wulato.UIKit.Navigation.Tests
{
    public class PageA:UIView
    {
        public static bool FailClose;
        protected override UniTask OnCloseAsync(UICloseReason reason,CancellationToken ct)
        {if(FailClose)throw new InvalidOperationException("old-page-close");return UniTask.CompletedTask;}
    }
    public class TypedPage:UIView<int>
    {
        public int Received;
        protected override UniTask OnOpenAsync(int args,UISessionScope scope,CancellationToken ct){Received=args;return UniTask.CompletedTask;}
    }
    public class PageB:UIView{}
    public class UnregisteredPage:UIView{}
    [Category("navigation")]
    public class NavigationFlowTests
    {
        UIFlowTestHarness Fixture()
        {
            PageA.FailClose=false;var b=new UIRegistryBuilder();b.Add<PageA>(new UIViewTypeKey("A"),UIViewAsset.Resources("A"),closePolicy:UIClosePolicy.CacheOne);
            b.Add<PageB>(new UIViewTypeKey("B"),UIViewAsset.Resources("B"));
            b.Add<TypedPage,int>(new UIViewTypeKey("typed"),UIViewAsset.Resources("typed"),closePolicy:UIClosePolicy.CacheOne);return new UIFlowTestHarness(b.Build());
        }
        [Test] public void PushBack_RebuildsDescriptorsAndClosesOldSessions()
        {
            using(var f=Fixture())using(var nav=new UINavigator(f.UI))
            {
                f.Observer=nav;
                Assert.That(nav.BackAsync().GetAwaiter().GetResult(),Is.False);
                var a=nav.PushAsync(new UIViewTypeKey("A"),UIArguments.None).GetAwaiter().GetResult();
                var b=nav.PushAsync(new UIViewTypeKey("B"),UIArguments.None).GetAwaiter().GetResult();
                Assert.That(a.State,Is.EqualTo(UIState.Closed));Assert.That(nav.History.Count,Is.EqualTo(2));
                Assert.That(nav.BackAsync().GetAwaiter().GetResult(),Is.True);Assert.That(b.State,Is.EqualTo(UIState.Closed));
                Assert.That(nav.History.Count,Is.EqualTo(1));Assert.That(nav.History[0].Type,Is.EqualTo(new UIViewTypeKey("A")));
            }
        }
        [Test] public void OldPageCloseFailure_DoesNotUndoCommittedHistory()
        {
            using(var f=Fixture())using(var nav=new UINavigator(f.UI))
            {
                f.Observer=nav;var old=nav.PushAsync<PageA>().GetAwaiter().GetResult();PageA.FailClose=true;
                var next=nav.PushAsync<PageB>().GetAwaiter().GetResult();
                Assert.That(next.State,Is.EqualTo(UIState.Open));Assert.That(nav.History.Count,Is.EqualTo(2));
                var completion=old.Closed.GetAwaiter().GetResult();Assert.That(completion.Errors,Is.Not.Empty);
                Assert.That(f.Errors,Is.Not.Empty);Assert.That(completion.Reason,Is.EqualTo(UICloseReason.Owner));
            }
        }
        [Test] public void BackAfterCacheEviction_RebuildsWithOriginalTypedArguments()
        {
            using(var f=Fixture())using(var nav=new UINavigator(f.UI))
            {
                f.Observer=nav;nav.PushAsync<TypedPage,int>(42).GetAwaiter().GetResult();var snapshot=nav.History;
                nav.PushAsync<PageA>().GetAwaiter().GetResult();f.UI.ClearCacheAsync().GetAwaiter().GetResult();
                Assert.That(snapshot.Count,Is.EqualTo(1));Assert.That(nav.BackAsync().GetAwaiter().GetResult(),Is.True);
                Assert.That(((TypedPage)f.LastView).Received,Is.EqualTo(42));Assert.That(f.Creates,Is.EqualTo(3));Assert.That(nav.History.Count,Is.EqualTo(1));
            }
        }
        [Test] public void SingleRepeat_FailsWithoutChangingHistory()
        {
            using(var f=Fixture())using(var nav=new UINavigator(f.UI))
            {
                f.Observer=nav;nav.PushAsync(new UIViewTypeKey("A"),UIArguments.None).GetAwaiter().GetResult();
                Assert.That(Assert.Throws<UIException>(()=>nav.PushAsync(new UIViewTypeKey("A"),UIArguments.None).GetAwaiter().GetResult()).Code,Is.EqualTo(UIErrorCode.AlreadyOpen));
                Assert.That(nav.History.Count,Is.EqualTo(1));
            }
        }
        [Test] public void ExternalCurrentClose_ClearsHistory()
        {
            using(var f=Fixture())using(var nav=new UINavigator(f.UI))
            {
                f.Observer=nav;var page=nav.PushAsync(new UIViewTypeKey("A"),UIArguments.None).GetAwaiter().GetResult();
                page.CloseAsync().GetAwaiter().GetResult();Assert.That(nav.History,Is.Empty);
            }
        }
        sealed class ControlledPair:INavigationTransition
        {
            public bool Block;public int Plays,Stops;
            public readonly UniTaskCompletionSource Gate=new UniTaskCompletionSource();
            public UITransitionWrites FromWrites=>UITransitionWrites.None;
            public UITransitionWrites ToWrites=>UITransitionWrites.None;
            public UniTask PlayAsync(UINavigationTransitionContext context,CancellationToken ct){Plays++;return Block?Gate.Task:UniTask.CompletedTask;}
            public void Stop(UINavigationTransitionContext context){Stops++;Gate.TrySetCanceled();}
            public void Reset(UINavigationTransitionContext context){}
        }
        [Test] public void BlockedPush_HoldsFIFOAndOrdinaryCommands()
        {
            using(var f=Fixture())
            {
                var pair=new ControlledPair();using(var nav=new UINavigator(f.UI,pair))
                {
                    f.Observer=nav;nav.PushAsync<PageA>().GetAwaiter().GetResult();pair.Block=true;
                    var toB=nav.PushAsync<PageB>();var back=nav.BackAsync();var close=f.UI.CloseAllAsync();
                    Assert.That(toB.Status.IsCompleted(),Is.False);Assert.That(back.Status.IsCompleted(),Is.False);Assert.That(close.Status.IsCompleted(),Is.False);
                    pair.Gate.TrySetResult();toB.GetAwaiter().GetResult();Assert.That(back.GetAwaiter().GetResult(),Is.True);close.GetAwaiter().GetResult();
                    Assert.That(pair.Plays,Is.EqualTo(3));Assert.That(nav.History,Is.Empty);
                }
            }
        }
        [Test] public void CancelBeforeCommit_RestoresOldPageAndHistory()
        {
            using(var f=Fixture())using(var caller=new CancellationTokenSource())
            {
                var pair=new ControlledPair();using(var nav=new UINavigator(f.UI,pair))
                {
                    f.Observer=nav;var old=nav.PushAsync<PageA>().GetAwaiter().GetResult();pair.Block=true;
                    var pending=nav.PushAsync<PageB>(caller.Token);caller.Cancel();
                    Assert.Throws<OperationCanceledException>(()=>pending.GetAwaiter().GetResult());
                    Assert.That(old.State,Is.EqualTo(UIState.Open));Assert.That(nav.History.Count,Is.EqualTo(1));Assert.That(pair.Stops,Is.EqualTo(1));
                }
            }
        }
        [Test] public void MissingKey_IsInvalidRequestBeforeLoad()
        {
            using(var f=Fixture())using(var nav=new UINavigator(f.UI))
            {var error=Assert.Throws<UIException>(()=>nav.PushAsync(new UIViewTypeKey("missing"),UIArguments.None).GetAwaiter().GetResult());Assert.That(error.Code,Is.EqualTo(UIErrorCode.InvalidRequest));Assert.That(f.Creates,Is.Zero);}
        }
        [Test] public void UnregisteredGeneric_ReturnsFaultedOperationWithoutSynchronousThrow()
        {
            using(var f=Fixture())using(var nav=new UINavigator(f.UI))
            {
                UniTask<IUISession> pending=default;
                Assert.DoesNotThrow(()=>pending=nav.PushAsync<UnregisteredPage>());
                var error=Assert.Throws<UIException>(()=>pending.GetAwaiter().GetResult());
                Assert.That(error.Code,Is.EqualTo(UIErrorCode.InvalidRequest));Assert.That(f.Creates,Is.Zero);
            }
        }
        [Test] public void ArgumentMismatch_FailsBeforeLoading()
        {
            using(var f=Fixture())using(var nav=new UINavigator(f.UI))
            {
                Assert.Throws<UIException>(()=>nav.PushAsync(new UIViewTypeKey("A"),UIArguments.From(1)).GetAwaiter().GetResult());
                Assert.That(f.Creates,Is.Zero);Assert.That(nav.History,Is.Empty);
            }
        }
    }
}
