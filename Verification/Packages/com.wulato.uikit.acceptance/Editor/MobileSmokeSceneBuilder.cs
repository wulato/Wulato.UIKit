#if UIKIT_MOBILE_SMOKE
using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
namespace Wulato.UIKit.Acceptance.Editor
{
    public static class MobileSmokeSceneBuilder
    {
        const string Prefab="Assets/UIKitMobileView.prefab";
        public static void Author()
        {
            if(File.Exists(Prefab))return;
            var go=new GameObject("MobileSmokeView",typeof(RectTransform),typeof(Image));
            try
            {
                var rect=(RectTransform)go.transform;rect.anchorMin=new Vector2(.1f,.2f);rect.anchorMax=new Vector2(.9f,.8f);rect.offsetMin=rect.offsetMax=Vector2.zero;
                var text=new GameObject("Instruction",typeof(RectTransform),typeof(Text));text.transform.SetParent(go.transform,false);
                var label=text.GetComponent<Text>();label.font=Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");label.text="T11 touch smoke";label.fontSize=30;label.color=Color.black;label.alignment=TextAnchor.MiddleCenter;
                var tr=(RectTransform)text.transform;tr.anchorMin=Vector2.zero;tr.anchorMax=Vector2.one;tr.offsetMin=tr.offsetMax=Vector2.zero;
                var close=new GameObject("Close",typeof(RectTransform),typeof(Image),typeof(Button));close.transform.SetParent(go.transform,false);
                var cr=(RectTransform)close.transform;cr.anchorMin=new Vector2(.25f,.1f);cr.anchorMax=new Vector2(.75f,.4f);cr.offsetMin=cr.offsetMax=Vector2.zero;close.GetComponent<Image>().color=Color.green;
                var marker=go.AddComponent<UIBindingMarker>();marker.viewScript=AssetDatabase.LoadAssetAtPath<MonoScript>("Assets/UIKitMobileSmoke/MobileSmokeView.cs");marker.typeKey="mobile-direct";marker.layer=UILayer.Modal;
                PrefabUtility.SaveAsPrefabAsset(go,Prefab);
            }finally{UnityEngine.Object.DestroyImmediate(go);}
            foreach(var name in new[]{"ViewA","ViewB","DefaultView"})
            {
                var path="Assets/UIKitCodegenFixture/"+name+".prefab";
                var script=AssetDatabase.FindAssets(name+" t:MonoScript",new[]{"Assets/UIKitCodegenFixture"}).Select(AssetDatabase.GUIDToAssetPath).Where(p=>Path.GetFileName(p)==name+".cs").Select(AssetDatabase.LoadAssetAtPath<MonoScript>).Single();
                var probe=new GameObject(name,typeof(RectTransform));
                try
                {
                    var marker=probe.AddComponent<UIBindingMarker>();marker.viewScript=script;marker.typeKey="generated-"+name;
                    var label=new GameObject("Serialized binding",typeof(RectTransform));label.transform.SetParent(probe.transform,false);
                    marker.bindings=new[]{new UIBindingMarker.Binding{fieldName="label",target=label.transform}};
                    PrefabUtility.SaveAsPrefabAsset(probe,path);
                }finally{UnityEngine.Object.DestroyImmediate(probe);}
            }
            AssetDatabase.SaveAssets();
        }
        public static void Create()
        {
            Directory.CreateDirectory("Assets/Resources");AssetDatabase.Refresh();
            if(!AssetDatabase.CopyAsset(Prefab,"Assets/Resources/UIKitMobileView.prefab"))throw new InvalidOperationException("Resource copy failed");
            var resource=PrefabUtility.LoadPrefabContents("Assets/Resources/UIKitMobileView.prefab");
            UnityEngine.Object.DestroyImmediate(resource.GetComponent<UIBindingMarker>());PrefabUtility.SaveAsPrefabAsset(resource,"Assets/Resources/UIKitMobileView.prefab");PrefabUtility.UnloadPrefabContents(resource);
            var provider=ScriptableObject.CreateInstance<MobileResourceProvider>();AssetDatabase.CreateAsset(provider,"Assets/MobileResourceProvider.asset");
            var registry=ScriptableObject.CreateInstance<UIRegistry>();AssetDatabase.CreateAsset(registry,"Assets/MobileResourceRegistry.asset");
            var so=new SerializedObject(registry);var list=so.FindProperty("providers");list.arraySize=1;list.GetArrayElementAtIndex(0).objectReferenceValue=provider;so.ApplyModifiedPropertiesWithoutUndo();
            var scene=EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);
            var camera=new GameObject("Smoke background",typeof(Camera)).GetComponent<Camera>();camera.clearFlags=CameraClearFlags.SolidColor;camera.backgroundColor=new Color(.04f,.06f,.09f,1);camera.cullingMask=0;
            var statusCanvas=new GameObject("Smoke status",typeof(RectTransform),typeof(Canvas)).GetComponent<Canvas>();statusCanvas.renderMode=RenderMode.ScreenSpaceOverlay;statusCanvas.sortingOrder=100;
            var status=new GameObject("Result",typeof(RectTransform),typeof(Text)).GetComponent<Text>();status.transform.SetParent(statusCanvas.transform,false);status.font=Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");status.fontSize=26;status.color=Color.white;status.raycastTarget=false;status.text="T11 smoke loading...";
            var statusRect=(RectTransform)status.transform;statusRect.anchorMin=Vector2.zero;statusRect.anchorMax=new Vector2(1,.14f);statusRect.offsetMin=new Vector2(16,12);statusRect.offsetMax=new Vector2(-16,-12);
            var root=new GameObject("UI Root",typeof(RectTransform),typeof(UIRoot));root.GetComponent<Canvas>().renderMode=RenderMode.ScreenSpaceOverlay;
            var events=new GameObject("Host input",typeof(EventSystem));
            var input=Type.GetType("Wulato.UIKit.InputSystem.InputSystemUIInputDriver, Wulato.UIKit.InputSystem",false);
            if(input!=null)events.AddComponent(input);else events.AddComponent<LegacyUIInputDriver>();
            var runner=new GameObject("T11 mobile smoke").AddComponent<MobileSmokeRunner>();runner.statusLabel=status;runner.root=root.GetComponent<UIRoot>();runner.directRegistry=AssetDatabase.LoadAssetAtPath<UIRegistry>(Wulato.UIKit.Editor.UICodegenPipeline.RegistryPath);runner.resourceRegistry=registry;
            var mapping=runner.directRegistry.CopyValidated();
            foreach(var name in new[]{"ViewA","ViewB","DefaultView"})
                if(!mapping.Get(new UIViewTypeKey("generated-"+name)).Accepts(MobileSmokeRunner.GeneratedArguments(name)))throw new InvalidOperationException("Mobile fixture argument mismatch: "+name);
            var request=JsonUtility.FromJson<Wulato.UIKit.Editor.Verification.UIPlayerBuild.Request>(File.ReadAllText(Environment.GetEnvironmentVariable("UIKIT_BUILD_REQUEST")));
            runner.buildId=request.build_id;runner.phase=string.IsNullOrEmpty(request.phase)?"phase1":request.phase;
            if(runner.phase=="phase2"||runner.phase=="uikit05")runner.flowRegistry=AssetDatabase.LoadAssetAtPath<UIRegistry>("Assets/Samples/Wulato UIKit/BusinessFlow/BusinessFlowRegistry.asset");
            EditorSceneManager.SaveScene(scene,"Assets/UIKitMobileSmokeScene.unity");AssetDatabase.SaveAssets();
        }
    }
}
#endif
