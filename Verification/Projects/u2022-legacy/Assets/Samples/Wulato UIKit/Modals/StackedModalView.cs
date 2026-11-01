using System.Threading;
using Cysharp.Threading.Tasks;
namespace Wulato.UIKit.Samples.Modals
{
    public partial class StackedModalView : UIView
    {
        public string Title { get; private set; }
        partial void BindControls();
        partial void RefreshText();
        protected override void OnCreate(){BindControls();}
        protected override UniTask OnOpenAsync(UISessionScope session,CancellationToken cancellationToken)
        {
            Title="Stacked modal";
            RefreshText();return UniTask.CompletedTask;
        }
    }
}
