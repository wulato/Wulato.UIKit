using System.Threading;
using Cysharp.Threading.Tasks;
namespace Wulato.UIKit.Samples.Inventory
{
    public partial class InventoryView : UIView<InventoryArgs>
    {
        public string Title { get; private set; }
        public InventoryTab DisplayedTab { get; private set; }
        partial void BindControls();
        partial void RefreshText();
        protected override void OnCreate(){BindControls();}
        protected override UniTask OnOpenAsync(InventoryArgs args,UISessionScope session,CancellationToken cancellationToken)
        {
            DisplayedTab=args.Tab;Title=args.PlayerId+" / "+args.Tab;
            RefreshText();return UniTask.CompletedTask;
        }
    }
}
