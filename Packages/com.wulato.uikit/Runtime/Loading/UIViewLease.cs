using System;
using UnityEngine;

namespace Wulato.UIKit
{
    /// <summary>Owns the cloned Prefab, never the framework's Shell or shared asset.</summary>
    internal sealed class UIViewLease : IUIViewLease
    {
        GameObject instance;
        UIViewBase view;
        UnityEngine.Object asset;
        internal UIViewLease(GameObject instance, UIViewBase view, UnityEngine.Object asset)
        {
            this.instance = instance;
            this.view = view;
            this.asset = asset;
        }
        public UIViewBase View => view;
        public void Dispose()
        {
            var owned = instance;
            if (owned == null) return;
            instance = null; view = null; asset = null;
            DestroyInstance(owned);
        }
        internal static void DestroyInstance(GameObject owned)
        {
            if (owned == null) return;
            owned.SetActive(false);
            // Keep scheduled destruction independent of a framework wrapper's lifetime.
            owned.transform.SetParent(null, false);
            if (Application.isPlaying) UnityEngine.Object.Destroy(owned);
            else UnityEngine.Object.DestroyImmediate(owned);
        }
    }
}
