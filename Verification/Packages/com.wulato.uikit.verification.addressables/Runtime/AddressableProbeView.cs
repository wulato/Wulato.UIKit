using System.Collections.Generic;
namespace Wulato.UIKit.Verification.Addressables
{
    public sealed class AddressableProbeView : UIView
    {
        public static readonly List<string> Events = new List<string>();
        void Awake() => Events.Add("awake");
        void OnEnable() => Events.Add("enable");
        void OnDestroy() => Events.Add("destroy");
    }
}
