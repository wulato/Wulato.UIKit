namespace Wulato.UIKit.Samples.QuickStart
{
    public partial class SettingsView
    {
        partial void BindControls(){closeButton.onClick.AddListener(RequestClose);}
        partial void RefreshText(){titleLabel.text=Title;}
    }
}
