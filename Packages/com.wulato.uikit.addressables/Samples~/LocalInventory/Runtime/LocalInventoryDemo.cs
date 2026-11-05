using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;
namespace Wulato.UIKit.Samples.Addressables
{
    public sealed class LocalInventoryDemo : MonoBehaviour
    {
        [SerializeField] UIRoot root;
        [SerializeField] UIRegistry registry;
        IUIContext ui;
        async void Start()
        {
            try
            {
                ui=new UIContextBuilder(root,registry).WithLoader(new AddressablesUIViewLoader()).Build();
                var first=await ui.OpenAsync<AddressableInventoryView,int>(12,destroyCancellationToken);
                await first.CloseAsync(); // CacheOne retains the instance and Addressables handle.
                var reopened=await ui.OpenAsync<AddressableInventoryView,int>(24,destroyCancellationToken);
                await reopened.CloseAsync();await ui.ClearCacheAsync();
                // A pre-canceled request never acquires a resource. In-flight cancellation is also supported.
                using(var cancellation=new CancellationTokenSource())
                {
                    cancellation.Cancel();
                    try{await ui.OpenAsync<AddressableInventoryView,int>(99,cancellation.Token);}catch(System.OperationCanceledException){}
                }
                await ui.OpenAsync<AddressableInventoryView,int>(24,destroyCancellationToken);
            }
            catch(System.OperationCanceledException){}
            catch(System.Exception error){Debug.LogException(error);}
        }
        async void OnDestroy(){if(ui!=null)await ui.ShutdownAsync();}
    }
}
