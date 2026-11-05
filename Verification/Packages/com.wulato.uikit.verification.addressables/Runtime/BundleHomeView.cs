using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
namespace Wulato.UIKit.Verification.Addressables
{
    public sealed class BundleHomeView : UIView<int>
    {
        public static readonly List<string> Events=new List<string>();
        public static BundleHomeView Last;
        public static int Argument;
        protected override void OnCreate() {Events.Add("create");}
        void Awake(){Events.Add("awake");}
        void OnEnable(){Events.Add("enable");}
        protected override UniTask OnOpenAsync(int args,UISessionScope scope,CancellationToken ct){Last=this;Argument=args;Events.Add("open");return UniTask.CompletedTask;}
    }
}
