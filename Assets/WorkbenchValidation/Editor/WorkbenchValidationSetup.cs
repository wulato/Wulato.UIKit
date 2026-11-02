using System;
using System.Linq;
using System.IO;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using Wulato.UIKit;
using Cysharp.Threading.Tasks;
using UnityEditor.SceneManagement;
using UnityEditor;
using UnityEditor.PackageManager.UI;
using UnityEngine;

// Manual validation setup only. Workbench actions themselves are exercised through its UI.
public static class WorkbenchValidationSetup
{
    [MenuItem("Tools/Wulato/UIKit/Validation/Import QuickStart")]
    public static void ImportQuickStart()
    {
        var sample=Sample.FindByPackage("com.wulato.uikit","0.3.0").Single(s=>s.displayName=="QuickStart");
        if(sample.isImported)throw new InvalidOperationException("QuickStart is already imported; refusing overwrite: "+sample.importPath);
        if(!sample.Import(Sample.ImportOptions.None))throw new InvalidOperationException("QuickStart import failed.");
        Debug.Log("Workbench validation imported QuickStart using Unity Sample.Import: "+sample.importPath);
    }

    [Serializable] sealed class Snapshot
    {
        public bool isPlaying;
        public string scene,contextState,contextId,selected;
        public string[] sessionIds;
        public int sessions=-1,activeLeases=-1,cameras;
        public string[] roots,views;
    }
    [MenuItem("Tools/Wulato/UIKit/Validation/Capture observed state")]
    public static void CaptureObservedState()
    {
        var scene=SceneManager.GetActiveScene();
        var snapshot=new Snapshot{isPlaying=EditorApplication.isPlaying,scene=scene.path,roots=scene.GetRootGameObjects().Select(g=>g.name).ToArray(),
            views=UnityEngine.Object.FindObjectsOfType<UIViewBase>().Select(v=>v.GetType().FullName).ToArray(),cameras=Camera.allCamerasCount,
            selected=EventSystem.current!=null&&EventSystem.current.currentSelectedGameObject!=null?EventSystem.current.currentSelectedGameObject.name:null};
        var demo=UnityEngine.Object.FindObjectsOfType<MonoBehaviour>().FirstOrDefault(b=>b.GetType().FullName=="Wulato.UIKit.Samples.QuickStart.QuickStartDemo");
        var ui=demo==null?null:demo.GetType().GetProperty("Context").GetValue(demo) as IUIContext;
        if(ui!=null){var d=ui.GetDiagnostics();snapshot.sessions=d.Sessions.Count;snapshot.activeLeases=d.ActiveLeaseCount;snapshot.contextState=d.State.ToString();snapshot.contextId=d.ContextId.ToString();snapshot.sessionIds=d.Sessions.Select(x=>x.Id.ToString()).ToArray();}
        var folder="Artifacts/uikit/authoring-interactive";Directory.CreateDirectory(folder);var stamp=DateTime.UtcNow.ToString("yyyyMMddTHHmmssfff");
        File.WriteAllText(folder+"/observed-"+stamp+".json",JsonUtility.ToJson(snapshot,true));
        if(EditorApplication.isPlaying)ScreenCapture.CaptureScreenshot(folder+"/game-"+stamp+".png");
        Debug.Log("Workbench observed state: "+JsonUtility.ToJson(snapshot));
    }

    [MenuItem("Tools/Wulato/UIKit/Validation/Invoke Demo Open UI")]
    public static async void InvokeDemoOpenUI()
    {
        try
        {
            if(!EditorApplication.isPlaying)throw new InvalidOperationException("Enter PlayMode first.");
            var demo=UnityEngine.Object.FindObjectsOfType<MonoBehaviour>().Single(b=>b.GetType().FullName=="Wulato.UIKit.Samples.QuickStart.QuickStartDemo");
            await (UniTask)demo.GetType().GetMethod("OpenAsync").Invoke(demo,null);
            CaptureObservedState();
        }
        catch(Exception e){Debug.LogException(e);}
    }
    [MenuItem("Tools/Wulato/UIKit/Validation/Create empty validation scene")]
    public static void CreateEmptyValidationScene()
    {
        if(EditorApplication.isPlaying)throw new InvalidOperationException("Exit PlayMode first.");
        const string path="Assets/WorkbenchValidation/SceneWizardValidation.unity";
        if(File.Exists(path))throw new InvalidOperationException("Refusing to overwrite existing validation scene.");
        if(!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())return;
        var scene=EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);
        if(!EditorSceneManager.SaveScene(scene,path))throw new IOException(path);
        CaptureObservedState();
    }
}
