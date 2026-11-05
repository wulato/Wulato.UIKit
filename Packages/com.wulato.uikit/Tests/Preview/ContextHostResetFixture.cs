using System;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
namespace Wulato.UIKit.Tests.Preview
{
    // Used only by the existing independent Editor reset executor, never shipped to Players.
    public static class ContextHostResetFixture
    {
        const string Folder="Assets/UIKitContextHostResetFixture",Key="UIKit.HostReset.Layout";
        [Serializable] sealed class Layout {public SceneSetup[] scenes;}
        public static void Prepare()
        {
            if(AssetDatabase.IsValidFolder(Folder))throw new InvalidOperationException("Host reset fixture already exists.");
            var setup=EditorSceneManager.GetSceneManagerSetup();SessionState.SetString(Key,JsonUtility.ToJson(new Layout{scenes=setup}));
            AssetDatabase.CreateFolder("Assets","UIKitContextHostResetFixture");
            var registry=ScriptableObject.CreateInstance<UIRegistry>();AssetDatabase.CreateAsset(registry,Folder+"/Registry.asset");
            var scene=EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);
            var root=new GameObject("ContextHostReloadRoot",typeof(RectTransform),typeof(UIRoot));root.GetComponent<Canvas>().renderMode=RenderMode.ScreenSpaceOverlay;
            var events=new GameObject("ContextHostReloadEvents",typeof(EventSystem));
#if ENABLE_INPUT_SYSTEM && !ENABLE_LEGACY_INPUT_MANAGER
            events.AddComponent(Type.GetType("Wulato.UIKit.InputSystem.InputSystemUIInputDriver, Wulato.UIKit.InputSystem",true));
#else
            events.AddComponent<LegacyUIInputDriver>();
#endif
            var host=root.AddComponent<UIContextHost>();var so=new SerializedObject(host);so.FindProperty("root").objectReferenceValue=root.GetComponent<UIRoot>();so.FindProperty("registry").objectReferenceValue=registry;so.ApplyModifiedPropertiesWithoutUndo();
            EditorSceneManager.SaveScene(scene,Folder+"/Host.unity");
        }
        public static void Restore()
        {
            var json=SessionState.GetString(Key,"");if(string.IsNullOrEmpty(json))return;
            var scenes=JsonUtility.FromJson<Layout>(json).scenes;
            if(scenes.Any(s=>!string.IsNullOrEmpty(s.path)))EditorSceneManager.RestoreSceneManagerSetup(scenes);else EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);
            AssetDatabase.DeleteAsset(Folder);SessionState.EraseString(Key);
        }
    }
}
