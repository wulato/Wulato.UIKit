using System;
using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
namespace Wulato.UIKit.Editor.Authoring
{
    internal sealed class UIAuthoringWorkbench : EditorWindow
    {
        [SerializeField] int tab;
        [SerializeField] UIRoot root;
        [SerializeField] UIRegistry registry;
        [SerializeField] EventSystem events;
        [SerializeField] MonoScript driverScript,viewScript;
        [SerializeField] GameObject prefab;
        [SerializeField] string folder="Assets/UI",viewNamespace="Game.UI",className="SettingsView",argsType="";
        [SerializeField] bool existing,typed;
        [SerializeField] int sampleIndex;
        [SerializeField] string sampleDirectory="";
        [SerializeField] string inspectedRequestPrefab;
        Vector2 scroll;
        [MenuItem("Tools/Wulato/UIKit/Workbench",false,0)]
        public static void Open()=>GetWindow<UIAuthoringWorkbench>("UIKit Workbench");
        void OnEnable(){minSize=new Vector2(720,440);EditorApplication.update+=Refresh;if(registry==null)registry=AssetDatabase.LoadAssetAtPath<UIRegistry>(UICodegenPipeline.RegistryPath);}
        void OnDisable(){EditorApplication.update-=Refresh;}
        void Refresh(){if(UIAuthoringOperation.Busy)Repaint();}
        Type Driver=>driverScript==null?typeof(LegacyUIInputDriver):driverScript.GetClass();
        void Run(Action action)
        {
            try{action();Repaint();}
            catch(Exception e){UIAuthoringOperation.Fail("workbench",prefab==null?"":AssetDatabase.GetAssetPath(prefab),e.ToString());Debug.LogException(e);}
        }
        void OnGUI()
        {
            tab=GUILayout.Toolbar(tab,new[]{"场景","界面","生成","样例"});scroll=EditorGUILayout.BeginScrollView(scroll);
            EditorGUILayout.Space();
            using(new EditorGUI.DisabledScope(UIAuthoringOperation.Busy||EditorApplication.isCompiling||EditorApplication.isPlayingOrWillChangePlaymode))
            {switch(tab){case 0:DrawScene();break;case 1:DrawView();break;case 2:DrawGeneration();break;case 3:DrawSamples();break;}}
            EditorGUILayout.Space();EditorGUILayout.HelpBox(UIAuthoringOperation.Describe(),UIAuthoringOperation.Current.status=="失败"?MessageType.Error:MessageType.Info);
            if(!string.IsNullOrEmpty(UIAuthoringOperation.Current.asset)&&GUILayout.Button("定位关联资产"))
            {var asset=AssetDatabase.LoadMainAssetAtPath(UIAuthoringOperation.Current.asset);if(asset!=null){Selection.activeObject=asset;EditorGUIUtility.PingObject(asset);}}
            if(UIAuthoringOperation.Current.status=="失败"&&UIAuthoringOperation.Current.stage!="workbench"&&GUILayout.Button("修复后重试"))Run(()=>{AssetDatabase.SaveAssets();UIAuthoringOperation.Retry();});
            if(UIAuthoringOperation.Busy&&GUILayout.Button("取消后续步骤（保留已有资产）"))UIAuthoringOperation.Cancel();
            EditorGUILayout.EndScrollView();
        }
        void DrawDriver()
        {
            driverScript=(MonoScript)EditorGUILayout.ObjectField("输入驱动脚本",driverScript,typeof(MonoScript),false);
            EditorGUILayout.HelpBox("留空使用 LegacyUIInputDriver。Input System 项目请拖入已安装适配包的 InputSystemUIInputDriver 脚本；不会自动安装、扫描或切换项目输入设置。",MessageType.Info);
        }
        void DrawScene()
        {
            EditorGUILayout.LabelField("目标场景",SceneManager.GetActiveScene().name);
            root=(UIRoot)EditorGUILayout.ObjectField("UIRoot",root,typeof(UIRoot),true);
            registry=(UIRegistry)EditorGUILayout.ObjectField("Registry",registry,typeof(UIRegistry),false);
            events=(EventSystem)EditorGUILayout.ObjectField("EventSystem",events,typeof(EventSystem),true);DrawDriver();
            if(registry==null)EditorGUILayout.HelpBox("先在界面页创建界面并生成 Registry，再配置场景。",MessageType.Warning);
            if(GUILayout.Button("检查并配置场景（支持 Undo，不自动保存）"))Run(()=>{
                root=UISceneAuthoring.Apply(SceneManager.GetActiveScene(),root,registry,events,Driver);
                EditorGUIUtility.PingObject(root);
            });
            EditorGUILayout.HelpBox("本页不创建运行时 Context。在业务初始化中持有返回的 IUIContext，Root 销毁仍按既有清理路径处理。",MessageType.Info);
            EditorGUILayout.SelectableLabel("IUIContext ui = new UIContextBuilder(root, registry).Build();\nIUISession<SettingsView> session = await ui.OpenAsync<SettingsView>();",GUILayout.Height(42));
        }
        internal void SelectPrefab(GameObject selected)
        {
            prefab=selected;var marker=prefab==null?null:prefab.GetComponent<UIBindingMarker>();
            viewScript=marker==null?null:marker.viewScript as MonoScript;
        }
        void DrawView()
        {
            var request=UIAuthoringOperation.Current;
            if(request.status=="完成"&&!string.IsNullOrEmpty(request.prefab)&&inspectedRequestPrefab!=request.prefab)
            {
                var completed=AssetDatabase.LoadAssetAtPath<GameObject>(request.prefab);
                if(completed!=null){SelectPrefab(completed);inspectedRequestPrefab=request.prefab;}
            }
            existing=EditorGUILayout.Toggle("使用已有 Prefab",existing);
            if(!existing)
            {
                folder=EditorGUILayout.TextField("目标目录",folder);viewNamespace=EditorGUILayout.TextField("命名空间",viewNamespace);className=EditorGUILayout.TextField("类名",className);
                typed=EditorGUILayout.Toggle("带参数",typed);if(typed)argsType=EditorGUILayout.TextField("参数类型（完整类型名）",argsType);
                if(GUILayout.Button("创建脚本、Prefab 并生成"))Run(()=>{
                    if(!folder.StartsWith("Assets/",StringComparison.Ordinal)||folder.Split('/').ContainsDotDot())throw new ArgumentException("目录必须位于 Assets 下。");
                    Type args=null;
                    if(typed)
                    {
                        args=Type.GetType(argsType,false);
                        if(args==null)foreach(var assembly in AppDomain.CurrentDomain.GetAssemblies()){var found=assembly.GetType(argsType,false);if(found!=null){if(args!=null)throw new ArgumentException("参数类型不唯一，请填写程序集限定名。");args=found;}}
                        if(args==null)throw new ArgumentException("参数类型尚未编译；请先创建并编译参数类型。");
                    }
                    UIAuthoringOperation.Create(folder+"/"+className+".cs",folder+"/"+className+".prefab",viewNamespace,className,args);
                });
            }
            var selected=(GameObject)EditorGUILayout.ObjectField("Prefab",prefab,typeof(GameObject),false);
            if(selected!=prefab)SelectPrefab(selected);
            viewScript=(MonoScript)EditorGUILayout.ObjectField("View 脚本",viewScript,typeof(MonoScript),false);
            if(existing&&GUILayout.Button("配置已有 Prefab 并生成"))Run(()=>{
                if(prefab==null)throw new ArgumentException("请选择普通 UI Prefab。");
                UIAuthoringOperation.Configure(AssetDatabase.GetAssetPath(prefab),viewScript);
            });
            if(prefab==null&&!string.IsNullOrEmpty(UIAuthoringOperation.Current.prefab))SelectPrefab(AssetDatabase.LoadAssetAtPath<GameObject>(UIAuthoringOperation.Current.prefab));
            var marker=prefab==null?null:prefab.GetComponent<UIBindingMarker>();
            if(marker!=null)
            {
                EditorGUILayout.Space();EditorGUILayout.LabelField("策略与组件绑定",EditorStyles.boldLabel);
                // A cached Editor can lose its native targets after script reload/writeback.
                // Draw fresh serialized properties instead of retaining that Editor across reloads.
                using(var serialized=new SerializedObject(marker))
                {
                    serialized.Update();var property=serialized.GetIterator();bool enterChildren=true;
                    while(property.NextVisible(enterChildren))
                    {
                        enterChildren=false;if(property.propertyPath=="m_Script")continue;
                        EditorGUILayout.PropertyField(property,true);
                    }
                    serialized.ApplyModifiedProperties();
                }
                EditorGUILayout.HelpBox("Bindings 使用明确字段名和 Prefab 内组件引用。编辑后保存并重新生成；不自动猜测节点名。Variant 请使用既有手工流程。",MessageType.Info);
                if(GUILayout.Button("保存 Marker 并重新生成全部注册"))Run(()=>{AssetDatabase.SaveAssets();UIAuthoringOperation.Generate();});
            }
        }
        void DrawGeneration()
        {
            EditorGUILayout.HelpBox("始终生成完整注册集合。保留文件所有权、指纹、路由及构建校验；编译失败请先修复源码，再重试。",MessageType.Info);
            if(GUILayout.Button("生成全部注册与绑定"))Run(()=>{AssetDatabase.SaveAssets();UIAuthoringOperation.Generate();});
            if(GUILayout.Button("完成现有写回"))Run(()=>{UICodegenPipeline.Complete();UICodegenPipeline.Validate();});
            if(GUILayout.Button("只读校验"))Run(UICodegenPipeline.Validate);
        }
        void DrawSamples()
        {
            int next=EditorGUILayout.Popup("样例",sampleIndex,UISampleAuthoring.Names);if(next!=sampleIndex){sampleIndex=next;sampleDirectory="";}
            var name=UISampleAuthoring.Names[sampleIndex];var dirs=UISampleAuthoring.FindDirectories(name);
            if(dirs.Length==0){EditorGUILayout.HelpBox("请通过 Package Manager 导入 "+name+"。QuickStart 可单独使用，无需移动目录。",MessageType.Info);return;}
            if(dirs.Length==1)sampleDirectory=dirs[0];
            else
            {
                int selected=Array.IndexOf(dirs,sampleDirectory);var labels=new string[dirs.Length+1];labels[0]="请选择明确目录";Array.Copy(dirs,0,labels,1,dirs.Length);
                int choice=EditorGUILayout.Popup("导入目录",selected+1,labels);sampleDirectory=choice==0?"":dirs[choice-1];
            }
            EditorGUILayout.LabelField("目标",sampleDirectory);DrawDriver();
            if(GUILayout.Button("预览并配置所选样例"))Run(()=>{
                var dir=UISampleAuthoring.SelectDirectory(dirs,sampleDirectory);UISampleAuthoring.CheckSources(name,dir);
                string summary="目录："+dir+"\n生成："+UICodegenPipeline.Root+"\n安装/校验 Usage partial；配置 "+UISampleAuthoring.SceneName(name)+" 的 Root、Registry 和所选输入驱动；保存样例场景。\n不移动样例，不覆盖已修改的源码。";
                if(EditorUtility.DisplayDialog("配置样例改动预览",summary,"应用","取消"))UIAuthoringOperation.Sample(name,dir,Driver);
            });
            var scene=sampleDirectory+"/"+UISampleAuthoring.SceneName(name);
            if(File.Exists(scene)&&GUILayout.Button("打开样例场景"))Run(()=>{if(UnityEditor.SceneManagement.EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())UnityEditor.SceneManagement.EditorSceneManager.OpenScene(scene);});
        }
    }
    internal static class UIAuthoringPathExtensions
    {
        internal static bool ContainsDotDot(this string[] parts)=>Array.IndexOf(parts,"..")>=0;
    }
}
