using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Wulato.UIKit
{
    internal sealed class UIViewHostFactory : IUIViewHostFactory, IDisposable, IUIRuntimeHostProvider
    {
        readonly UIRoot root;
        readonly RectTransform stagingRoot;
        readonly RectTransform cacheRoot;
        readonly HashSet<UIViewHost> hosts = new HashSet<UIViewHost>();
        UIRuntimeHost runtime;
        IDisposable runtimeLease;
        bool disposed;
        public event Action RootDestroyed;
        public event Action<bool> AvailabilityChanged;
        bool IUIRuntimeHostProvider.RootAvailable => root != null && root.gameObject.activeInHierarchy;
        IUIRuntimeHost IUIRuntimeHostProvider.RuntimeHost
        {
            get
            {
                if (runtime == null)
                {
                    if (disposed) throw new ObjectDisposedException(nameof(UIViewHostFactory));
                    runtime = UIRuntimeHost.Create(); runtimeLease = runtime.Retain();
                    stagingRoot.SetParent(runtime.IsolationRoot, false); cacheRoot.SetParent(runtime.IsolationRoot, false);
                }
                return runtime;
            }
        }
        internal UIViewHostFactory(UIRoot root)
        {
            this.root = root != null ? root : throw new ArgumentNullException(nameof(root));
            root.ValidateAndInitialize();
            stagingRoot = UIRoot.CreateRect("UIKit.Staging", root.transform);
            stagingRoot.gameObject.SetActive(false);
            cacheRoot = UIRoot.CreateRect("UIKit.Cache", root.transform);
            cacheRoot.gameObject.SetActive(false);
            root.Disabled += OnDisabled; root.Enabled += OnEnabled; root.Destroyed += OnDestroyed;
        }
        void OnDisabled()
        {
            AvailabilityChanged?.Invoke(false);
            if (runtime != null) foreach (var host in hosts.ToArray()) host.Evacuate(root.transform, runtime.IsolationRoot);
        }
        void OnEnabled()
        {
            foreach (var host in hosts.ToArray()) host.RestoreEvacuated();
            AvailabilityChanged?.Invoke(true);
        }
        void OnDestroyed() { RootDestroyed?.Invoke(); Dispose(); }
        public IUIViewHost Create(UILayer layer)
        {
            if (disposed) throw new ObjectDisposedException(nameof(UIViewHostFactory));
            var host = new UIViewHost(root.GetLayer(layer), stagingRoot, cacheRoot, h => hosts.Remove(h));
            hosts.Add(host); return host;
        }
        // Context owner calls this after all leases/hosts have been released or transferred.
        public void Dispose()
        {
            if (disposed) return;
            disposed = true;
            if (root != null) { root.Disabled -= OnDisabled; root.Enabled -= OnEnabled; root.Destroyed -= OnDestroyed; }
            RootDestroyed = null; AvailabilityChanged = null;
            if (runtime != null) runtimeLease.Dispose();
            else { DestroyRoot(stagingRoot); DestroyRoot(cacheRoot); }
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
