using System;
using System.Collections;
using System.IO;
using System.Linq;
using Cysharp.Threading.Tasks;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.AddressableAssets;
using UnityEditor.AddressableAssets.Build;
using UnityEditor.AddressableAssets.Settings;
using UnityEditor.AddressableAssets.Settings.GroupSchemas;
using UnityEditor.AddressableAssets.Build.DataBuilders;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;
using AddressablesAPI = UnityEngine.AddressableAssets.Addressables;
namespace Wulato.UIKit.Verification.Addressables.Editor
{
    public static class AddressablesContent
    {
        public const string Folder = "Assets/UIKitAddressables";
        public const string PrefabPath = Folder + "/Probe.prefab";
        public const string Address = "uikit/probe";
        public static void Prepare()
        {
            Wulato.UIKit.Acceptance.Editor.T2HostProbe.Prepare();
            Directory.CreateDirectory(Folder);
            if (!File.Exists(PrefabPath))
            {
                var go = new GameObject("AddressableProbe", typeof(RectTransform), typeof(AddressableProbeView));
                PrefabUtility.SaveAsPrefabAsset(go, PrefabPath); Object.DestroyImmediate(go);
            }
            AssetDatabase.Refresh();
            var settings = AddressableAssetSettingsDefaultObject.GetSettings(true);
            var group = settings.FindGroup("UIKitVerification") ?? settings.CreateGroup("UIKitVerification", false, false, false, null, typeof(BundledAssetGroupSchema), typeof(ContentUpdateGroupSchema));
            var schema = group.GetSchema<BundledAssetGroupSchema>();
            schema.IncludeGUIDInCatalog = true;
            schema.BuildPath.SetVariableByName(settings, "Local.BuildPath");
            schema.LoadPath.SetVariableByName(settings, "Local.LoadPath");
            var entry = settings.CreateOrMoveEntry(AssetDatabase.AssetPathToGUID(PrefabPath), group);
            entry.address = Address;
            Add<BundleHomeView>(settings,group,"Home","uikit/home");
            Add<BundlePageView>(settings,group,"Page","uikit/page");
            Add<BundleDialogView>(settings,group,"Dialog","uikit/dialog");
            Add<BundleRewardView>(settings,group,"Reward","uikit/reward");
            settings.ActivePlayerDataBuilderIndex = settings.DataBuilders.FindIndex(x => x is BuildScriptPackedMode);
            settings.ActivePlayModeDataBuilderIndex = settings.DataBuilders.FindIndex(x => x is BuildScriptPackedPlayMode);
            if (settings.ActivePlayerDataBuilderIndex < 0 || settings.ActivePlayModeDataBuilderIndex < 0) throw new InvalidOperationException("Packed builders missing");
            Directory.CreateDirectory(Folder+"/Resources");
            File.WriteAllText(Folder+"/Resources/UIKitAddressablesGuid.txt",entry.guid);
            if(!File.Exists(Folder+"/Resources/UIKitAddressablesFallback.prefab"))
                AssetDatabase.CopyAsset(PrefabPath,Folder+"/Resources/UIKitAddressablesFallback.prefab");
            if(!File.Exists(Folder+"/Resources/UIKitAddressablesPage.prefab"))
                AssetDatabase.CopyAsset(Folder+"/Page.prefab",Folder+"/Resources/UIKitAddressablesPage.prefab");
            AssetDatabase.Refresh();
            AssetDatabase.SaveAssets();
            AddressableAssetSettings.BuildPlayerContent(out AddressablesPlayerBuildResult result);
            if (!string.IsNullOrEmpty(result.Error)) throw new InvalidOperationException(result.Error);
            var output = Environment.GetEnvironmentVariable("UIKIT_EVIDENCE_DIR");
            File.WriteAllText(Path.Combine(output,"addressables-content.json"), JsonUtility.ToJson(new ContentReport {
                editor=Application.unityVersion, version=UnityEditor.PackageManager.PackageInfo.FindForAssembly(typeof(AddressablesAPI).Assembly).version,
                sbpVersion=UnityEditor.PackageManager.PackageInfo.FindForAssembly(typeof(UnityEditor.Build.Pipeline.ContentPipeline).Assembly).version, builderType=settings.ActivePlayModeDataBuilder.GetType().FullName,
                target=EditorUserBuildSettings.activeBuildTarget.ToString(), playMode=settings.ActivePlayModeDataBuilder.Name,
                guid=entry.guid, output=result.OutputPath }, true));
        }
        static void Add<T>(AddressableAssetSettings settings,AddressableAssetGroup group,string name,string address) where T:UIViewBase
        {
            var path=Folder+"/"+name+".prefab";
            if(!File.Exists(path))
            {
                var go=new GameObject(name,typeof(RectTransform));
                try {go.AddComponent<T>();PrefabUtility.SaveAsPrefabAsset(go,path);}
                finally {Object.DestroyImmediate(go);}
            }
            settings.CreateOrMoveEntry(AssetDatabase.AssetPathToGUID(path),group).address=address;
        }
        [Serializable] sealed class ContentReport { public string editor,version,sbpVersion,builderType,target,playMode,guid,output; }
    }
}
