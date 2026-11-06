using Cysharp.Threading.Tasks;
using UnityEngine;
namespace Wulato.UIKit.Samples.DOTween
{
    /// <summary>Assign Host presets and a Registry non-Page key; wire these methods to authored uGUI buttons.</summary>
    public sealed class DOTweenExample:MonoBehaviour
    {
        public UIContextHost Host;
        public string ModalKey="notice";
        public void Open()=>OpenAsync().Forget(Debug.LogException);
        async UniTask OpenAsync()
        {
            var ui=Host.UI??Host.Initialize();
            await ui.OpenAsync(new UIViewTypeKey(ModalKey),UIArguments.None);
        }
        public void Back(){if(Host&&Host.UI!=null)Host.UI.BackAsync().Forget(Debug.LogException);}
    }
}
