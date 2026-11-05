using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using NUnit.Framework;
using Wulato.UIKit.Tests.Support;
namespace Wulato.UIKit.Dialogs.Tests
{
    public sealed class Confirm:IDialogRequest<bool>{}
    public class ConfirmView:UIView<DialogArguments<Confirm,bool>>
    {
        public static IDialogResponder<bool> Responder;
        public static bool Fail,SubmitEarly,FailClose,SubmitOnClose;
        public static UniTaskCompletionSource OpenGate,CloseGate;
        protected override UniTask OnOpenAsync(DialogArguments<Confirm,bool> args,UISessionScope scope,CancellationToken token)
        {Responder=args.Responder;if(SubmitEarly)args.Responder.TrySubmit(true);if(Fail)throw new InvalidOperationException("open");return OpenGate?.Task??UniTask.CompletedTask;}
        protected override UniTask OnCloseAsync(UICloseReason reason,CancellationToken ct){if(SubmitOnClose)Responder.TrySubmit(true);if(FailClose)throw new InvalidOperationException("close");return CloseGate?.Task??UniTask.CompletedTask;}
        public void Dismiss()=>RequestClose();
    }
    [Category("dialogs")]
    public class DialogLifecycleTests
    {
        UIFlowTestHarness Fixture()
        {
            ConfirmView.Responder=null;ConfirmView.Fail=false;ConfirmView.SubmitEarly=false;ConfirmView.FailClose=false;ConfirmView.SubmitOnClose=false;ConfirmView.OpenGate=null;ConfirmView.CloseGate=null;
            var registry=new UIRegistryBuilder();registry.AddDialog<ConfirmView,Confirm,bool>(new UIViewTypeKey("confirm"),UIViewAsset.Resources("confirm"),UILayer.Modal,closePolicy:UIClosePolicy.CacheOne);
            return new UIFlowTestHarness(registry.Build());
        }
        UIDialogs Dialogs(UIFlowTestHarness fixture)=>new UIDialogs(fixture.UI);
        [Test] public void ResultFromClosingHook_WaitsForExistingClosedWithoutNestedCommand()
        {
            using(var f=Fixture())
            {ConfirmView.SubmitOnClose=true;var show=Dialogs(f).ShowAsync(new Confirm());((ConfirmView)f.LastView).Dismiss();
             var result=show.GetAwaiter().GetResult();Assert.That(result.Status,Is.EqualTo(DialogStatus.Submitted));Assert.That(result.Completion.Errors,Is.Empty);}
        }
        [Test] public void CallerCancelDuringSubmittedClose_WaitsForBarrierThenCancels()
        {
            using(var f=Fixture())using(var caller=new CancellationTokenSource())
            {
                ConfirmView.CloseGate=new UniTaskCompletionSource();var show=Dialogs(f).ShowAsync(new Confirm(),caller.Token);
                Assert.That(ConfirmView.Responder.TrySubmit(true),Is.True);Assert.That(show.Status.IsCompleted(),Is.False);
                caller.Cancel();Assert.That(show.Status.IsCompleted(),Is.False);ConfirmView.CloseGate.TrySetResult();
                Assert.Throws<OperationCanceledException>(()=>show.GetAwaiter().GetResult());Assert.That(f.UI.GetDiagnostics().Sessions,Is.Empty);
            }
        }
        [Test] public void Shutdown_OverridesUnfinishedSubmittedIntent()
        {
            using(var f=Fixture())
            {
                ConfirmView.CloseGate=new UniTaskCompletionSource();var show=Dialogs(f).ShowAsync(new Confirm());ConfirmView.Responder.TrySubmit(true);
                var stop=f.UI.ShutdownAsync();f.Clock.Advance(3);stop.GetAwaiter().GetResult();
                Assert.That(show.GetAwaiter().GetResult().Status,Is.EqualTo(DialogStatus.ContextStopped));ConfirmView.CloseGate.TrySetResult();
            }
        }
        [Test] public void DuplicateRequestRegistration_IsRejected()
        {
            var registry=new UIRegistryBuilder();registry.AddDialog<ConfirmView,Confirm,bool>(new UIViewTypeKey("confirm"),UIViewAsset.Resources("confirm"));
            Assert.Throws<ArgumentException>(()=>registry.AddDialog<ConfirmView,Confirm,bool>(new UIViewTypeKey("confirm"),UIViewAsset.Resources("confirm")));
        }
        [Test] public void CanceledBeforeOpen_DoesNotLoad()
        {
            using(var f=Fixture())using(var caller=new CancellationTokenSource())
            {caller.Cancel();Assert.Throws<OperationCanceledException>(()=>Dialogs(f).ShowAsync(new Confirm(),caller.Token).GetAwaiter().GetResult());Assert.That(f.Creates,Is.Zero);}
        }
        [Test] public void CancelUncooperativeOpen_ObservesLateCleanup()
        {
            using(var f=Fixture())using(var caller=new CancellationTokenSource())
            {
                var gate=new UniTaskCompletionSource();ConfirmView.OpenGate=gate;var show=Dialogs(f).ShowAsync(new Confirm(),caller.Token);
                caller.Cancel();Assert.Throws<OperationCanceledException>(()=>show.GetAwaiter().GetResult());Assert.That(f.UI.GetDiagnostics().Sessions,Is.Empty);
                Assert.That(f.Releases,Is.Zero);gate.TrySetResult();Assert.That(f.Releases,Is.EqualTo(1));
            }
        }
        [Test] public void CloseFailure_RemainsInSubmittedCompletion()
        {
            using(var f=Fixture())
            {ConfirmView.FailClose=true;var show=Dialogs(f).ShowAsync(new Confirm());ConfirmView.Responder.TrySubmit(true);
             var result=show.GetAwaiter().GetResult();Assert.That(result.Status,Is.EqualTo(DialogStatus.Submitted));Assert.That(result.Completion.Errors.Count,Is.GreaterThan(0));}
        }
        [Test] public void SubmittedFalse_IsAValidResultAfterClosed()
        {
            using(var f=Fixture())
            {var show=Dialogs(f).ShowAsync(new Confirm());Assert.That(show.Status.IsCompleted(),Is.False);Assert.That(ConfirmView.Responder.TrySubmit(false),Is.True);
             var result=show.GetAwaiter().GetResult();Assert.That(result.Status,Is.EqualTo(DialogStatus.Submitted));Assert.That(result.Value,Is.False);Assert.That(result.Completion,Is.Not.Null);Assert.That(f.UI.GetDiagnostics().Sessions,Is.Empty);}
        }
        [Test] public void CallerCancelAfterOpen_ClosesBeforeCancellation()
        {
            using(var f=Fixture())using(var caller=new CancellationTokenSource())
            {var show=Dialogs(f).ShowAsync(new Confirm(),caller.Token);caller.Cancel();Assert.Throws<OperationCanceledException>(()=>show.GetAwaiter().GetResult());Assert.That(f.UI.GetDiagnostics().Sessions,Is.Empty);}
        }
        [Test] public void UserClose_ReturnsUserCanceledAndInvalidatesResponder()
        {
            using(var f=Fixture())
            {var show=Dialogs(f).ShowAsync(new Confirm());var responder=ConfirmView.Responder;((ConfirmView)f.LastView).Dismiss();
             var result=show.GetAwaiter().GetResult();Assert.That(result.Status,Is.EqualTo(DialogStatus.UserCanceled));Assert.That(responder.TrySubmit(true),Is.False);Assert.Throws<InvalidOperationException>(()=>{var ignored=result.Value;});}
        }
        [Test] public void SubmitEarly_ThenOpenFailureDoesNotPublishSuccess()
        {
            using(var f=Fixture())
            {ConfirmView.Fail=true;ConfirmView.SubmitEarly=true;Assert.Throws<UIException>(()=>Dialogs(f).ShowAsync(new Confirm()).GetAwaiter().GetResult());Assert.That(ConfirmView.Responder.TrySubmit(true),Is.False);Assert.That(f.Creates,Is.EqualTo(f.Releases));}
        }
        [Test] public void Shutdown_EndsWaiterAsContextStopped()
        {
            using(var f=Fixture())
            {var show=Dialogs(f).ShowAsync(new Confirm());f.UI.ShutdownAsync().GetAwaiter().GetResult();Assert.That(show.GetAwaiter().GetResult().Status,Is.EqualTo(DialogStatus.ContextStopped));}
        }
        [Test] public void CachedReopen_OldResponderCannotResolveNewWaiter()
        {
            using(var f=Fixture())
            {var dialogs=Dialogs(f);var first=dialogs.ShowAsync(new Confirm());var old=ConfirmView.Responder;old.TryCancel();first.GetAwaiter().GetResult();
             var second=dialogs.ShowAsync(new Confirm());Assert.That(old.TrySubmit(true),Is.False);Assert.That(second.Status.IsCompleted(),Is.False);ConfirmView.Responder.TrySubmit(true);Assert.That(second.GetAwaiter().GetResult().Value,Is.True);Assert.That(f.Creates,Is.EqualTo(1));}
        }
    }
}
