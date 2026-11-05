using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;

namespace Wulato.UIKit
{
    /// <summary>Default loader for explicit Resources identifiers and direct Prefab descriptors.</summary>
    public sealed class ResourcesUIViewLoader : IUIViewLoader
    {
        readonly PrefabUIViewLoader prefabs = new PrefabUIViewLoader();
        public async UniTask<IUIViewLease> CreateAsync(UIViewAsset asset, Transform inactiveParent, CancellationToken cancellationToken)
        {
            PrefabUIViewLoader.ValidateParent(inactiveParent, cancellationToken);
            if (asset == null) throw new ArgumentNullException(nameof(asset));
            if (asset.Kind == UIViewAssetKind.Prefab)
                return await prefabs.CreateAsync(asset, inactiveParent, cancellationToken);
            if (asset.Kind != UIViewAssetKind.Resources || string.IsNullOrWhiteSpace(asset.ResourceId))
                throw new ArgumentException("An explicit Resources identifier is required.", nameof(asset));
            var request = Resources.LoadAsync<GameObject>(asset.ResourceId);
            await request.ToUniTask(cancellationToken: cancellationToken);
            await UniTask.SwitchToMainThread(cancellationToken);
            cancellationToken.ThrowIfCancellationRequested();
            if (!(request.asset is GameObject prefab))
                throw new InvalidOperationException("UI Prefab was not found in Resources: " + asset.ResourceId);
            // ResourceRequest owns its in-flight load; no instance exists before this handoff.
            // Unity caches the shared Prefab; the lease retains it and releases only its clone.
            return PrefabUIViewLoader.InstantiateLease(prefab, inactiveParent, cancellationToken);
        }
    }
}
