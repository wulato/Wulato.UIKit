using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;

namespace Wulato.UIKit
{
    public enum UIViewAssetKind { Prefab=0, Resources=1, External=2 }
    /// <summary>Explicit source, never inferred from the View name.</summary>
    [Serializable]
    public sealed class UIViewAsset
    {
        [SerializeField] private UIViewAssetKind kind;
        [SerializeField] private GameObject prefab;
        [SerializeField] private string resourceId;
        [SerializeField] private string providerId;
        [SerializeField] private string assetId;
        internal UIViewAsset CopyValidated()
        {
            switch(kind)
            {
                case UIViewAssetKind.External:
                    return External(providerId,assetId);
                case UIViewAssetKind.Resources:
                    if(string.IsNullOrWhiteSpace(resourceId))throw new ArgumentException("Resource identifier is required.");
                    return Resources(resourceId);
                case UIViewAssetKind.Prefab:
                    if(prefab==null)throw new ArgumentException("Prefab reference is required.");
                    return Direct(prefab);
                default:throw new ArgumentException("Unknown asset kind.");
            }
        }
        public UIViewAssetKind Kind => kind;
        public GameObject Prefab => prefab;
        public string ResourceId => resourceId;
        /// <summary>Explicit external backend identity, compared ordinally without normalization.</summary>
        public string ProviderId => providerId;
        /// <summary>Opaque backend asset identifier; whitespace within a nonblank identifier is preserved.</summary>
        public string AssetId => assetId;
        private UIViewAsset() { }
        /// <summary>Describe an optional loader backend without depending on its package.</summary>
        public static UIViewAsset External(string providerId,string assetId)
        {
            if(string.IsNullOrWhiteSpace(providerId))throw new ArgumentException("A provider identifier is required.",nameof(providerId));
            if(string.IsNullOrWhiteSpace(assetId))throw new ArgumentException("An asset identifier is required.",nameof(assetId));
            return new UIViewAsset{kind=UIViewAssetKind.External,providerId=providerId,assetId=assetId};
        }
        public static UIViewAsset Resources(string id)
        {
            if(string.IsNullOrWhiteSpace(id))throw new ArgumentException("Resource identifier is required.",nameof(id));
            return new UIViewAsset{kind=UIViewAssetKind.Resources,resourceId=id};
        }
        public static UIViewAsset Direct(GameObject prefab)
        {
            if(prefab==null)throw new ArgumentNullException(nameof(prefab));
            return new UIViewAsset{kind=UIViewAssetKind.Prefab,prefab=prefab};
        }
    }
    /// <summary>Explicit, immutable type and preparation bridge, preserved by generic registration.</summary>
    public sealed class UIRegistration
    {
        public UIViewTypeKey Key { get; }
        public Type ViewType { get; }
        public Type ArgumentType { get; }
        private readonly UIViewAsset asset;
        public UIViewAsset Asset => asset.CopyValidated();
        public UILayer Layer { get; }
        public UIMultiplicity Multiplicity { get; }
        public UIClosePolicy ClosePolicy { get; }
        public UIBackAction BackAction { get; }
        public UIInputPolicy InputPolicy { get; }
        internal UIRegistration(UIViewTypeKey key,Type viewType,Type argumentType,UIViewAsset asset,UILayer layer,
            UIMultiplicity multiplicity,UIClosePolicy closePolicy,UIBackAction backAction,UIInputPolicy inputPolicy)
        { Key=key;ViewType=viewType;ArgumentType=argumentType;this.asset=asset.CopyValidated();Layer=layer;Multiplicity=multiplicity;ClosePolicy=closePolicy;BackAction=backAction;InputPolicy=inputPolicy; }
        public bool Accepts(UIArguments args) => ArgumentType==args.ArgumentType;
    }
    /// <summary>Generated providers populate explicit mappings; no assembly scanning or Type.GetType.</summary>
    public abstract class UIRegistryProvider : ScriptableObject
    {
        public abstract void Populate(UIRegistryBuilder builder);
    }
    [CreateAssetMenu(menuName="Wulato/UIKit/Registry")]
    public sealed class UIRegistry : ScriptableObject
    {
        [SerializeField] private List<UIRegistryProvider> providers=new List<UIRegistryProvider>();
        public UIRegistrySnapshot CopyValidated()
        {
            var builder=new UIRegistryBuilder();
            foreach(var provider in providers)
            {
                if(provider==null)throw new ArgumentException("Registry provider is missing.");
                provider.Populate(builder);
            }
            return builder.Build();
        }
    }
    public sealed class UIRegistryBuilder
    {
        private readonly List<UIRegistration> entries=new List<UIRegistration>();
        public void Add<TView>(UIViewTypeKey key,UIViewAsset asset,UILayer layer=UILayer.Page,UIMultiplicity multiplicity=UIMultiplicity.Single,
            UIClosePolicy closePolicy=UIClosePolicy.Release,UIBackAction backAction=UIBackAction.RequestClose,UIInputPolicy? inputPolicy=null) where TView : UIView
            => Add(key,typeof(TView),null,asset,layer,multiplicity,closePolicy,backAction,inputPolicy);
        public void Add<TView,TArgs>(UIViewTypeKey key,UIViewAsset asset,UILayer layer=UILayer.Page,UIMultiplicity multiplicity=UIMultiplicity.Single,
            UIClosePolicy closePolicy=UIClosePolicy.Release,UIBackAction backAction=UIBackAction.RequestClose,UIInputPolicy? inputPolicy=null) where TView : UIView<TArgs>
            => Add(key,typeof(TView),typeof(TArgs),asset,layer,multiplicity,closePolicy,backAction,inputPolicy);
        private void Add(UIViewTypeKey key,Type viewType,Type argumentType,UIViewAsset asset,UILayer layer,UIMultiplicity multiplicity,
            UIClosePolicy closePolicy,UIBackAction backAction,UIInputPolicy? inputPolicy)
        {
            if(string.IsNullOrWhiteSpace(key.Value) || viewType.IsAbstract || viewType.ContainsGenericParameters || asset==null)
                throw new ArgumentException("A concrete View, stable key and asset are required.");
            if(!Enum.IsDefined(typeof(UILayer),layer) || !Enum.IsDefined(typeof(UIMultiplicity),multiplicity) ||
                !Enum.IsDefined(typeof(UIClosePolicy),closePolicy) || !Enum.IsDefined(typeof(UIBackAction),backAction))throw new ArgumentException("Invalid registration policy.");
            if(multiplicity==UIMultiplicity.Multi && closePolicy==UIClosePolicy.CacheOne)throw new ArgumentException("Multi only supports Release.");
            foreach(var item in entries)if(item.Key==key || item.ViewType==viewType)throw new ArgumentException("Duplicate View type or key.");
            entries.Add(new UIRegistration(key,viewType,argumentType,asset,layer,multiplicity,closePolicy,backAction,
                inputPolicy ?? new UIInputPolicy(layer!=UILayer.Toast,layer==UILayer.Modal)));
        }
        public UIRegistrySnapshot Build() => new UIRegistrySnapshot(entries);
    }
    public sealed class UIRegistrySnapshot
    {
        public IReadOnlyList<UIRegistration> Entries { get; }
        private readonly Dictionary<UIViewTypeKey,UIRegistration> byKey;
        internal UIRegistrySnapshot(IEnumerable<UIRegistration> entries)
        {
            Entries=UIFrozen.Copy(entries);byKey=new Dictionary<UIViewTypeKey,UIRegistration>();
            foreach(var entry in Entries)byKey.Add(entry.Key,entry);
        }
        public UIRegistration Get(UIViewTypeKey key) => byKey.TryGetValue(key,out var entry) ? entry : throw new ArgumentException("Unregistered View key.",nameof(key));
    }
}
