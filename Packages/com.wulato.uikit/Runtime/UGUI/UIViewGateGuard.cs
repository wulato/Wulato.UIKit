using System;
using UnityEngine;

namespace Wulato.UIKit
{
    // Preparation hooks and Start may create dynamic descendants while the command awaits.
    // Revalidate before normal UI Update and before Canvas rendering; violations fail closed.
    [DefaultExecutionOrder(-32000)]
    internal sealed class UIViewGateGuard : MonoBehaviour
    {
        Action monitor;
        Action validate;
        internal void Initialize(Action monitor, Action validate)
        {
            this.monitor = monitor;
            this.validate = validate;
        }
        internal void ValidateNow() => validate?.Invoke();
        void OnEnable() => Canvas.preWillRenderCanvases += Check;
        void OnDisable() => Canvas.preWillRenderCanvases -= Check;
        void Update() => Check();
        void Check() => monitor?.Invoke();
        void OnDestroy() { monitor = null; validate = null; }
    }
}
