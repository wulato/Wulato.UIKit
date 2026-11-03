using System;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
namespace Wulato.UIKit.Editor.Authoring
{
    internal static class UIContextHostAuthoring
    {
        public static UIContextHost Apply(UIRoot root,UIRegistry registry)
        {
            if(EditorApplication.isPlayingOrWillChangePlaymode)throw new InvalidOperationException("Configure the host outside PlayMode.");
            if(!root||!registry||!root.gameObject.scene.IsValid()||!root.gameObject.scene.isLoaded)throw new ArgumentException("Select a scene Root and Registry.");
            var hosts=root.gameObject.scene.GetRootGameObjects().SelectMany(g=>g.GetComponentsInChildren<UIContextHost>(true)).ToArray();
            foreach(var other in hosts)
            {
                var reference=new SerializedObject(other).FindProperty("root").objectReferenceValue;
                if(reference==root&&other.gameObject!=root.gameObject)throw new InvalidOperationException("Another host already owns this Root.");
            }
            var host=root.GetComponent<UIContextHost>();
            if(host!=null)
            {
                var assigned=new SerializedObject(host).FindProperty("root").objectReferenceValue;
                if(assigned!=null&&assigned!=root)throw new InvalidOperationException("This host is assigned to another Root.");
            }
            Undo.IncrementCurrentGroup();int group=Undo.GetCurrentGroup();Undo.SetCurrentGroupName("Configure UIKit Context Host");
            try
            {
                if(!host)host=Undo.AddComponent<UIContextHost>(root.gameObject);
                Undo.RecordObject(host,"Configure Context Host");var so=new SerializedObject(host);
                so.FindProperty("root").objectReferenceValue=root;so.FindProperty("registry").objectReferenceValue=registry;so.ApplyModifiedProperties();
                EditorSceneManager.MarkSceneDirty(root.gameObject.scene);Undo.CollapseUndoOperations(group);return host;
            }
            catch{Undo.RevertAllDownToGroup(group);throw;}
        }
    }
}
