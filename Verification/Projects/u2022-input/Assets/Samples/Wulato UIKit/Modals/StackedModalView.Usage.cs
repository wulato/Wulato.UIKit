namespace Wulato.UIKit.Samples.Modals
{
    public partial class StackedModalView
    {
        partial void BindControls(){closeButton.onClick.AddListener(RequestClose);}
        partial void RefreshText(){titleLabel.text=Title;}
    }
}
