using System;
using System.IO;
using UnityEditor;
using UnityEditor.Compilation;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
namespace Wulato.UIKit.Editor.Authoring
{
    [Serializable] internal sealed class UIAuthoringState
    {
        public string status="空闲",kind,stage,error,asset,script,prefab,sampleName,sampleDirectory,driver;
        public int phase;
        public SceneSetup[] scenes;
    }
    [InitializeOnLoad]
    internal static class UIAuthoringOperation
    {
        const string StatePath="Library/WulatoUIKit/authoring.json";
        static UIAuthoringState state=Read();static bool advancing;
        internal static UIAuthoringState Current=>state;
        internal static bool SuspendAutomaticWriteback=>(state.status=="已取消"||state.status=="失败")&&state.phase>=2;
        internal static bool Busy=>state.status=="进行中"||state.status=="等待编译"||state.status=="待写回";
        static UIAuthoringOperation()
        {
            EditorApplication.update+=Update;
            CompilationPipeline.assemblyCompilationFinished+=(path,messages)=>{
                foreach(var m in messages)if(Busy&&m.type==CompilerMessageType.Error){Fail("compile",m.file,m.message);break;}
            };
        }
        static UIAuthoringState Read()
        {try{return File.Exists(StatePath)?JsonUtility.FromJson<UIAuthoringState>(File.ReadAllText(StatePath)):new UIAuthoringState();}catch{return new UIAuthoringState();}}
        static void Save()
        {
            Directory.CreateDirectory(Path.GetDirectoryName(StatePath));string temporary=StatePath+".tmp";File.WriteAllText(temporary,JsonUtility.ToJson(state,true));
            if(File.Exists(StatePath))File.Replace(temporary,StatePath,null);else File.Move(temporary,StatePath);
        }
        public static void Reset(){state=new UIAuthoringState();Save();}
        public static bool TryBegin(string kind)
        {
            if(Busy)return false;state=new UIAuthoringState{kind=kind,status="进行中",stage="prepare"};Save();return true;
        }
        public static void Fail(string stage,string asset,string message)
        {state.status="失败";state.stage=stage;state.error=message;state.asset=string.IsNullOrEmpty(asset)?asset:(Path.IsPathRooted(asset)?FileUtil.GetProjectRelativePath(asset):asset);Save();}
        public static string Describe()=>state.status+" · "+state.kind+" · "+state.stage+(string.IsNullOrEmpty(state.error)?"":"\n"+state.error)+(string.IsNullOrEmpty(state.asset)?"":"\n"+state.asset);
        internal static void Generate(){if(TryBegin("generate"))Advance();}
        internal static void ObserveGeneration(bool pending){state.phase=2;state.stage=pending?"compile":"complete";state.status=pending?"等待编译":"完成";Save();}
        internal static void ObserveWriteback(){if(state.kind=="generate"&&Busy)Finish();}
        internal static void Create(string script,string prefab,string ns,string name,Type args)
        {
            if(Busy)throw new InvalidOperationException("请等待当前操作完成或取消。");
            UIViewAuthoring.ValidateNames(ns,name);
            if(File.Exists(script)||File.Exists(prefab))throw new IOException("目标脚本或 Prefab 已存在；已有资产请使用已有模式。");
            UIViewAuthoring.WriteSource(script,ns,name,args);
            TryBegin("view");state.script=script;state.prefab=prefab;state.phase=0;state.status="等待编译";Save();AssetDatabase.Refresh();
        }
        internal static void Configure(string prefab,MonoScript script)
        {
            if(!TryBegin("configure"))return;
            try{UIViewAuthoring.Configure(prefab,script);state.prefab=prefab;state.phase=1;Save();Advance();}
            catch(Exception e){Record(e);}
        }
        internal static void Sample(string name,string directory,Type driver)
        {
            if(Busy)throw new InvalidOperationException("当前操作尚未完成。");
            UISceneAuthoring.ValidateDriver(driver);UISampleAuthoring.CheckSources(name,directory);
            for(int i=0;i<SceneManager.sceneCount;i++)
            {
                var scene=SceneManager.GetSceneAt(i);
                if(!scene.isDirty)continue;
                if(!EditorUtility.DisplayDialog("保存场景",scene.name+" 有未保存更改。保存后才能配置样例。","保存并继续","取消"))return;
                if(!EditorSceneManager.SaveScene(scene))return;
            }
            if(!TryBegin("sample"))return;
            state.sampleName=name;state.sampleDirectory=directory;state.asset=directory;state.driver=driver.AssemblyQualifiedName;state.scenes=EditorSceneManager.GetSceneManagerSetup();state.phase=1;Save();Advance();
        }
        internal static void Retry()
        {
            if(state.status!="失败")return;if(state.stage=="compile"&&state.phase==2)state.phase=1;state.status="进行中";state.error=null;Save();Advance();
        }
        internal static void Cancel()
        {
            if(advancing)return;state.status="已取消";state.stage="cancel";Save();
        }
        static void Update()
        {
            if(Application.isBatchMode||EditorApplication.isPlayingOrWillChangePlaymode||EditorApplication.isCompiling||EditorApplication.isUpdating||!Busy)return;
            Advance();
        }
        static void Record(Exception e){if(state.stage=="generate")state.phase=1;Fail(state.stage,state.asset,e.ToString());Debug.LogException(e);}
        static void RestoreScenes()
        {
            if(state.scenes==null||state.scenes.Length==0)return;
            // WireScene opens additively and closes its own scene; restore only if the layout changed.
            var now=EditorSceneManager.GetSceneManagerSetup();
            if(JsonUtility.ToJson(new Layout{items=now})!=JsonUtility.ToJson(new Layout{items=state.scenes}))
            {
                if(state.scenes.Length==1&&string.IsNullOrEmpty(state.scenes[0].path))EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);
                else EditorSceneManager.RestoreSceneManagerSetup(state.scenes);
            }
        }
        [Serializable] sealed class Layout{public SceneSetup[] items;}
        internal static void Advance()
        {
            if(advancing||EditorApplication.isCompiling||EditorApplication.isUpdating)return;advancing=true;
            try
            {
                if(state.kind=="view"&&state.phase==0)
                {
                    state.stage="create-prefab";var script=AssetDatabase.LoadAssetAtPath<MonoScript>(state.script);
                    if(script==null||script.GetClass()==null){state.status="等待编译";Save();return;}
                    if(!File.Exists(state.prefab))UIViewAuthoring.CreatePrefab(state.prefab);
                    UIViewAuthoring.Configure(state.prefab,script);state.phase=1;Save();
                }
                if(state.phase<=1)
                {
                    state.stage="generate";state.phase=2;Save();UICodegenPipeline.GenerateAll();state.status="等待编译";Save();return;
                }
                if(state.phase==2)
                {
                    state.stage="writeback";state.status="待写回";Save();UICodegenPipeline.Complete();UICodegenPipeline.Validate();
                    if(state.kind=="sample")
                    {
                        state.stage="usage";var file=state.sampleDirectory+"/"+UISampleAuthoring.ViewName(state.sampleName)+".Usage.cs";
                        UISampleAuthoring.CheckSources(state.sampleName,state.sampleDirectory);UISampleAuthoring.InstallUsage(file+".txt",file);
                        state.phase=3;state.status="等待编译";Save();AssetDatabase.Refresh();return;
                    }
                    Finish();return;
                }
                if(state.phase==3&&state.kind=="sample")
                {
                    state.stage="scene";Save();
                    for(int i=0;i<SceneManager.sceneCount;i++)if(SceneManager.GetSceneAt(i).isDirty)throw new InvalidOperationException("等待期间场景发生未保存修改，请保存场景后重试；不会自动丢弃更改。");
                    state.scenes=EditorSceneManager.GetSceneManagerSetup();Save();
                    try{UISampleAuthoring.WireScene(state.sampleName,state.sampleDirectory,Type.GetType(state.driver,true));}
                    finally{RestoreScenes();}
                    state.stage="validate";Save();UISampleAuthoring.ValidateOne(state.sampleName,state.sampleDirectory);Finish();
                }
            }
            catch(Exception e){Record(e);}
            finally{advancing=false;}
        }
        static void Finish(){state.status="完成";state.stage="complete";Save();}
    }
}
