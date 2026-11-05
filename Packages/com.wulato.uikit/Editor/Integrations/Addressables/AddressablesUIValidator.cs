using System;
using System.Linq;
using UnityEditor;
using UnityEditor.AddressableAssets;
using UnityEditor.AddressableAssets.Settings;
using UnityEditor.AddressableAssets.Settings.GroupSchemas;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;
namespace Wulato.UIKit.Editor
{
    /// <summary>Read-only validation for explicitly registered Addressables UI Prefabs.</summary>
    public static class AddressablesUIValidator
    {
        /// <summary>Validate a marker against the host's existing local Addressables settings.</summary>
        public static void ValidatePrefab(GameObject prefab)
        {
            var path=AssetDatabase.GetAssetPath(prefab);
            void Fail(string message) => throw new InvalidOperationException(path+": "+message);
            if(!prefab)throw new ArgumentNullException(nameof(prefab));
            var marker=prefab.GetComponent<UIBindingMarker>();
            if(!marker || !marker.includeInRegistry || marker.externalProviderId!="addressables")return;
            if(string.IsNullOrWhiteSpace(marker.externalAssetId))Fail("Addressables identifier is required.");
            var views=prefab.GetComponents<UIViewBase>();
            if(!prefab.activeSelf || !(prefab.transform is RectTransform) || views.Length!=1 || !views[0].enabled)
                Fail("Addressable Prefab requires an active RectTransform root and one enabled UIView.");
            var settings=AddressableAssetSettingsDefaultObject.Settings;
            if(!settings)Fail("Configure Addressables settings explicitly before validation.");
            var guid=AssetDatabase.AssetPathToGUID(path);
            var entry=settings.FindAssetEntry(guid);
            if(entry==null || !entry.parentGroup)Fail("Prefab must be an explicit Addressables entry.");
            var schema=entry.parentGroup.GetSchema<BundledAssetGroupSchema>();
            if(!schema || !schema.IncludeInBuild)Fail("Prefab must be included in an AssetBundle group.");
            var id=marker.externalAssetId;
            if(string.Equals(id,guid,StringComparison.Ordinal))
            {
                if(!schema.IncludeGUIDInCatalog)Fail("Enable Include GUIDs in Catalog for a GUID registration.");
            }
            else
            {
                if(!schema.IncludeAddressInCatalog || !string.Equals(id,entry.address,StringComparison.Ordinal))
                    Fail("Identifier must equal this Prefab's address or GUID; labels and sub-assets are unsupported.");
                var count=settings.groups.Where(g=>g).SelectMany(g=>g.entries).Count(e=>string.Equals(e.address,id,StringComparison.Ordinal));
                if(count!=1)Fail("Address must uniquely identify one explicit entry.");
            }
        }
        /// <summary>Validate only; never creates settings, groups, content, or generated C#.</summary>
        public static void Validate()
        {
            foreach(var guid in AssetDatabase.FindAssets("t:Prefab",new[]{"Assets"}))
                ValidatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(AssetDatabase.GUIDToAssetPath(guid)));
        }
    }
    public sealed class AddressablesUIBuildValidator : IPreprocessBuildWithReport
    {
        public int callbackOrder=>1;
        public void OnPreprocessBuild(BuildReport report)
        {
            try {AddressablesUIValidator.Validate();}
            catch(Exception error){throw new BuildFailedException(error.Message);}
        }
    }
}
