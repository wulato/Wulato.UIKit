using UnityEditor;
namespace Wulato.UIKit.Editor
{
    [InitializeOnLoad]
    internal static class AddressablesUIEditorBootstrap
    {
        static AddressablesUIEditorBootstrap()=>AddressablesUIBootstrap.Register();
    }
}
