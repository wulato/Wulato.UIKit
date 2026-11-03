using System.Threading;
using Cysharp.Threading.Tasks;
using Wulato.UIKit.Dialogs;
namespace Wulato.UIKit.Samples.BusinessFlow
{
    public sealed class ConfirmRequest:IDialogRequest<bool>{}
    public sealed class ConfirmPage:UIView<DialogArguments<ConfirmRequest,bool>>
    {
        IDialogResponder<bool> responder;
        protected override void OnCreate()
        {
            FlowUI.Label(transform,"Confirm action?",130);FlowUI.Button(transform,"Yes",60,()=>responder?.TrySubmit(true));
            FlowUI.Button(transform,"No",-5,()=>responder?.TrySubmit(false));FlowUI.Button(transform,"Cancel",-70,()=>responder?.TryCancel());
        }
        protected override UniTask OnOpenAsync(DialogArguments<ConfirmRequest,bool> args,UISessionScope scope,CancellationToken ct){responder=args.Responder;return UniTask.CompletedTask;}
        protected override void OnClosed(UICloseReason reason){responder=null;}
    }
}
