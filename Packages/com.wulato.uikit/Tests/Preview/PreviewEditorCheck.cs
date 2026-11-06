using System;
using System.IO;
using System.Linq;
using System.Reflection;
using Cysharp.Threading.Tasks;
using UnityEditor;
using UnityEditor.Compilation;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
namespace Wulato.UIKit.Tests.Preview
{
    // Independent Editor orchestration through the existing play-reset executor.
    [InitializeOnLoad]
    public static class PreviewEditorCheck
    {
        const string Key="UIKit.PreviewCheck.",Folder="Assets/UIKitPreviewEditorCheck";
        static Type Controller=>typeof(Wulato.UIKit.Editor.UICodegenPipeline).Assembly.GetType("Wulato.UIKit.Editor.Authoring.UIPreviewController",true);
        static Type Profile=>typeof(Wulato.UIKit.Editor.UICodegenPipeline).Assembly.GetType("Wulato.UIKit.Editor.Authoring.UIPreviewProfile",true);
        [Serializable] sealed class Layout {public SceneSetup[] scenes;}
        [Serializable] sealed class Evidence {public int schema=1,completed,activeContexts,oldLocks,subscriptions;public bool domainReload,sceneReload;public string status,scope="preview";}
        static PreviewEditorCheck()
        {
            EditorApplication.update+=Tick;
            CompilationPipeline.compilationStarted+=_=>{if(SessionState.GetBool(Key+"active",false))SessionState.SetBool(Key+"compileStarted",true);};
            AssemblyReloadEvents.afterAssemblyReload+=()=>{if(SessionState.GetBool(Key+"active",false))SessionState.SetBool(Key+"compiledReload",true);};
        }
        static object Get(string name)=>Controller.GetProperty(name,BindingFlags.Static|BindingFlags.Public).GetValue(null);
        static object Call(string name,params object[] args)
        {
            try{return Controller.GetMethod(name,BindingFlags.Static|BindingFlags.Public).Invoke(null,args);}catch(TargetInvocationException e){throw e.InnerException;}
        }
        public static void Run()
        {
            if((bool)Get("Active")||AssetDatabase.IsValidFolder(Folder))throw new InvalidOperationException("Existing preview or fixture; preserve it before retrying.");
            SessionState.SetString(Key+"layout",JsonUtility.ToJson(new Layout{scenes=EditorSceneManager.GetSceneManagerSetup()}));
            SessionState.SetBool(Key+"enabled",EditorSettings.enterPlayModeOptionsEnabled);SessionState.SetInt(Key+"options",(int)EditorSettings.enterPlayModeOptions);
            SessionState.SetInt(Key+"option",int.Parse(Environment.GetEnvironmentVariable("UIKIT_RESET_OPTIONS")));
            SessionState.SetString(Key+"out",Environment.GetEnvironmentVariable("UIKIT_EVIDENCE_DIR"));SessionState.SetInt(Key+"cycle",0);SessionState.SetString(Key+"action",Environment.GetEnvironmentVariable("UIKIT_PREVIEW_ACTION")??"window");
            AssetDatabase.CreateFolder("Assets","UIKitPreviewEditorCheck");
            var scene=EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);EditorSceneManager.SaveScene(scene,Folder+"/Original.unity");
            var registry=AssetDatabase.LoadAssetAtPath<UIRegistry>(Wulato.UIKit.Editor.UICodegenPipeline.RegistryPath);
            var entry=registry.CopyValidated().Entries.First(e=>e.ArgumentType==null);
            var profile=ScriptableObject.CreateInstance(Profile);Profile.GetField("registry").SetValue(profile,registry);Profile.GetField("view").SetValue(profile,entry.Key);
#if ENABLE_INPUT_SYSTEM && !ENABLE_LEGACY_INPUT_MANAGER
            var driver=Type.GetType("Wulato.UIKit.InputSystem.InputSystemUIInputDriver, Wulato.UIKit.InputSystem",true);
            var script=AssetDatabase.FindAssets("InputSystemUIInputDriver t:MonoScript").Select(g=>AssetDatabase.LoadAssetAtPath<MonoScript>(AssetDatabase.GUIDToAssetPath(g))).First(s=>s.GetClass()==driver);
            Profile.GetField("inputDriver").SetValue(profile,script);
#endif
            AssetDatabase.CreateAsset(profile,Folder+"/Profile.asset");
            EditorSettings.enterPlayModeOptionsEnabled=true;EditorSettings.enterPlayModeOptions=(EnterPlayModeOptions)SessionState.GetInt(Key+"option",0);
            SessionState.SetBool(Key+"active",true);
            if(SessionState.GetString(Key+"action","")=="cancel")Phase("save-cancel");else Begin();
        }
        static void CheckSaveCancellation()
        {
            var profile=AssetDatabase.LoadAssetAtPath<ScriptableObject>(Folder+"/Profile.asset");
            var dirty=new GameObject("UnsavedPreviewCancellationProbe");EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
            Call("Start",profile);
            if(EditorApplication.isPlayingOrWillChangePlaymode||(bool)Get("Active")||!dirty||!SceneManager.GetActiveScene().isDirty)throw new Exception("Save cancellation did not preserve the dirty original scene.");
            File.WriteAllText(Path.Combine(SessionState.GetString(Key+"out",""),"save-cancel.json"),"{\"status\":\"PASS\",\"dirtyScenePreserved\":true,\"playModeEntered\":false,\"previewCreated\":false}");
            UnityEngine.Object.DestroyImmediate(dirty);EditorSceneManager.SaveScene(SceneManager.GetActiveScene());Begin();
        }

