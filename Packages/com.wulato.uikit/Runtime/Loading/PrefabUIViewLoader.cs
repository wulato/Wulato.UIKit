using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;

namespace Wulato.UIKit
{
    public sealed class PrefabUIViewLoader : IUIViewLoader
    {
        public UniTask<IUIViewLease> CreateAsync(UIViewAsset asset, Transform inactiveParent, CancellationToken cancellationToken)
        {
            try
            {
                ValidateParent(inactiveParent, cancellationToken);
                if (asset == null || asset.Kind != UIViewAssetKind.Prefab)
                    throw new ArgumentException("A direct Prefab asset is required.", nameof(asset));
                return UniTask.FromResult(InstantiateLease(asset.Prefab, inactiveParent, cancellationToken));
            }
            catch (OperationCanceledException e) { return UniTask.FromCanceled<IUIViewLease>(e.CancellationToken); }
            catch (Exception e) { return UniTask.FromException<IUIViewLease>(e); }
        }
        internal static void ValidateParent(Transform parent, CancellationToken token)
        {
            if (!PlayerLoopHelper.IsMainThread) throw new InvalidOperationException("UI loading requires the main thread.");
            token.ThrowIfCancellationRequested();
            if (parent == null || parent.gameObject.activeInHierarchy)
                throw new ArgumentException("The loader requires a non-active parent.", nameof(parent));
        }
        internal static IUIViewLease InstantiateLease(GameObject prefab, Transform parent, CancellationToken token)
        {
            ValidateParent(parent, token);
            if (prefab == null || !prefab.activeSelf)
                throw new ArgumentException("The Prefab root must exist and be activeSelf.", nameof(prefab));
            GameObject instance = null;
            try
            {
                instance = UnityEngine.Object.Instantiate(prefab, parent, false);
                var views = instance.GetComponents<UIViewBase>();
                if (!(instance.transform is RectTransform) || views.Length != 1 || !views[0].enabled)
                    throw new InvalidOperationException("The Prefab root requires one enabled UIView and a RectTransform.");
                token.ThrowIfCancellationRequested();
                return new UIViewLease(instance, views[0], prefab);
            }
            catch
            {
                UIViewLease.DestroyInstance(instance);
                throw;
            }
        }
    }
}
