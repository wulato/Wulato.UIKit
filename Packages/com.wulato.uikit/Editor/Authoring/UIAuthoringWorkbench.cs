using System;
using System.IO;
using Cysharp.Threading.Tasks;
using UnityEditor;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
namespace Wulato.UIKit.Editor.Authoring
{
    internal sealed class UIAuthoringWorkbench : EditorWindow
    {
        [SerializeField] int tab;
        [SerializeField] UIPreviewProfile previewProfile;
        [SerializeField] UIRoot root;
        [SerializeField] UIRegistry registry;
        [SerializeField] EventSystem events;
        [SerializeField] MonoScript driverScript,viewScript;
        [SerializeField] GameObject prefab;
        [SerializeField] string folder="Assets/UI",viewNamespace="Game.UI",className="SettingsView",argsType="";
        [SerializeField] bool existing,typed;
        [SerializeField] int sampleIndex,template,driverChoice,sourceChoice;
        [SerializeField] bool presenter;
        [SerializeField] string sourceProvider="",sourceId="";
        [SerializeField] UIRoot presetsRoot;
        [SerializeField] UITransitionPreset transitionPreset;
        [SerializeField] UINavigationTransitionPreset navigationPreset;
        [SerializeField] string sampleDirectory="";
        [SerializeField] string inspectedRequestPrefab;
        Vector2 scroll;
        [MenuItem("Tools/Wulato/UIKit/Workbench",false,0)]
        public static void Open()=>GetWindow<UIAuthoringWorkbench>("UIKit Workbench");
        void OnEnable(){minSize=new Vector2(720,440);EditorApplication.update+=Refresh;if(registry==null)registry=AssetDatabase.LoadAssetAtPath<UIRegistry>(UICodegenPipeline.RegistryPath);}
        void OnDisable(){EditorApplication.update-=Refresh;}
        void OnDestroy(){if(UIPreviewController.Active&&EditorApplication.isPlayingOrWillChangePlaymode)UIPreviewController.StopAsync().Forget(UIPreviewController.Report);}
        void Refresh(){if(UIAuthoringOperation.Busy||UIPreviewController.Active)Repaint();}
        Type Driver=>driverChoice==0?UISceneAuthoring.DefaultDriver():driverChoice==1?typeof(LegacyUIInputDriver):driverChoice==2?Type.GetType("Wulato.UIKit.InputSystem.InputSystemUIInputDriver, Wulato.UIKit.InputSystem",true):driverScript==null?null:driverScript.GetClass();
        void Run(Action action)
        {
            try{action();Repaint();}
            catch(Exception e){UIAuthoringOperation.Fail("workbench",prefab==null?"":AssetDatabase.GetAssetPath(prefab),e.ToString());Debug.LogException(e);}
        }
        void OnGUI()
        {
            tab=GUILayout.Toolbar(tab,new[]{"场景","界面","生成","样例","预览"});scroll=EditorGUILayout.BeginScrollView(scroll);
            EditorGUILayout.Space();
            if(tab==4){try{UIPreviewPage.Draw(ref previewProfile);}catch(Exception e){UIPreviewController.Report(e);}}
            else using(new EditorGUI.DisabledScope(UIPreviewController.Active||UIAuthoringOperation.Busy||EditorApplication.isCompiling||EditorApplication.isPlayingOrWillChangePlaymode))
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
            driverChoice=EditorGUILayout.Popup("输入驱动",driverChoice,new[]{"按当前输入设置","Legacy","Input System","自定义驱动"});
            if(driverChoice==3)driverScript=(MonoScript)EditorGUILayout.ObjectField("输入驱动脚本",driverScript,typeof(MonoScript),false);
            EditorGUILayout.HelpBox("自动选择只依据已启用输入后端；Both 必须明确选择驱动。不安装 SDK、不更改 ProjectSettings，不替换冲突模块。",MessageType.Info);
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
            if(presetsRoot!=root)
            {
                presetsRoot=root;var host=root?root.GetComponent<UIContextHost>():null;
                transitionPreset=null;navigationPreset=null;
                if(host){var so=new SerializedObject(host);transitionPreset=so.FindProperty("transitionPreset").objectReferenceValue as UITransitionPreset;navigationPreset=so.FindProperty("navigationPreset").objectReferenceValue as UINavigationTransitionPreset;}
            }
            transitionPreset=(UITransitionPreset)EditorGUILayout.ObjectField("单界面 Preset",transitionPreset,typeof(UITransitionPreset),false);
            navigationPreset=(UINavigationTransitionPreset)EditorGUILayout.ObjectField("页面配对 Preset",navigationPreset,typeof(UINavigationTransitionPreset),false);
            EditorGUILayout.HelpBox("初始化会配置 Root、EventSystem 和 Context Host；无需手工装配导航、Dialog、Popup。Preset 留空使用 Immediate；已有宿主可在 Inspector 编辑策略。",MessageType.Info);
            if(GUILayout.Button("初始化场景与 Host（支持 Undo，不自动保存）"))Run(()=>{
                root=UISceneAuthoring.InstallHost(SceneManager.GetActiveScene(),root,registry,events,Driver,transitionPreset,navigationPreset);
                EditorGUIUtility.PingObject(root.GetComponent<UIContextHost>());
            });
            EditorGUILayout.SelectableLabel("IUIContext ui = host.UI;\nawait ui.OpenAsync<SettingsView>();",GUILayout.Height(42));

        }
        internal void SelectPrefab(GameObject selected)
        {
            prefab=selected;var marker=prefab==null?null:prefab.GetComponent<UIBindingMarker>();
            viewScript=marker==null?null:marker.viewScript as MonoScript;
            sourceProvider=marker==null?"":marker.externalProviderId;sourceId=marker==null?"":marker.externalAssetId;
            sourceChoice=string.IsNullOrEmpty(sourceProvider)?0:sourceProvider=="addressables"?1:2;
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
                template=EditorGUILayout.Popup("模板",template,new[]{"Page · Single / Release","Modal · Multi / Release","Dialog · Multi / Release"});
                presenter=EditorGUILayout.Toggle("可选 Presenter",presenter);
                if(template!=2){typed=EditorGUILayout.Toggle("带参数",typed);if(typed)argsType=EditorGUILayout.TextField("参数类型（完整类型名）",argsType);}
                else EditorGUILayout.HelpBox("Dialog 模板生成独立 Request/bool 结果与 Session 安全的 Submit/Cancel；创建后由业务编辑。",MessageType.Info);
                if(GUILayout.Button("创建脚本、Prefab 并生成"))Run(()=>{
                    if(!folder.StartsWith("Assets/",StringComparison.Ordinal)||folder.Split('/').ContainsDotDot())throw new ArgumentException("目录必须位于 Assets 下。");
                    Type args=null;
                    if(typed&&template!=2)
                    {
                        args=Type.GetType(argsType,false);
                        if(args==null)foreach(var assembly in AppDomain.CurrentDomain.GetAssemblies()){var found=assembly.GetType(argsType,false);if(found!=null){if(args!=null)throw new ArgumentException("参数类型不唯一，请填写程序集限定名。");args=found;}}
                        if(args==null)throw new ArgumentException("参数类型尚未编译；请先创建并编译参数类型。");
                    }
                    UIAuthoringOperation.CreateTemplate(folder+"/"+className+".cs",folder+"/"+className+".prefab",viewNamespace,className,args,template,presenter);
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
                EditorGUILayout.Space();EditorGUILayout.LabelField("资源来源",EditorStyles.boldLabel);
                sourceChoice=EditorGUILayout.Popup("来源",sourceChoice,new[]{"Prefab 直接引用","Addressables 已有条目","自定义外部来源"});
                if(sourceChoice==2)sourceProvider=EditorGUILayout.TextField("Provider ID",sourceProvider);
                if(sourceChoice!=0)sourceId=EditorGUILayout.TextField("Asset ID / Address / GUID",sourceId);
                if(GUILayout.Button("校验并保存来源"))Run(()=>UIAssetSourceAuthoring.Apply(AssetDatabase.GetAssetPath(prefab),sourceChoice,sourceProvider,sourceId));
                EditorGUILayout.HelpBox("Addressables 使用已配置的显式条目；不自动创建 SDK 设置或 Bundle。修改来源后重新生成。已有策略不会由模板重置。",MessageType.Info);
                EditorGUILayout.Space();EditorGUILayout.LabelField("策略与组件绑定",EditorStyles.boldLabel);
                // A cached Editor can lose its native targets after script reload/writeback.
                // Draw fresh serialized properties instead of retaining that Editor across reloads.
                using(var serialized=new SerializedObject(marker))
                {
                    serialized.Update();var property=serialized.GetIterator();bool enterChildren=true;
                    while(property.NextVisible(enterChildren))
                    {
                        enterChildren=false;if(property.propertyPath=="m_Script"||property.propertyPath=="externalProviderId"||property.propertyPath=="externalAssetId")continue;
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
