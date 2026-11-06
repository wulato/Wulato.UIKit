using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using NUnit.Framework;
using Wulato.UIKit.Tests.Support;
namespace Wulato.UIKit.Navigation.Tests
{
    public sealed class BackHome : UIView { }
    public sealed class BackPage : UIView
    {
        internal static bool Allow=true,Throw;internal static int Checks;
        protected override bool CanClose(UICloseReason reason){Checks++;Assert.That(reason,Is.EqualTo(UICloseReason.Back));if(Throw)throw new InvalidOperationException("can-close");return Allow;}
    }
    public sealed class BackNext : UIView { }
    public sealed class BackModal : UIView
    {
        internal static bool Allow=true;internal static int Checks;internal static UniTaskCompletionSource CloseGate;
        protected override bool CanClose(UICloseReason reason){Checks++;Assert.That(reason,Is.EqualTo(UICloseReason.Back));return Allow;}
        protected override UniTask OnCloseAsync(UICloseReason reason,CancellationToken ct)=>CloseGate?.Task??UniTask.CompletedTask;
    }
    [Category("navigation")]
    public sealed class BackNavigationTests
    {
        sealed class Pair:INavigationTransition
        {
            internal bool Block;internal readonly UniTaskCompletionSource Gate=new UniTaskCompletionSource();
            public UITransitionWrites FromWrites=>UITransitionWrites.None;public UITransitionWrites ToWrites=>UITransitionWrites.None;
            public UniTask PlayAsync(UINavigationTransitionContext context,CancellationToken ct)=>Block?Gate.Task:UniTask.CompletedTask;
            public void Stop(UINavigationTransitionContext context)=>Gate.TrySetCanceled();public void Reset(UINavigationTransitionContext context){}
        }
        static UIFlowTestHarness Fixture(Pair pair=null,UIBackAction modalAction=UIBackAction.RequestClose)
        {
            BackPage.Allow=true;BackPage.Throw=false;BackPage.Checks=0;BackModal.Allow=true;BackModal.Checks=0;BackModal.CloseGate=null;
            var b=new UIRegistryBuilder();b.Add<BackHome>(new UIViewTypeKey("home"),UIViewAsset.Resources("home"));
            b.Add<BackPage>(new UIViewTypeKey("page"),UIViewAsset.Resources("page"));b.Add<BackNext>(new UIViewTypeKey("next"),UIViewAsset.Resources("next"));
            b.Add<BackModal>(new UIViewTypeKey("modal"),UIViewAsset.Resources("modal"),UILayer.Modal,backAction:modalAction);
            return new UIFlowTestHarness(b.Build(),pair);
        }
        static IUISession Page(UIFlowTestHarness f){f.UI.OpenAsync<BackHome>().GetAwaiter().GetResult();return f.UI.PushAsync<BackPage>().GetAwaiter().GetResult();}
        [Test] public void Back_ClosesOnlyTheModalThenRespectsPageVetoAndRootNoOp()
        {
            using(var f=Fixture())
            {
                var page=Page(f);var modal=f.UI.OpenAsync<BackModal>().GetAwaiter().GetResult();var nav=f.UI.GetService<IUINavigator>();
                BackModal.Allow=false;Assert.That(f.UI.BackAsync().GetAwaiter().GetResult(),Is.False);Assert.That(nav.History.Count,Is.EqualTo(2));Assert.That(BackPage.Checks,Is.Zero);
                BackModal.Allow=true;Assert.That(f.UI.BackAsync().GetAwaiter().GetResult(),Is.True);Assert.That(modal.State,Is.EqualTo(UIState.Closed));Assert.That(page.State,Is.EqualTo(UIState.Open));Assert.That(BackPage.Checks,Is.Zero);
                Assert.That(modal.Closed.GetAwaiter().GetResult().Reason,Is.EqualTo(UICloseReason.Back));
                BackPage.Allow=false;Assert.That(nav.BackAsync().GetAwaiter().GetResult(),Is.False);Assert.That(nav.History.Count,Is.EqualTo(2));
                BackPage.Allow=true;Assert.That(f.UI.BackAsync().GetAwaiter().GetResult(),Is.True);Assert.That(nav.History.Count,Is.EqualTo(1));Assert.That(f.UI.BackAsync().GetAwaiter().GetResult(),Is.False);
            }
        }
        [Test] public void BackActionNone_BlocksFallthroughAndOwnerCloseStillWorks()
        {
            using(var f=Fixture(modalAction:UIBackAction.None))
            {
                var page=Page(f);var modal=f.UI.OpenAsync<BackModal>().GetAwaiter().GetResult();Assert.That(f.UI.BackAsync().GetAwaiter().GetResult(),Is.False);
                Assert.That(BackModal.Checks+BackPage.Checks,Is.Zero);Assert.That(page.State,Is.EqualTo(UIState.Open));Assert.That(modal.State,Is.EqualTo(UIState.Open));
                f.UI.CloseAsync(modal).GetAwaiter().GetResult();Assert.That(modal.State,Is.EqualTo(UIState.Closed));
            }
        }
        [Test] public void InputBack_RestoresHistoryAndRootIsANoOp()
        {
            using(var f=Fixture())
            {
                var page=Page(f);f.Input.Registration.UserCloseRequested(page.Id,UICloseReason.Back);
                var nav=f.UI.GetService<IUINavigator>();Assert.That(nav.History.Count,Is.EqualTo(1));Assert.That(f.UI.GetDiagnostics().Sessions[0].Type.Value,Is.EqualTo("home"));
                f.Input.Registration.UserCloseRequested(f.UI.GetDiagnostics().Sessions[0].Id,UICloseReason.Back);Assert.That(nav.History.Count,Is.EqualTo(1));Assert.That(f.UI.GetDiagnostics().Sessions[0].State,Is.EqualTo(UIState.Open));
            }
        }
        [Test] public void ModalWithoutPages_AcceptedBackIgnoresLateCallerCancellation()
        {
            using(var f=Fixture())using(var ct=new CancellationTokenSource())
            {
                var modal=f.UI.OpenAsync<BackModal>().GetAwaiter().GetResult();BackModal.CloseGate=new UniTaskCompletionSource();
                var back=f.UI.BackAsync(ct.Token);ct.Cancel();Assert.That(back.Status.IsCompleted(),Is.False);
                BackModal.CloseGate.TrySetResult();Assert.That(back.GetAwaiter().GetResult(),Is.True);Assert.That(modal.State,Is.EqualTo(UIState.Closed));
                Assert.That(f.UI.GetService<IUINavigator>().History,Is.Empty);
            }
        }
        [Test] public void InputBack_IsDebouncedAndCannotTargetAReplacementSession()
        {
            var pair=new Pair();using(var f=Fixture(pair))
            {
                var old=Page(f);pair.Block=true;var push=f.UI.PushAsync<BackNext>();
                f.Input.Registration.UserCloseRequested(old.Id,UICloseReason.Back);f.Input.Registration.UserCloseRequested(old.Id,UICloseReason.Back);
                pair.Block=false;pair.Gate.TrySetResult();push.GetAwaiter().GetResult();
                Assert.That(f.UI.GetService<IUINavigator>().History.Count,Is.EqualTo(3));Assert.That(BackPage.Checks,Is.Zero);Assert.That(f.UI.GetDiagnostics().Sessions.Count,Is.EqualTo(1));
                f.Input.Registration.UserCloseRequested(old.Id,UICloseReason.Back);Assert.That(f.UI.GetService<IUINavigator>().History.Count,Is.EqualTo(3));
            }
        }
        [Test] public void ModalBack_HoldsFifoUntilClosedAndDoesNotAlsoPopThePage()
        {
            using(var f=Fixture())
            {
                Page(f);var modal=f.UI.OpenAsync<BackModal>().GetAwaiter().GetResult();BackModal.CloseGate=new UniTaskCompletionSource();
                var back=f.UI.BackAsync();var push=f.UI.PushAsync<BackNext>();Assert.That(back.Status.IsCompleted(),Is.False);Assert.That(push.Status.IsCompleted(),Is.False);
                BackModal.CloseGate.TrySetResult();Assert.That(back.GetAwaiter().GetResult(),Is.True);push.GetAwaiter().GetResult();
                Assert.That(modal.State,Is.EqualTo(UIState.Closed));Assert.That(f.UI.GetService<IUINavigator>().History.Count,Is.EqualTo(3));Assert.That(BackPage.Checks,Is.Zero);
            }
        }
        [Test] public void Back_HookFailureAndPreCanceledRequestDoNotChangeHistory()
        {
            using(var f=Fixture())using(var ct=new CancellationTokenSource())
            {
                var page=Page(f);ct.Cancel();Assert.Throws<OperationCanceledException>(()=>f.UI.BackAsync(ct.Token).GetAwaiter().GetResult());Assert.That(BackPage.Checks,Is.Zero);
                BackPage.Throw=true;Assert.That(f.UI.BackAsync().GetAwaiter().GetResult(),Is.False);Assert.That(f.Errors.Count,Is.EqualTo(1));
                Assert.That(f.UI.GetService<IUINavigator>().History.Count,Is.EqualTo(2));Assert.That(page.State,Is.EqualTo(UIState.Open));
            }
        }
        [Test] public void Back_IsContextLocalAndShutdownCanTakeOverABlockedBack()
        {
            var pair=new Pair();using(var a=Fixture(pair))using(var b=Fixture())
            {
                Page(a);var other=Page(b);var nav=a.UI.GetService<IUINavigator>();pair.Block=true;var back=a.UI.BackAsync();var stop=a.UI.ShutdownAsync();
                Assert.Throws<OperationCanceledException>(()=>back.GetAwaiter().GetResult());stop.GetAwaiter().GetResult();Assert.That(nav.History,Is.Empty);
                Assert.That(other.State,Is.EqualTo(UIState.Open));Assert.That(b.UI.GetService<IUINavigator>().History.Count,Is.EqualTo(2));
            }
        }
    }
}
