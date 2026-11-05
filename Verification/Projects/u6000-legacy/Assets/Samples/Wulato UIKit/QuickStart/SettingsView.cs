using System.Threading;
using Cysharp.Threading.Tasks;
namespace Wulato.UIKit.Samples.QuickStart
{
    public partial class SettingsView : UIView
    {
        public string Title { get; private set; }
        partial void BindControls();
        partial void RefreshText();
        protected override void OnCreate(){BindControls();}
        protected override UniTask OnOpenAsync(UISessionScope session,CancellationToken cancellationToken)
        {
            Title="Settings";
            RefreshText();return UniTask.CompletedTask;
        }
    }
}
