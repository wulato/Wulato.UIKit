using UnityEngine;
namespace Wulato.UIKit
{
    internal static class AddressablesUIBootstrap
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterAssembliesLoaded)]
        internal static void Register()=>UIResourceResolver.RegisterProvider("addressables",Create);
        static IUIViewLoader Create()=>new AddressablesUIViewLoader();
    }
}
