using UnityEditor;
using UnityEngine;
namespace Wulato.UIKit.Editor
{
    public static class UIRootMenu
    {
        [MenuItem("GameObject/UI/Wulato UI Root",false,2050)]
        static void CreateRoot(MenuCommand command)
        {
            var root=new GameObject("UIRoot",typeof(RectTransform),typeof(UIRoot));
            GameObjectUtility.SetParentAndAlign(root,command.context as GameObject);root.GetComponent<Canvas>().renderMode=RenderMode.ScreenSpaceOverlay;
            Undo.RegisterCreatedObjectUndo(root,"Create UI Root");Selection.activeGameObject=root;
        }
        [MenuItem("Assets/Create/Wulato/UIKit/View partial")]
        static void CreateView()
        {
            var path=EditorUtility.SaveFilePanelInProject("Create View partial","NewView","cs","Select the business script location.");
            if(string.IsNullOrEmpty(path))return;
            UIBindingGenerator.CreateUserPartial(path,"",System.IO.Path.GetFileNameWithoutExtension(path));AssetDatabase.Refresh();
        }
    }
}
