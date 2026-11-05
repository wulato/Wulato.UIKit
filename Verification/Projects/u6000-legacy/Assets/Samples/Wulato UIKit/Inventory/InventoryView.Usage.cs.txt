namespace Wulato.UIKit.Samples.Inventory
{
    public partial class InventoryView
    {
        partial void BindControls(){closeButton.onClick.AddListener(RequestClose);}
        partial void RefreshText(){titleLabel.text=Title;}
    }
}
