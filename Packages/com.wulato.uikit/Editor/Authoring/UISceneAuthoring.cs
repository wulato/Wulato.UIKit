using System;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
namespace Wulato.UIKit.Editor.Authoring
{
    internal static class UISceneAuthoring
    {
        public static void ValidateDriver(Type driver)
        {
            if(driver==null||driver.IsAbstract||driver.ContainsGenericParameters||!typeof(BaseInputModule).IsAssignableFrom(driver)||!typeof(IUIInputDriver).IsAssignableFrom(driver))
                throw new ArgumentException("请选择实现 IUIInputDriver 的具体 BaseInputModule 脚本。");
#if ENABLE_INPUT_SYSTEM && !ENABLE_LEGACY_INPUT_MANAGER
            if(typeof(LegacyUIInputDriver).IsAssignableFrom(driver))throw new ArgumentException("当前项目仅启用 Input System，请明确选择已安装的 InputSystemUIInputDriver 脚本。");
#endif
        }
        public static UIRoot Apply(Scene scene,UIRoot root,UIRegistry registry,EventSystem events,Type driver)
        {
            if(!scene.IsValid()||!scene.isLoaded||registry==null)throw new ArgumentException("请选择已加载场景和生成的 Registry；尚未生成时先创建界面并生成注册。");
            ValidateDriver(driver);
            var systems=scene.GetRootGameObjects().SelectMany(g=>g.GetComponentsInChildren<EventSystem>(true)).ToArray();
            if(systems.Length>1)throw new InvalidOperationException("场景存在多个 EventSystem："+string.Join(", ",systems.Select(e=>e.name)));
            if(events!=null&&events.gameObject.scene!=scene)throw new ArgumentException("EventSystem 不属于目标场景。");
            if(events==null)events=systems.SingleOrDefault();
            if(root!=null&&root.gameObject.scene!=scene)throw new ArgumentException("UIRoot 不属于目标场景。");
            if(root==null)
            {
                var roots=scene.GetRootGameObjects().SelectMany(g=>g.GetComponentsInChildren<UIRoot>(true)).ToArray();
                if(roots.Length>1)throw new InvalidOperationException("存在多个 UIRoot，请明确选择。");root=roots.SingleOrDefault();
            }
            if(root!=null)
            {
                var canvas=root.Canvas;
                if(canvas.renderMode==RenderMode.WorldSpace||(canvas.renderMode==RenderMode.ScreenSpaceCamera&&canvas.worldCamera==null))throw new InvalidOperationException("Root 需要 Overlay 或已配置相机的 Screen Space Camera。");
                var config=new SerializedObject(root);var layers=config.FindProperty("layers");
                if(layers.arraySize!=5)throw new InvalidOperationException("Root 必须配置五个逻辑层。");
                var mount=root.SafeArea!=null?root.SafeArea:root.transform;
                if(mount!=root.transform&&!mount.IsChildOf(root.transform))throw new InvalidOperationException("安全区域不属于 Root。");
                var assigned=Enumerable.Range(0,5).Select(i=>layers.GetArrayElementAtIndex(i).objectReferenceValue as RectTransform).Where(t=>t!=null).ToArray();
                if(assigned.Any(t=>t.parent!=mount)||assigned.Distinct().Count()!=assigned.Length)throw new InvalidOperationException("层级节点必须互不相同并直接属于挂载点。");
            }
            var modules=events==null?Array.Empty<BaseInputModule>():events.GetComponents<BaseInputModule>().Where(m=>m.enabled).ToArray();
            if(modules.Length>1||modules.Any(m=>m.GetType()!=driver))throw new InvalidOperationException("启用的输入模块冲突："+string.Join(", ",modules.Select(m=>m.GetType().Name))+"；请先手动处理，工具不会替换或删除模块。");
            if(events!=null&&(!events.enabled||!events.gameObject.activeInHierarchy))throw new InvalidOperationException("所选 EventSystem 必须处于启用状态。");
            Undo.IncrementCurrentGroup();int group=Undo.GetCurrentGroup();Undo.SetCurrentGroupName("Configure Wulato UIKit scene");
            try
            {
                if(root==null)
                {
                    var go=new GameObject("UIRoot",typeof(RectTransform),typeof(UIRoot));SceneManager.MoveGameObjectToScene(go,scene);
                    Undo.RegisterCreatedObjectUndo(go,"Create UIRoot");root=go.GetComponent<UIRoot>();root.GetComponent<Canvas>().renderMode=RenderMode.ScreenSpaceOverlay;
                }
                if(events==null)
                {
                    var go=new GameObject("EventSystem",typeof(EventSystem));SceneManager.MoveGameObjectToScene(go,scene);
                    Undo.RegisterCreatedObjectUndo(go,"Create EventSystem");events=go.GetComponent<EventSystem>();
                }
                if(modules.Length==0)
                {
                    var existing=events.GetComponents<BaseInputModule>().FirstOrDefault(m=>m.GetType()==driver);
                    if(existing!=null){Undo.RecordObject(existing,"Enable UI driver");existing.enabled=true;}
                    else Undo.AddComponent(events.gameObject,driver);
                }
                EditorSceneManager.MarkSceneDirty(scene);Undo.CollapseUndoOperations(group);return root;
            }
            catch{Undo.RevertAllDownToGroup(group);throw;}
        }
    }
}
