using System;
using System.IO;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
namespace Wulato.UIKit.Samples.BusinessFlow.Editor
{
    public static class BusinessFlowSetup
    {
        const string Folder="Assets/Samples/Wulato UIKit/BusinessFlow";
        [MenuItem("Wulato/UIKit/Prepare Business Flow Sample")]
        public static void Author()
        {
            var staging=new GameObject("flow authoring");staging.SetActive(false);
            try
            {
                var provider=AssetDatabase.LoadAssetAtPath<BusinessFlowRegistryProvider>(Folder+"/BusinessFlowProvider.asset");
                if(!provider){provider=ScriptableObject.CreateInstance<BusinessFlowRegistryProvider>();AssetDatabase.CreateAsset(provider,Folder+"/BusinessFlowProvider.asset");}
                provider.Home=Prefab<HomePage>(staging.transform);provider.Settings=Prefab<SettingsPage>(staging.transform);provider.Confirm=Prefab<ConfirmPage>(staging.transform);provider.Reward=Prefab<RewardPage>(staging.transform);EditorUtility.SetDirty(provider);
                var registry=AssetDatabase.LoadAssetAtPath<UIRegistry>(Folder+"/BusinessFlowRegistry.asset");
                if(!registry){registry=ScriptableObject.CreateInstance<UIRegistry>();AssetDatabase.CreateAsset(registry,Folder+"/BusinessFlowRegistry.asset");}
                var serialized=new SerializedObject(registry);var list=serialized.FindProperty("providers");list.arraySize=1;list.GetArrayElementAtIndex(0).objectReferenceValue=provider;serialized.ApplyModifiedPropertiesWithoutUndo();
                if(!File.Exists(Folder+"/BusinessFlowScene.unity"))
                {
                var scene=EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);
                var camera=new GameObject("Background",typeof(Camera)).GetComponent<Camera>();camera.clearFlags=CameraClearFlags.SolidColor;camera.backgroundColor=new Color(.06f,.09f,.12f);
                var root=new GameObject("UI Root",typeof(RectTransform),typeof(UIRoot)).GetComponent<UIRoot>();root.GetComponent<Canvas>().renderMode=RenderMode.ScreenSpaceOverlay;
                var events=new GameObject("Events",typeof(EventSystem));var driver=Type.GetType("Wulato.UIKit.InputSystem.InputSystemUIInputDriver, Wulato.UIKit.InputSystem",false);events.AddComponent(driver??typeof(LegacyUIInputDriver));
                var demo=new GameObject("Business Flow",typeof(BusinessFlowDemo)).GetComponent<BusinessFlowDemo>();var data=new SerializedObject(demo);data.FindProperty("root").objectReferenceValue=root;data.FindProperty("registry").objectReferenceValue=registry;data.ApplyModifiedPropertiesWithoutUndo();
                EditorSceneManager.SaveScene(scene,Folder+"/BusinessFlowScene.unity");
                }
                AssetDatabase.SaveAssets();SelectInput();Validate();
            }
            finally{UnityEngine.Object.DestroyImmediate(staging);}
        }
        static GameObject Prefab<T>(Transform parent)where T:UIViewBase
        {
            var existing=AssetDatabase.LoadAssetAtPath<GameObject>(Folder+"/"+typeof(T).Name+".prefab");if(existing)return existing;
            var go=new GameObject(typeof(T).Name,typeof(RectTransform));go.transform.SetParent(parent,false);((RectTransform)go.transform).sizeDelta=new Vector2(480,400);go.AddComponent<T>();
            var asset=PrefabUtility.SaveAsPrefabAsset(go,Folder+"/"+typeof(T).Name+".prefab");UnityEngine.Object.DestroyImmediate(go);return asset;
        }
        public static void SelectInput()
        {
            var path=Folder+"/BusinessFlowScene.unity";var scene=EditorSceneManager.OpenScene(path);bool changed=false;
            var desired=Type.GetType("Wulato.UIKit.InputSystem.InputSystemUIInputDriver, Wulato.UIKit.InputSystem",false)??typeof(LegacyUIInputDriver);
            GameObject events=null;foreach(var go in scene.GetRootGameObjects())if(go.GetComponent<EventSystem>()){events=go;break;}
            if(!events){events=new GameObject("Events",typeof(EventSystem));changed=true;}
            foreach(var module in events.GetComponents<BaseInputModule>())if(module.GetType()!=desired){UnityEngine.Object.DestroyImmediate(module);changed=true;}
            if(!events.GetComponent(desired)){events.AddComponent(desired);changed=true;}
            if(changed)EditorSceneManager.SaveScene(scene,path);
        }
        public static void Validate()
        {
            var registry=AssetDatabase.LoadAssetAtPath<UIRegistry>(Folder+"/BusinessFlowRegistry.asset");if(!registry||registry.CopyValidated().Entries.Count!=4)throw new InvalidOperationException("Missing BusinessFlow registry");
            if(!File.Exists(Folder+"/BusinessFlowScene.unity"))throw new InvalidOperationException("Missing BusinessFlow scene");
        }
    }
}
