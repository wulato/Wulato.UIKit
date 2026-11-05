using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.UI;
namespace Wulato.UIKit.Acceptance
{
    public readonly struct MobileSmokeArgs {public readonly int Value;public MobileSmokeArgs(int value){Value=value;}}
    public partial class MobileSmokeView : UIView<MobileSmokeArgs>
    {
        public int Value {get;private set;}
        protected override void OnCreate(){GetComponentInChildren<Button>().onClick.AddListener(RequestClose);}
        protected override UniTask OnOpenAsync(MobileSmokeArgs args,UISessionScope scope,CancellationToken ct)
        {Value=args.Value;GetComponentInChildren<Text>().text="T11: tap Close to complete touch smoke";return UniTask.CompletedTask;}
    }
}
