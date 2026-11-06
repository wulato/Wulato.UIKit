using System;
using System.IO;
using System.Linq;
using Cysharp.Threading.Tasks;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
namespace Wulato.UIKit.Editor.Authoring
{
    [InitializeOnLoad]
    internal static class UIPreviewController
    {
        static UIPreviewState state;
        static UIPreviewRuntime runtime;
        static UIPreviewProfile preparingProfile;
        static bool stopping;
        static UIDiagnosticsSnapshot lastDiagnostics;
        public static string LastSampleUtc { get; private set; }
        public static string RecoveryError { get; private set; }
        public static string LastMessage { get; private set; }
        public static bool Active=>state!=null||File.Exists(UIPreviewOperation.StatePath);
        public static bool Running=>runtime!=null&&!stopping;
        public static bool CanCommand=>Running&&!runtime.Busy;
        public static IUISession Session=>runtime?.Session;
        public static UIDiagnosticsSnapshot Diagnostics=>runtime?.Context?.GetDiagnostics()??lastDiagnostics;
        static UIPreviewController()
        {
            try{state=UIPreviewOperation.Read();lastDiagnostics=state?.terminal?.Restore();LastSampleUtc=state?.terminal?.capturedUtc;}catch(Exception e){RecoveryError=e.Message;}
            EditorApplication.playModeStateChanged+=Changed;
            AssemblyReloadEvents.beforeAssemblyReload+=BeforeReload;
            bool expectedEntry=state!=null&&state.phase=="entering"&&SessionState.GetString("UIKit.Preview.Request","")==state.request;
            EditorApplication.update+=()=>{if(stopping&&!EditorApplication.isPlayingOrWillChangePlaymode)FinishRestore();};
            if(Active&&!expectedEntry)EditorApplication.delayCall+=()=>{
                // A freshly loaded controller cannot own an interrupted runtime run.
                if(EditorApplication.isPlayingOrWillChangePlaymode)StopAsync().Forget(Report);
                else LastMessage="存在未完成预览。请选择恢复原场景或保留现场。";
            };
        }
        public static void ValidateProfile(UIPreviewProfile profile)
        {
            if(!profile||!profile.registry)throw new ArgumentException("Choose a preview profile and Registry.");
            profile.registry.CopyValidated().Get(profile.view);
            UISceneAuthoring.ValidateDriver(profile.inputDriver?profile.inputDriver.GetClass():UISceneAuthoring.DefaultDriver());
            if(profile.rootTemplate)
            {
                if(!PrefabUtility.IsPartOfPrefabAsset(profile.rootTemplate)||profile.rootTemplate.GetComponentsInChildren<UIRoot>(true).Length!=1)throw new ArgumentException("Root template must be a Prefab with exactly one UIRoot.");
                foreach(var script in profile.rootTemplate.GetComponentsInChildren<MonoBehaviour>(true))
                    if(script==null||(!(script is UIRoot)&&script.GetType().Namespace!="UnityEngine.UI"))throw new ArgumentException("Root template may only contain UIRoot and standard Unity UI components; remove startup scripts, Host and EventSystem.");
            }
        }
        public static void Start(UIPreviewProfile profile)
        {
            if(Active||EditorApplication.isPlayingOrWillChangePlaymode||EditorApplication.isCompiling||UIAuthoringOperation.Busy)throw new InvalidOperationException("Finish the current operation before starting a preview.");
            if(PrefabStageUtility.GetCurrentPrefabStage()!=null)throw new InvalidOperationException("Exit Prefab Stage before previewing.");
            ValidateProfile(profile);UICodegenPipeline.Validate();
            var registryPath=AssetDatabase.GetAssetPath(profile.registry);
            if(string.IsNullOrEmpty(registryPath))throw new ArgumentException("Save the preview Registry as an asset first.");
            var guid=AssetDatabase.AssetPathToGUID(AssetDatabase.GetAssetPath(profile));if(string.IsNullOrEmpty(guid))throw new ArgumentException("Save the profile as an asset first.");
            if(!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()){LastMessage="已取消启动；未创建预览。";return;}
            lastDiagnostics=null;
            var scenes=EditorSceneManager.GetSceneManagerSetup();
            if(scenes.Any(s=>string.IsNullOrEmpty(s.path)))throw new InvalidOperationException("Save untitled scenes before previewing.");
            var id=Guid.NewGuid().ToString("N");state=new UIPreviewState{createdAssetRoot=!Directory.Exists(UIPreviewOperation.AssetRoot),request=id,phase="preparing",profileGuid=guid,directory=UIPreviewOperation.AssetRoot+"/"+id,scenePath=UIPreviewOperation.AssetRoot+"/"+id+"/Preview.unity",scenes=scenes,originalGuids=scenes.Select(s=>AssetDatabase.AssetPathToGUID(s.path)).ToArray(),selection=Selection.objects.Select(o=>GlobalObjectId.GetGlobalObjectIdSlow(o).ToString()).ToArray()};
            Scene preparedScene=default;preparingProfile=profile;
            try
            {
                UIPreviewOperation.Save(state);Directory.CreateDirectory(state.directory);AssetDatabase.Refresh();
                state.directoryGuid=AssetDatabase.AssetPathToGUID(state.directory);state.assetRootGuid=AssetDatabase.AssetPathToGUID(UIPreviewOperation.AssetRoot);UIPreviewOperation.Save(state);
                var scene=EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);preparedScene=scene;
                profile=AssetDatabase.LoadAssetAtPath<UIPreviewProfile>(AssetDatabase.GUIDToAssetPath(guid));preparingProfile=profile;
                var previewRegistry=AssetDatabase.LoadAssetAtPath<UIRegistry>(registryPath);
                if(!previewRegistry)throw new InvalidOperationException("Preview Registry was unloaded or removed: "+registryPath);
                UIRoot root=null;
                if(profile.rootTemplate)
                {
                    var instance=(GameObject)PrefabUtility.InstantiatePrefab(profile.rootTemplate,scene);root=instance.GetComponentInChildren<UIRoot>(true);
                }
                var driver=profile.inputDriver?profile.inputDriver.GetClass():UISceneAuthoring.DefaultDriver();
                root=UISceneAuthoring.Apply(scene,root,previewRegistry,null,driver);UISampleAuthoring.EnsurePreviewCamera(scene);
                var host=UIContextHostAuthoring.ApplyPresets(root,previewRegistry,profile.transitionPreset,profile.navigationPreset);var so=new SerializedObject(host);so.FindProperty("initializeOnStart").boolValue=false;so.ApplyModifiedPropertiesWithoutUndo();
                if(!EditorSceneManager.SaveScene(scene,state.scenePath))throw new IOException("Could not save preview scene.");
                UIPreviewOperation.CaptureFiles(state);state.phase="entering";UIPreviewOperation.Save(state);SessionState.SetString("UIKit.Preview.Request",state.request);EditorApplication.EnterPlaymode();
            }
            catch(Exception e)
            {
                state.error=e.ToString();state.phase="recovery";LastMessage=e.Message;
                try
                {
                    if(preparedScene.IsValid()&&SceneManager.GetActiveScene()==preparedScene)EditorSceneManager.RestoreSceneManagerSetup(state.scenes);
                    if(state.files==null&&Directory.Exists(state.directory)&&!Directory.EnumerateFiles(state.directory,"*",SearchOption.AllDirectories).Any())UIPreviewOperation.CaptureFiles(state);
                    UIPreviewOperation.DeleteOwned(state);UIPreviewOperation.Save(state);UIPreviewOperation.Archive();state=null;
                }
                catch(Exception cleanup){state.error+="\n"+cleanup;SaveStateSafely();}
                throw;
            }
            finally{preparingProfile=null;}
        }
        static void Changed(PlayModeStateChange mode)
        {
            if(mode==PlayModeStateChange.ExitingEditMode){lastDiagnostics=null;LastSampleUtc=null;}
            if(state==null)return;
            if(mode==PlayModeStateChange.EnteredPlayMode)
            {
                if(state.phase=="entering"&&SessionState.GetString("UIKit.Preview.Request","")==state.request)Run().Forget(Report);else StopAsync().Forget(Report);
            }
            if(mode==PlayModeStateChange.ExitingPlayMode)StopAsync().Forget(Report);
            if(mode==PlayModeStateChange.EnteredEditMode)
            {
                if(stopping||(state.phase=="stopping"&&SessionState.GetString("UIKit.Preview.Request","")==state.request))EditorApplication.delayCall+=FinishRestore;
                else if(state.phase!="preparing"){state.phase="recovery";SaveStateSafely();LastMessage="预览已中断，请恢复原场景或保留现场。";}
            }
        }
        static async UniTask Run()
        {
            try
            {
                var profile=AssetDatabase.LoadAssetAtPath<UIPreviewProfile>(AssetDatabase.GUIDToAssetPath(state.profileGuid));ValidateProfile(profile);
                var scene=SceneManager.GetActiveScene();if(scene.path!=state.scenePath)throw new InvalidOperationException("Preview scene identity changed.");
                var hosts=scene.GetRootGameObjects().SelectMany(g=>g.GetComponentsInChildren<UIContextHost>(true)).ToArray();if(hosts.Length!=1)throw new InvalidOperationException("Expected one preview Context Host.");
                runtime=new UIPreviewRuntime(profile,hosts[0]);state.phase="running";UIPreviewOperation.Save(state);await runtime.OpenAsync();LastMessage="预览已打开。";
            }
            catch(OperationCanceledException) when(stopping) { }
            catch(UIException e) when(stopping&&e.Code==UIErrorCode.ContextStopped) { }
            catch(Exception e){Report(e);await StopAsync();}
        }
        public static UniTask CloseAsync()=>runtime==null?UniTask.CompletedTask:runtime.CloseAsync();
        public static UniTask ReopenAsync()=>runtime==null?UniTask.CompletedTask:runtime.OpenAsync();
        public static async UniTask StopAsync()
        {
            if(stopping)return;
            stopping=true;
            try
            {
                if(state!=null){state.phase="stopping";SaveStateSafely();}
                if(runtime!=null)
                {
                    var result=await runtime.ShutdownAsync();lastDiagnostics=runtime.Context?.GetDiagnostics();LastMessage="Shutdown errors: "+result.Errors.Count+"; pending cleanup: "+result.PendingCleanupCount;
                    if(state!=null)state.error=(state.error??"")+"\n"+LastMessage;
                }
                else if(state!=null&&EditorApplication.isPlaying)
                {
                    var scene=SceneManager.GetActiveScene();if(scene.path==state.scenePath)
                        foreach(var host in scene.GetRootGameObjects().SelectMany(g=>g.GetComponentsInChildren<UIContextHost>(true)))await host.ShutdownAsync();
                }
            }
            catch(Exception e){Report(e);}
            finally
            {
                if(runtime?.Context!=null)lastDiagnostics=runtime.Context.GetDiagnostics();
                if(lastDiagnostics!=null)
                {
                    if(state?.terminal==null)LastSampleUtc=DateTime.UtcNow.ToString("O");
                    if(state!=null)state.terminal=UIDiagnosticsRecord.Capture(lastDiagnostics,LastSampleUtc);
                }
                UIDiagnosticsPage.Remember(lastDiagnostics,false);runtime=null;SaveStateSafely();
                if(EditorApplication.isPlayingOrWillChangePlaymode)EditorApplication.ExitPlaymode();else FinishRestore();
            }
        }
        static void BeforeReload()
        {
            if(!Active)return;
            if(state!=null&&state.phase=="entering"&&SessionState.GetString("UIKit.Preview.Request","")==state.request)return;
            if(state!=null){state.phase="recovery";SaveStateSafely();}StopAsync().Forget(Report);
        }
        static void FinishRestore()
        {
            stopping=false;
            if(EditorApplication.isPlayingOrWillChangePlaymode)return;
            try{Restore();}catch(Exception e){Report(e);}
        }
        public static void Restore()
        {
            if(EditorApplication.isPlayingOrWillChangePlaymode)throw new InvalidOperationException("Stop PlayMode before restoring.");
            if(state==null){if(Active)throw new InvalidDataException(RecoveryError);return;}
            var setup=EditorSceneManager.GetSceneManagerSetup();
            if(setup.Length!=1||setup[0].path!=state.scenePath||SceneManager.GetActiveScene().isDirty)throw new InvalidOperationException("Scene layout changed or is dirty. Keep the current scene, or restore manually; nothing was overwritten.");
            for(int i=0;i<state.scenes.Length;i++)
                if(string.IsNullOrEmpty(state.originalGuids[i])||AssetDatabase.AssetPathToGUID(state.scenes[i].path)!=state.originalGuids[i])throw new IOException("An original scene is missing or replaced: "+state.scenes[i].path);
            EditorSceneManager.RestoreSceneManagerSetup(state.scenes);
            if(state.selection!=null)Selection.objects=state.selection.Select(s=>GlobalObjectId.TryParse(s,out var id)?GlobalObjectId.GlobalObjectIdentifierToObjectSlow(id):null).Where(o=>o!=null).ToArray();
            bool deleted=UIPreviewOperation.DeleteOwned(state);LastMessage=deleted?"原场景已恢复。":"原场景已恢复；修改过的临时资产已保留："+state.directory;
            UIPreviewOperation.Archive();state=null;RecoveryError=null;
        }
        public static void Keep()
        {
            if(EditorApplication.isPlayingOrWillChangePlaymode)throw new InvalidOperationException("Stop PlayMode before keeping the scene.");
            UIPreviewOperation.Archive();state=null;RecoveryError=null;stopping=false;LastMessage="已保留现场和恢复记录。";
        }
        static void SaveStateSafely()
        {
            if(state==null)return;
            try{UIPreviewOperation.Save(state);}
            catch(Exception error){LastMessage=(LastMessage??"")+"\nPreview recovery could not be saved: "+error;}
        }
        public static void Report(Exception error)
        {
            LastMessage=error.ToString();if(state!=null){state.error=(state.error??"")+"\n"+error;SaveStateSafely();}Debug.LogException(error);
        }
    }
}