        static void Phase(string value)
        {
            SessionState.SetString(Key+"phase",value);SessionState.SetFloat(Key+"deadline",(float)EditorApplication.timeSinceStartup+90);
            File.AppendAllText(Path.Combine(SessionState.GetString(Key+"out",""),"preview-progress.log"),SessionState.GetInt(Key+"cycle",0)+":"+value+"\n");
        }
        static void Begin()
        {
            Phase("opening");
            if(SessionState.GetInt(Key+"cycle",0)==1&&SessionState.GetString(Key+"action","")=="window")EditorWindow.GetWindow(Controller.Assembly.GetType("Wulato.UIKit.Editor.Authoring.UIAuthoringWorkbench",true));
            Call("Start",AssetDatabase.LoadAssetAtPath<ScriptableObject>(Folder+"/Profile.asset"));
        }
        static void Tick()
        {
            if(!SessionState.GetBool(Key+"active",false))return;
            try
            {
                var phase=SessionState.GetString(Key+"phase","");
                if(EditorApplication.timeSinceStartup>SessionState.GetFloat(Key+"deadline",float.MaxValue))throw new TimeoutException("Preview stage timed out: "+phase+"; "+Get("LastMessage"));
                if(phase=="save-cancel")
                {
                    if(EditorApplication.isUpdating||EditorApplication.isCompiling)return;
                    CheckSaveCancellation();return;
                }
                var session=Get("Session") as IUISession;
                if(EditorApplication.isPlaying)
                {
                    if(phase=="opening"&&session?.State==UIState.Open)
                    {
                        var snapshot=(UIDiagnosticsSnapshot)Get("Diagnostics");
                        if(snapshot==null||!snapshot.Sessions.Any(s=>s.Id.Equals(session.Id)))throw new Exception("Preview diagnostics did not capture the active Session.");
                        SessionState.SetString(Key+"context",snapshot.ContextId.ToString());
                        SessionState.SetString(Key+"session",session.Id.ToString());Phase("closing");((UniTask)Call("CloseAsync")).Forget(Fail);
                    }
                    else if(phase=="closing"&&session?.State==UIState.Closed)
                    {Phase("reopening");((UniTask)Call("ReopenAsync")).Forget(Fail);}
                    else if(phase=="reopening"&&session?.State==UIState.Open)
                    {
                        if(session.Id.ToString()==SessionState.GetString(Key+"session",""))throw new Exception("Preview reused a Session.");
                        Phase("restoring");
                        if(SessionState.GetInt(Key+"cycle",0)==1&&SessionState.GetString(Key+"action","")=="compile")
                        {
                            SessionState.SetBool(Key+"compileStarted",false);SessionState.SetBool(Key+"compiledReload",false);
                            File.AppendAllText(Path.Combine(SessionState.GetString(Key+"out",""),"preview-progress.log"),"compile-requested\n");CompilationPipeline.RequestScriptCompilation();
                        }
                        else if(SessionState.GetInt(Key+"cycle",0)==1)EditorWindow.GetWindow(Controller.Assembly.GetType("Wulato.UIKit.Editor.Authoring.UIAuthoringWorkbench",true)).Close();
                        else ((UniTask)Call("StopAsync")).Forget(Fail);
                    }
                }
                else if(phase=="restoring"&&!EditorApplication.isPlayingOrWillChangePlaymode&&!(bool)Get("Active"))
                {
                    if(SessionState.GetInt(Key+"cycle",0)==1&&SessionState.GetString(Key+"action","")=="compile"&&(!SessionState.GetBool(Key+"compileStarted",false)||!SessionState.GetBool(Key+"compiledReload",false)||EditorApplication.isCompiling))return;
                    if(SceneManager.GetActiveScene().path!=Folder+"/Original.unity")throw new Exception("Original scene was not restored.");
                    var facade=typeof(UIContextBuilder).Assembly.GetType("Wulato.UIKit.UIContextFacade",true);
                    if((int)facade.GetProperty("ActiveCount",BindingFlags.Static|BindingFlags.NonPublic).GetValue(null)!=0)throw new Exception("Context leaked after preview stop.");
                    if(UnityEngine.Object.FindObjectsOfType<UIInputGate>().Length!=0)throw new Exception("Input gate leaked after preview stop.");
                    var terminal=(UIDiagnosticsSnapshot)Get("Diagnostics");
                    if(SessionState.GetString(Key+"action","")!="compile"&&(terminal==null||terminal.State!=UIContextState.Stopped||terminal.ContextId.ToString()!=SessionState.GetString(Key+"context","")||terminal.InputLockCount!=0))throw new Exception("Terminal value snapshot was not preserved across preview exit/reload.");
                    var cycle=SessionState.GetInt(Key+"cycle",0);File.WriteAllText(Path.Combine(SessionState.GetString(Key+"out",""),"automatic-exit-"+cycle+".json"),"{\"status\":\"PASS\",\"activeContexts\":0,\"oldLocks\":0,\"subscriptions\":0,\"sceneRestored\":true,\"sessionChanged\":true}");
                    SessionState.SetInt(Key+"cycle",cycle+1);if(cycle==0)Begin();else Finish();
                }
            }catch(Exception e){Fail(e);}
        }
        static void Finish()
        {
            int option=SessionState.GetInt(Key+"option",0);var evidence=new Evidence{completed=2,domainReload=(option&1)==0,sceneReload=(option&2)==0,status="PASS"};
            File.WriteAllText(Path.Combine(SessionState.GetString(Key+"out",""),"play-reset.json"),JsonUtility.ToJson(evidence,true));Restore();EditorApplication.Exit(0);
        }
        static void Restore()
        {
            SessionState.SetBool(Key+"active",false);EditorSettings.enterPlayModeOptionsEnabled=SessionState.GetBool(Key+"enabled",false);EditorSettings.enterPlayModeOptions=(EnterPlayModeOptions)SessionState.GetInt(Key+"options",0);
            var setup=JsonUtility.FromJson<Layout>(SessionState.GetString(Key+"layout","")).scenes;
            if(setup.Any(s=>!string.IsNullOrEmpty(s.path)))EditorSceneManager.RestoreSceneManagerSetup(setup);else EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);
            AssetDatabase.DeleteAsset(Folder);
        }
        static void Fail(Exception e){Debug.LogException(e);SessionState.SetBool(Key+"active",false);EditorSettings.enterPlayModeOptionsEnabled=SessionState.GetBool(Key+"enabled",false);EditorSettings.enterPlayModeOptions=(EnterPlayModeOptions)SessionState.GetInt(Key+"options",0);EditorApplication.Exit(1);}
    }
}
