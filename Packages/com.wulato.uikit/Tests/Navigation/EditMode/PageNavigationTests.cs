using System;
using System.Linq;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using NUnit.Framework;
using Wulato.UIKit.Tests.Support;
namespace Wulato.UIKit.Navigation.Tests
{
    public sealed class HistoryModal : UIView { }
    public sealed class HistoryTypedModal : UIView<int> { }
    public sealed class HistoryMultiPage : UIView<int>
    {
        internal static readonly List<int> Opened=new List<int>();
        protected override UniTask OnOpenAsync(int args,UISessionScope scope,CancellationToken ct){Opened.Add(args);return UniTask.CompletedTask;}
    }
    public sealed class HistoryRootPage : UIView
    {
        internal static Action Closing;internal static bool Fail;
        protected override bool CanClose(UICloseReason reason)=>false;
        protected override UniTask OnCloseAsync(UICloseReason reason,CancellationToken ct)
        {Closing?.Invoke();if(Fail)throw new InvalidOperationException("old-root-close");return UniTask.CompletedTask;}
    }
    [Category("navigation")]
    public sealed class PageNavigationTests
    {
        sealed class Pair : INavigationTransition
        {
            internal bool Block,Fail;internal int Plays;internal UniTaskCompletionSource Gate=new UniTaskCompletionSource();
            public UITransitionWrites FromWrites=>UITransitionWrites.None;public UITransitionWrites ToWrites=>UITransitionWrites.None;
            public UniTask PlayAsync(UINavigationTransitionContext context,CancellationToken ct)
            {Plays++;return Fail?UniTask.FromException(new InvalidOperationException("pair")):Block?Gate.Task:UniTask.CompletedTask;}
            public void Stop(UINavigationTransitionContext context)=>Gate.TrySetCanceled();
            public void Reset(UINavigationTransitionContext context) { }
        }
        static UIFlowTestHarness Fixture(Pair pair=null)
        {
            PageA.FailClose=false;HistoryRootPage.Closing=null;HistoryRootPage.Fail=false;HistoryMultiPage.Opened.Clear();
            var b=new UIRegistryBuilder();b.Add<PageA>(new UIViewTypeKey("A"),UIViewAsset.Resources("A"),closePolicy:UIClosePolicy.CacheOne);
            b.Add<PageB>(new UIViewTypeKey("B"),UIViewAsset.Resources("B"));b.Add<TypedPage,int>(new UIViewTypeKey("typed"),UIViewAsset.Resources("typed"),closePolicy:UIClosePolicy.CacheOne);
            b.Add<HistoryMultiPage,int>(new UIViewTypeKey("multi"),UIViewAsset.Resources("multi"),multiplicity:UIMultiplicity.Multi);
            b.Add<HistoryRootPage>(new UIViewTypeKey("root"),UIViewAsset.Resources("root"));
            b.Add<HistoryModal>(new UIViewTypeKey("modal"),UIViewAsset.Resources("modal"),UILayer.Modal);
            b.Add<HistoryTypedModal,int>(new UIViewTypeKey("typed-modal"),UIViewAsset.Resources("typed-modal"),UILayer.Modal);
            return new UIFlowTestHarness(b.Build(),pair);
        }
        static string[] Keys(IUINavigator nav)=>nav.History.Select(p=>p.Type.Value).ToArray();
        [Test] public void PageOpen_IsANewRootForTypedAndErasedCalls()
        {
            using(var f=Fixture())
            {
                var nav=f.UI.GetService<IUINavigator>();var first=f.UI.OpenAsync<PageA>().GetAwaiter().GetResult();
                f.UI.PushAsync<TypedPage,int>(42).GetAwaiter().GetResult();var next=f.UI.OpenAsync(new UIViewTypeKey("B"),UIArguments.None).GetAwaiter().GetResult();
                Assert.That(Keys(nav),Is.EqualTo(new[]{"B"}));Assert.That(first.State,Is.EqualTo(UIState.Closed));
                Assert.That(f.UI.BackAsync().GetAwaiter().GetResult(),Is.False);f.UI.CloseAsync(first).GetAwaiter().GetResult();Assert.That(next.State,Is.EqualTo(UIState.Open));
            }
        }
        [Test] public void Replace_ActsAsFirstPageOrOnlyReplacesTheTop()
        {
            using(var f=Fixture())
            {
                var nav=f.UI.GetService<IUINavigator>();f.UI.ReplaceAsync<HistoryRootPage>().GetAwaiter().GetResult();
                var typed=f.UI.PushAsync<TypedPage,int>(17).GetAwaiter().GetResult();f.UI.ReplaceAsync<PageB>().GetAwaiter().GetResult();
                Assert.That(Keys(nav),Is.EqualTo(new[]{"root","B"}));Assert.That(typed.State,Is.EqualTo(UIState.Closed));
                Assert.That(f.UI.BackAsync().GetAwaiter().GetResult(),Is.True);Assert.That(Keys(nav),Is.EqualTo(new[]{"root"}));
            }
        }
        [Test] public void PopTo_RestoresNearestTypedDescriptorAndSkipsIntermediateLifecycles()
        {
            using(var f=Fixture())
            {
                var nav=f.UI.GetService<IUINavigator>();f.UI.PushAsync<HistoryMultiPage,int>(1).GetAwaiter().GetResult();f.UI.PushAsync<PageA>().GetAwaiter().GetResult();
                f.UI.PushAsync<HistoryMultiPage,int>(2).GetAwaiter().GetResult();f.UI.PushAsync<PageB>().GetAwaiter().GetResult();f.UI.ClearCacheAsync().GetAwaiter().GetResult();int before=f.Creates;
                Assert.That(f.UI.PopToAsync<HistoryMultiPage>().GetAwaiter().GetResult(),Is.True);Assert.That(Keys(nav),Is.EqualTo(new[]{"multi","A","multi"}));
                Assert.That(HistoryMultiPage.Opened,Is.EqualTo(new[]{1,2,2}));Assert.That(f.Creates,Is.EqualTo(before+1));
                Assert.That(f.UI.PopToAsync<HistoryMultiPage>().GetAwaiter().GetResult(),Is.False);Assert.That(f.UI.PopToAsync<UnregisteredPage>().GetAwaiter().GetResult(),Is.False);
                Assert.That(f.UI.PopToAsync(new UIViewTypeKey("absent")).GetAwaiter().GetResult(),Is.False);Assert.That(f.Creates,Is.EqualTo(before+1));
            }
        }
        [Test] public void ModalOpen_DoesNotChangeHistoryOrCloseTheCurrentPage()
        {
            var pair=new Pair();using(var f=Fixture(pair))
            {
                var nav=f.UI.GetService<IUINavigator>();var page=f.UI.OpenAsync<PageA>().GetAwaiter().GetResult();var snapshot=nav.History;int plays=pair.Plays;
                var a=f.UI.OpenAsync<HistoryModal>().GetAwaiter().GetResult();var b=f.UI.OpenAsync(new UIViewTypeKey("typed-modal"),UIArguments.From(7)).GetAwaiter().GetResult();
                Assert.That(nav.History,Is.SameAs(snapshot));Assert.That(page.State,Is.EqualTo(UIState.Open));Assert.That(pair.Plays,Is.EqualTo(plays));
                a.CloseAsync().GetAwaiter().GetResult();b.CloseAsync().GetAwaiter().GetResult();Assert.That(nav.History,Is.SameAs(snapshot));
            }
        }
        [Test] public void CurrentSingle_OpenPushAndReplaceFailWithoutPublishingHistory()
        {
            using(var f=Fixture())
            {
                var nav=f.UI.GetService<IUINavigator>();var page=f.UI.OpenAsync<PageA>().GetAwaiter().GetResult();var snapshot=nav.History;int creates=f.Creates;
                Assert.That(Assert.Throws<UIException>(()=>f.UI.OpenAsync<PageA>().GetAwaiter().GetResult()).Code,Is.EqualTo(UIErrorCode.AlreadyOpen));
                Assert.That(Assert.Throws<UIException>(()=>f.UI.PushAsync<PageA>().GetAwaiter().GetResult()).Code,Is.EqualTo(UIErrorCode.AlreadyOpen));
                Assert.That(Assert.Throws<UIException>(()=>f.UI.ReplaceAsync<PageA>().GetAwaiter().GetResult()).Code,Is.EqualTo(UIErrorCode.AlreadyOpen));
                Assert.That(nav.History,Is.SameAs(snapshot));Assert.That(f.Creates,Is.EqualTo(creates));Assert.That(page.State,Is.EqualTo(UIState.Open));
            }
        }
        [Test] public void FailedRootAndReplace_RestoreTheOldPageHistoryAndOwner()
        {
            var pair=new Pair();using(var f=Fixture(pair))
            {
                var nav=f.UI.GetService<IUINavigator>();var old=f.UI.OpenAsync<PageA>().GetAwaiter().GetResult();var snapshot=nav.History;pair.Fail=true;
                Assert.Throws<UIException>(()=>f.UI.OpenAsync<PageB>().GetAwaiter().GetResult());Assert.Throws<UIException>(()=>f.UI.ReplaceAsync<TypedPage,int>(8).GetAwaiter().GetResult());
                Assert.That(nav.History,Is.SameAs(snapshot));Assert.That(old.State,Is.EqualTo(UIState.Open));Assert.That(f.UI.GetDiagnostics().InputOwner.Value.SessionId,Is.EqualTo(old.Id));
            }
        }
        [Test] public void CancelBeforeRootOrPopToCommit_RestoresCurrentHistoryAndInputOwner()
        {
            var pair=new Pair();using(var f=Fixture(pair))
            {
                var nav=f.UI.GetService<IUINavigator>();f.UI.OpenAsync<TypedPage,int>(31).GetAwaiter().GetResult();var old=f.UI.PushAsync<PageA>().GetAwaiter().GetResult();var snapshot=nav.History;
                using(var ct=new CancellationTokenSource())
                {pair.Block=true;var pending=f.UI.OpenAsync<PageB>(ct.Token);ct.Cancel();Assert.Throws<OperationCanceledException>(()=>pending.GetAwaiter().GetResult());}
                Assert.That(nav.History,Is.SameAs(snapshot));Assert.That(old.State,Is.EqualTo(UIState.Open));
                pair.Gate=new UniTaskCompletionSource();using(var ct=new CancellationTokenSource())
                {var pending=f.UI.PopToAsync<TypedPage>(ct.Token);ct.Cancel();Assert.Throws<OperationCanceledException>(()=>pending.GetAwaiter().GetResult());}
                Assert.That(nav.History,Is.SameAs(snapshot));Assert.That(f.UI.GetDiagnostics().InputOwner.Value.SessionId,Is.EqualTo(old.Id));
            }
        }
        [Test] public void CancellationInsideInputCommit_RollsBackRootPublicationAndOwner()
        {
            using(var f=Fixture())using(var ct=new CancellationTokenSource())
            {
                var old=f.UI.OpenAsync<PageA>().GetAwaiter().GetResult();var nav=f.UI.GetService<IUINavigator>();var snapshot=nav.History;
                f.Input.Committing=_=>ct.Cancel();
                try{Assert.Throws<OperationCanceledException>(()=>f.UI.ReplaceAsync<PageB>(ct.Token).GetAwaiter().GetResult());}
                finally{f.Input.Committing=null;}
                Assert.That(nav.History,Is.SameAs(snapshot));Assert.That(old.State,Is.EqualTo(UIState.Open));Assert.That(f.UI.GetDiagnostics().InputOwner.Value.SessionId,Is.EqualTo(old.Id));
            }
        }
        [Test] public void PostCommitCancellationAndOldCloseFailure_DoNotUndoNewRoot()
        {
            using(var f=Fixture())using(var ct=new CancellationTokenSource())
            {
                var old=f.UI.OpenAsync<HistoryRootPage>().GetAwaiter().GetResult();HistoryRootPage.Closing=ct.Cancel;HistoryRootPage.Fail=true;
                var next=f.UI.OpenAsync<PageB>(ct.Token).GetAwaiter().GetResult();Assert.That(ct.IsCancellationRequested,Is.True);Assert.That(next.State,Is.EqualTo(UIState.Open));
                Assert.That(Keys(f.UI.GetService<IUINavigator>()),Is.EqualTo(new[]{"B"}));Assert.That(old.Closed.GetAwaiter().GetResult().Errors,Is.Not.Empty);
            }
        }
        [Test] public void QueuedRootPushAndPopTo_ReadHistoryWhenTheirFifoTurnStarts()
        {
            var pair=new Pair();using(var f=Fixture(pair))
            {
                f.UI.OpenAsync<TypedPage,int>(9).GetAwaiter().GetResult();pair.Block=true;
                var root=f.UI.OpenAsync<PageB>();var push=f.UI.PushAsync<PageA>();var pop=f.UI.PopToAsync<TypedPage>();Assert.That(push.Status.IsCompleted(),Is.False);
                pair.Block=false;pair.Gate.TrySetResult();root.GetAwaiter().GetResult();push.GetAwaiter().GetResult();Assert.That(pop.GetAwaiter().GetResult(),Is.False);
                Assert.That(Keys(f.UI.GetService<IUINavigator>()),Is.EqualTo(new[]{"B","A"}));
            }
        }
    }
}
