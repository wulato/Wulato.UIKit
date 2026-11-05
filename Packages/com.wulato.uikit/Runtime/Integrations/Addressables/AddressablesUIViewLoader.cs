using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.ResourceManagement.AsyncOperations;
using UnityEngine.ResourceManagement.ResourceLocations;
using AddressablesAPI = UnityEngine.AddressableAssets.Addressables;
using Object = UnityEngine.Object;
namespace Wulato.UIKit
{
    /// <summary>Explicit Addressables adapter. Each returned lease owns its clone and asset handle.</summary>
    public sealed class AddressablesUIViewLoader : IUIViewLoader
    {
        readonly IAddressableOperations operations;
        readonly ResourcesUIViewLoader defaults = new ResourcesUIViewLoader();
        /// <summary>Create the production loader. Registry routing installs this per Context; explicit overrides remain supported.</summary>
        public AddressablesUIViewLoader() : this(new UnityAddressableOperations()) { }
        internal AddressablesUIViewLoader(IAddressableOperations operations)
        { this.operations=operations??throw new ArgumentNullException(nameof(operations)); }

        /// <summary>Wait for the underlying operation once. Core owns prompt cancellation and late observation.</summary>
        public async UniTask<IUIViewLease> CreateAsync(UIViewAsset asset,Transform inactiveParent,CancellationToken cancellationToken)
        {
            ValidateParent(inactiveParent,cancellationToken);
            if(asset==null)throw new ArgumentNullException(nameof(asset));
            if(asset.Kind==UIViewAssetKind.Prefab || asset.Kind==UIViewAssetKind.Resources)
                return await defaults.CreateAsync(asset,inactiveParent,cancellationToken);
            if(asset.Kind!=UIViewAssetKind.External || !string.Equals(asset.ProviderId,"addressables",StringComparison.Ordinal) || string.IsNullOrWhiteSpace(asset.AssetId))
                throw new ArgumentException("An explicit addressables provider and asset identifier are required.",nameof(asset));
            var location=await LocateUnique(asset.AssetId,inactiveParent,cancellationToken);
            ValidateParent(inactiveParent,cancellationToken);
            var load=operations.Load(location);
            bool ownsLoad=true;
            GameObject instance=null;
            try
            {
                await Observe(load);
                ValidateParent(inactiveParent,cancellationToken);
                var prefab=load.Result;
                if(!prefab || !prefab.activeSelf || !(prefab.transform is RectTransform))
                    throw new InvalidOperationException("Addressable Prefab requires an active RectTransform root: "+asset.AssetId);
                var sources=prefab.GetComponents<UIViewBase>();
                if(sources.Length!=1 || !sources[0].enabled)
                    throw new InvalidOperationException("Addressable Prefab requires one enabled root UIView: "+asset.AssetId);
                instance=Object.Instantiate(prefab,inactiveParent,false);
                ValidateParent(inactiveParent,cancellationToken);
                var views=instance.GetComponents<UIViewBase>();
                if(views.Length!=1 || !views[0].enabled || instance.activeInHierarchy)
                    throw new InvalidOperationException("Addressable instance violated the inactive View contract.");
                var lease=new AddressableUIViewLease(instance,views[0],load,operations);
                instance=null;ownsLoad=false;return lease;
            }
            finally
            {
                if(ownsLoad)AddressableUIViewLease.Retire(instance,load,operations);
            }
        }
        async UniTask<IResourceLocation> LocateUnique(string id,Transform parent,CancellationToken token)
        {
            var query=operations.Locate(id);
            try
            {
                await Observe(query);ValidateParent(parent,token);
                if(query.Result==null || query.Result.Count!=1)
                    throw new InvalidOperationException("Addressables requires exactly one GameObject location: "+id);
                var location=query.Result[0];
                // A label that happens to select one asset is not an explicit address.
                if(!string.Equals(location.PrimaryKey,id,StringComparison.Ordinal) && !IsGuid(id))
                    throw new ArgumentException("Use an explicit address or GUID, not a label or sub-asset key.",nameof(id));
                return location;
            }
            finally {if(query.IsValid())operations.Release(query);}
        }
        static bool IsGuid(string id)
        {
            if(id.Length!=32)return false;
            foreach(char c in id)if(!((c>='0'&&c<='9')||(c>='a'&&c<='f')||(c>='A'&&c<='F')))return false;
            return true;
        }
        static async UniTask Observe<T>(AsyncOperationHandle<T> operation)
        {
            // Do not use cancelable ToUniTask: some integrations release the handle on cancellation.
            while(!operation.IsDone)await UniTask.NextFrame();
            await UniTask.SwitchToMainThread();
            if(operation.Status!=AsyncOperationStatus.Succeeded)
                throw operation.OperationException??new InvalidOperationException("Addressables operation failed.");
        }
        static void ValidateParent(Transform parent,CancellationToken cancellationToken)
        {
            if(!PlayerLoopHelper.IsMainThread)throw new InvalidOperationException("UI loading requires the main thread.");
            cancellationToken.ThrowIfCancellationRequested();
            if(!parent || parent.gameObject.activeInHierarchy)throw new ArgumentException("A live inactive parent is required.",nameof(parent));
        }
        sealed class UnityAddressableOperations : IAddressableOperations
        {
            public AsyncOperationHandle<IList<IResourceLocation>> Locate(string key) => AddressablesAPI.LoadResourceLocationsAsync(key,typeof(GameObject));
            public AsyncOperationHandle<GameObject> Load(IResourceLocation location) => AddressablesAPI.LoadAssetAsync<GameObject>(location);
            public void Release<T>(AsyncOperationHandle<T> operation) => AddressablesAPI.Release(operation);
        }
    }
}
