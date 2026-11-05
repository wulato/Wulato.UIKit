using System.Threading;
using Cysharp.Threading.Tasks;
using Wulato.UIKit.Dialogs;
namespace Wulato.UIKit.Verification.Addressables
{
    public sealed class BundleQuestion : IDialogRequest<bool>{}
    public sealed class BundleDialogView : UIView<DialogArguments<BundleQuestion,bool>>
    {
        public static IDialogResponder<bool> Responder;
        protected override UniTask OnOpenAsync(DialogArguments<BundleQuestion,bool> args,UISessionScope scope,CancellationToken ct){Responder=args.Responder;return UniTask.CompletedTask;}
    }
}
