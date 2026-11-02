using System;
using System.IO;
using System.Reflection;
using UnityEditor;
using UnityEditor.AddressableAssets;
using UnityEditor.AddressableAssets.Settings.GroupSchemas;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using Wulato.UIKit.Editor;
using Object=UnityEngine.Object;
namespace Wulato.UIKit.Samples.Addressables.Editor
{
    public static class LocalInventorySetup
    {
        const string Folder="Assets/Samples/Wulato UIKit/LocalInventory";
        const string Prefab=Folder+"/AddressableInventory.prefab";
        public static void Author()
        {
            Directory.CreateDirectory(Folder);AssetDatabase.Refresh();
            if(!File.Exists(Prefab))
            {
                var go=new GameObject("AddressableInventory",typeof(RectTransform),typeof(AddressableInventoryView),typeof(UIBindingMarker));
                try
                {
                    var label=new GameObject("Count",typeof(RectTransform),typeof(Text));label.transform.SetParent(go.transform,false);
                    var text=label.GetComponent<Text>();text.font=Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");text.fontSize=32;text.color=Color.white;
                    var rect=label.GetComponent<RectTransform>();rect.sizeDelta=new Vector2(400,100);
                    var view=go.GetComponent<AddressableInventoryView>();var so=new SerializedObject(view);so.FindProperty("countLabel").objectReferenceValue=text;so.ApplyModifiedPropertiesWithoutUndo();
                    var marker=go.GetComponent<UIBindingMarker>();marker.viewScript=MonoScript.FromMonoBehaviour(view);marker.typeKey="sample-addressable-inventory";
                    marker.externalProviderId="addressables";marker.externalAssetId="uikit/sample-inventory";marker.closePolicy=UIClosePolicy.CacheOne;
                    PrefabUtility.SaveAsPrefabAsset(go,Prefab);
                }
                finally{Object.DestroyImmediate(go);}
            }
            var settings=AddressableAssetSettingsDefaultObject.GetSettings(true);
            var group=settings.FindGroup("UIKitLocalInventory")??settings.CreateGroup("UIKitLocalInventory",false,false,false,null,typeof(BundledAssetGroupSchema),typeof(ContentUpdateGroupSchema));
            var schema=group.GetSchema<BundledAssetGroupSchema>();schema.IncludeGUIDInCatalog=true;schema.BuildPath.SetVariableByName(settings,"Local.BuildPath");schema.LoadPath.SetVariableByName(settings,"Local.LoadPath");
            settings.CreateOrMoveEntry(AssetDatabase.AssetPathToGUID(Prefab),group).address="uikit/sample-inventory";AssetDatabase.SaveAssets();
            Debug.Log("UIKIT_ADDRESSABLES_SAMPLE_OK");
        }
        public static void FinalizeSample()
        {
            var path=Folder+"/LocalInventoryScene.unity";
            if(!File.Exists(path))
            {
                var scene=EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);
                var root=new GameObject("UIKit",typeof(RectTransform),typeof(UIRoot));root.GetComponent<Canvas>().renderMode=RenderMode.ScreenSpaceOverlay;
                var events=new GameObject("Input",typeof(EventSystem));
                var input=Type.GetType("Wulato.UIKit.InputSystem.InputSystemUIInputDriver, Wulato.UIKit.InputSystem",false);
                if(input!=null)events.AddComponent(input);else events.AddComponent<LegacyUIInputDriver>();
                var demo=root.AddComponent<LocalInventoryDemo>();var so=new SerializedObject(demo);so.FindProperty("root").objectReferenceValue=root.GetComponent<UIRoot>();
                so.FindProperty("registry").objectReferenceValue=AssetDatabase.LoadAssetAtPath<UIRegistry>(UICodegenPipeline.RegistryPath);so.ApplyModifiedPropertiesWithoutUndo();
                EditorSceneManager.SaveScene(scene,path);
            }
            Validate();
        }
        public static void Validate()
        {
            AddressablesUIValidator.ValidatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(Prefab));
            var registry=AssetDatabase.LoadAssetAtPath<UIRegistry>(UICodegenPipeline.RegistryPath);
            var entry=registry.CopyValidated().Get(new UIViewTypeKey("sample-addressable-inventory"));
            if(entry.Asset.Kind!=UIViewAssetKind.External || entry.Asset.ProviderId!="addressables" || entry.Asset.AssetId!="uikit/sample-inventory")throw new InvalidOperationException("Sample External registry mismatch");
            foreach(var dependency in AssetDatabase.GetDependencies(UICodegenPipeline.RegistryPath,true))
                if(dependency==Prefab)throw new InvalidOperationException("External provider must not directly reference its Addressables Prefab");
            Debug.Log("UIKIT_ADDRESSABLES_SAMPLE_OK");
        }
    }
}
