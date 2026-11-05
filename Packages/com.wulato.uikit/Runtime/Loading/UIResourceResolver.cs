using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;
namespace Wulato.UIKit
{
    /// <summary>Per-context source routing. Only factories are global; loaders and leases are never shared.</summary>
    internal sealed class UIResourceResolver : IUIViewLoader
    {
        static readonly Dictionary<string,Func<IUIViewLoader>> providers=new Dictionary<string,Func<IUIViewLoader>>(StringComparer.Ordinal);
        readonly Dictionary<string,IUIViewLoader> external=new Dictionary<string,IUIViewLoader>(StringComparer.Ordinal);
        readonly PrefabUIViewLoader prefabs=new PrefabUIViewLoader();
        readonly ResourcesUIViewLoader resources=new ResourcesUIViewLoader();

        // First-party adapters only: no scanning or user-owned instances in this table.
        internal static void RegisterProvider(string id,Func<IUIViewLoader> factory)
        {
            if(string.IsNullOrWhiteSpace(id))throw new ArgumentException("A provider identifier is required.",nameof(id));
            providers[id]=factory??throw new ArgumentNullException(nameof(factory));
        }
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetProviders()=>providers.Clear();
        static InvalidOperationException Missing(string id)=>new InvalidOperationException("No UI resource backend registered for '"+id+"'. Install its supported Unity SDK or supply a custom UIContextBuilder.WithLoader.");
        internal static void RequireProvider(string id)
        {if(!providers.ContainsKey(id))throw Missing(id);}

        internal UIResourceResolver(UIRegistrySnapshot registry)
        {
            if(registry==null)throw new ArgumentNullException(nameof(registry));
            var snapshot=new Dictionary<string,Func<IUIViewLoader>>(providers,StringComparer.Ordinal);
            var required=new Dictionary<string,Func<IUIViewLoader>>(StringComparer.Ordinal);
            // Validate the whole registry before invoking any factory or allocating Context resources.
            foreach(var entry in registry.Entries)
            {
                var asset=entry.Asset;
                if(asset.Kind!=UIViewAssetKind.External)continue;
                if(!snapshot.TryGetValue(asset.ProviderId,out var factory))throw Missing(asset.ProviderId);
                required[asset.ProviderId]=factory;
            }
            foreach(var pair in required)
                external.Add(pair.Key,pair.Value()??throw new InvalidOperationException("UI resource factory returned null: "+pair.Key));
        }
        public UniTask<IUIViewLease> CreateAsync(UIViewAsset asset,Transform parent,CancellationToken token)
        {
            try
            {
                if(!PlayerLoopHelper.IsMainThread)throw new InvalidOperationException("UI loading requires the main thread.");
                token.ThrowIfCancellationRequested();
                if(asset==null)throw new ArgumentNullException(nameof(asset));
                asset.CopyValidated();
                switch(asset.Kind)
                {
                    case UIViewAssetKind.Prefab:return prefabs.CreateAsync(asset,parent,token);
                    case UIViewAssetKind.Resources:return resources.CreateAsync(asset,parent,token);
                    case UIViewAssetKind.External:
                        if(!external.TryGetValue(asset.ProviderId,out var loader))throw Missing(asset.ProviderId);
                        return loader.CreateAsync(asset,parent,token);
                    default:throw new ArgumentException("Unknown UI asset kind.",nameof(asset));
                }
            }
            catch(OperationCanceledException error){return UniTask.FromCanceled<IUIViewLease>(error.CancellationToken);}
            catch(Exception error){return UniTask.FromException<IUIViewLease>(error);}
        }
    }
}
