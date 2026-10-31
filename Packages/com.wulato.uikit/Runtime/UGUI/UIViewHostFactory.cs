using System;
using UnityEngine;

namespace Wulato.UIKit
{
    internal sealed class UIViewHostFactory : IUIViewHostFactory, IDisposable
    {
        readonly UIRoot root;
        readonly RectTransform stagingRoot;
        readonly RectTransform cacheRoot;
        bool disposed;
        internal UIViewHostFactory(UIRoot root)
        {
            this.root = root != null ? root : throw new ArgumentNullException(nameof(root));
            root.ValidateAndInitialize();
            stagingRoot = UIRoot.CreateRect("UIKit.Staging", root.transform);
            stagingRoot.gameObject.SetActive(false);
            cacheRoot = UIRoot.CreateRect("UIKit.Cache", root.transform);
            cacheRoot.gameObject.SetActive(false);
        }
        public IUIViewHost Create(UILayer layer)
        {
            if (disposed) throw new ObjectDisposedException(nameof(UIViewHostFactory));
            return new UIViewHost(root.GetLayer(layer), stagingRoot, cacheRoot);
        }
        // Context owner calls this after all leases/hosts have been released or transferred.
        public void Dispose()
        {
            if (disposed) return;
            disposed = true;
            DestroyRoot(stagingRoot); DestroyRoot(cacheRoot);
        }
        static void DestroyRoot(RectTransform rect)
        {
            if (rect == null) return;
            rect.gameObject.SetActive(false);
            if (Application.isPlaying) UnityEngine.Object.Destroy(rect.gameObject);
            else UnityEngine.Object.DestroyImmediate(rect.gameObject);
        }
    }
}
