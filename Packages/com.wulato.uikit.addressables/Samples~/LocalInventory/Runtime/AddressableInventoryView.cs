using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.UI;
namespace Wulato.UIKit.Samples.Addressables
{
    public sealed partial class AddressableInventoryView : UIView<int>
    {
        [SerializeField] Text countLabel;
        protected override UniTask OnOpenAsync(int itemCount,UISessionScope scope,CancellationToken ct)
        {countLabel.text="Inventory: "+itemCount;return UniTask.CompletedTask;}
    }
}
