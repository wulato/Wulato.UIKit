using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.PackageManager;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
namespace Wulato.UIKit.Editor.Authoring
{
    // Public Editor utility so imported sample tools can share path and ownership rules.
    public static class UISampleAuthoring
    {
        public static readonly string[] Names={"QuickStart","Modals","Inventory"};
        public static string ViewName(string name)=>name=="QuickStart"?"SettingsView":name=="Modals"?"StackedModalView":"InventoryView";
        public static string SceneName(string name)=>(name=="QuickStart"?"Settings":name)+"Scene.unity";
        public static string[] FindDirectories(string name)
        {
            if(!Names.Contains(name))throw new ArgumentException("未知样例。");
            string expected="Wulato.UIKit.Samples."+name+"."+name+"Demo";
            return AssetDatabase.FindAssets(name+"Demo t:MonoScript",new[]{"Assets"}).Select(AssetDatabase.GUIDToAssetPath)
                .Where(p=>Path.GetFileName(p)==name+"Demo.cs"&&AssetDatabase.LoadAssetAtPath<MonoScript>(p)?.GetClass()?.FullName==expected)
                .Select(p=>Path.GetDirectoryName(p).Replace('\\','/')).Distinct().OrderBy(p=>p,StringComparer.Ordinal).ToArray();
        }
        public static string SelectDirectory(string[] candidates,string selected)
        {
            if(!string.IsNullOrEmpty(selected))
            {if(!candidates.Contains(selected))throw new ArgumentException("目录不是已导入样例候选："+selected);return selected;}
            if(candidates.Length!=1)throw new InvalidOperationException(candidates.Length==0?"请先导入所选样例。":"发现多份样例，请明确选择目录："+string.Join(", ",candidates));
            return candidates[0];
        }
        public static void InstallUsage(string source,string target)
        {
            var expected=File.ReadAllText(source);
            if(File.Exists(target)){if(File.ReadAllText(target)!=expected)throw new IOException("Usage 已被修改，拒绝覆盖："+target);return;}
            File.WriteAllText(target,expected);
        }
        public static void CheckSources(string name,string directory)
        {
            var package=UnityEditor.PackageManager.PackageInfo.FindForAssembly(typeof(UIRoot).Assembly);
            if(package==null)throw new InvalidOperationException("无法定位主包源文件。");
            var original=Path.Combine(package.resolvedPath,"Samples~",name);
            foreach(var source in Directory.GetFiles(original,"*",SearchOption.AllDirectories).Where(p=>p.EndsWith(".cs",StringComparison.Ordinal)||p.EndsWith(".cs.txt",StringComparison.Ordinal)||p.EndsWith(".asmdef",StringComparison.Ordinal)))
            {
                var target=directory+"/"+source.Substring(original.Length+1).Replace('\\','/');
                if(!File.Exists(target)||File.ReadAllText(target)!=File.ReadAllText(source))throw new IOException("样例源码缺失或已被修改；拒绝自动配置："+target);
            }
            var usage=directory+"/"+ViewName(name)+".Usage.cs";
            if(File.Exists(usage)&&File.ReadAllText(usage)!=File.ReadAllText(usage+".txt"))throw new IOException("Usage 已被修改："+usage);
            if(!File.Exists(directory+"/"+ViewName(name)+".prefab"))throw new IOException("样例 Prefab 缺失，请重新导入："+directory);
        }
        public static void WireScene(string name,string directory,Type driver)
        {
            var path=directory+"/"+SceneName(name);
            var setup=EditorSceneManager.GetSceneManagerSetup();
            bool untitled=setup.Length==1&&string.IsNullOrEmpty(setup[0].path);
            var scene=SceneManager.GetSceneByPath(path);bool owned=!scene.IsValid()||!scene.isLoaded;
            if(owned)scene=EditorSceneManager.OpenScene(path,untitled?OpenSceneMode.Single:OpenSceneMode.Additive);
            try
            {
                var root=scene.GetRootGameObjects().SelectMany(g=>g.GetComponentsInChildren<UIRoot>(true)).SingleOrDefault();
                var registry=AssetDatabase.LoadAssetAtPath<UIRegistry>(UICodegenPipeline.RegistryPath);
                var script=AssetDatabase.LoadAssetAtPath<MonoScript>(directory+"/"+name+"Demo.cs");var type=script.GetClass();
                var demo=scene.GetRootGameObjects().SelectMany(g=>g.GetComponentsInChildren(type,true)).SingleOrDefault();
                if(demo==null)throw new InvalidOperationException("场景缺少样例 Demo："+path);
                root=UISceneAuthoring.Apply(scene,root,registry,null,driver);
                EnsurePreviewCamera(scene);
                var so=new SerializedObject(demo);so.FindProperty("root").objectReferenceValue=root;so.FindProperty("registry").objectReferenceValue=registry;so.ApplyModifiedPropertiesWithoutUndo();
                if(!EditorSceneManager.SaveScene(scene,path))throw new IOException("无法保存样例场景："+path);
            }
            finally
            {
                if(owned)
                {
                    if(untitled)EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);
                    else EditorSceneManager.CloseScene(scene,true);
                }
            }
        }
        // Overlay canvases still need a camera in sample scenes to avoid the Editor's
        // "No cameras rendering" warning obscuring the actual UI in Game view.
        internal static Camera EnsurePreviewCamera(Scene scene)
        {
            if(!scene.IsValid()||!scene.isLoaded)throw new ArgumentException("请选择已加载的样例场景。");
            var existing=scene.GetRootGameObjects().SelectMany(g=>g.GetComponentsInChildren<Camera>(true))
                .FirstOrDefault(c=>c.enabled&&c.gameObject.activeInHierarchy);
            if(existing!=null)return existing;
            Undo.IncrementCurrentGroup();int group=Undo.GetCurrentGroup();Undo.SetCurrentGroupName("Create sample preview camera");
            var go=new GameObject("Sample preview camera",typeof(Camera));SceneManager.MoveGameObjectToScene(go,scene);
            Undo.RegisterCreatedObjectUndo(go,"Create sample preview camera");
            var camera=go.GetComponent<Camera>();camera.clearFlags=CameraClearFlags.SolidColor;
            camera.backgroundColor=new Color(.025f,.035f,.055f,1f);camera.cullingMask=0;
            EditorSceneManager.MarkSceneDirty(scene);Undo.CollapseUndoOperations(group);return camera;
        }
        public static void ValidateOne(string name,string directory)
        {
            CheckSources(name,directory);UICodegenPipeline.Validate();
            var mappings=AssetDatabase.LoadAssetAtPath<UIRegistry>(UICodegenPipeline.RegistryPath).CopyValidated();
            var entry=mappings.Entries.Single(e=>e.Key.Value=="sample-"+name);var prefab=entry.Asset.Prefab;
            if(entry.ViewType.Assembly.GetName().Name!="Wulato.UIKit.Samples."+name)throw new InvalidOperationException("样例程序集路由错误。");
            var so=new SerializedObject(prefab.GetComponent(entry.ViewType));
            foreach(var field in new[]{"titleLabel","closeButton"})if(so.FindProperty(field)?.objectReferenceValue==null)throw new InvalidOperationException("缺少生成绑定："+field);
            if(!File.Exists(directory+"/"+SceneName(name)))throw new IOException("缺少样例场景。");
        }
    }
}
