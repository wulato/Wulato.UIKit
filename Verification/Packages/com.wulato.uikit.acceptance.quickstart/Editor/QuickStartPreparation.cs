using System;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine.SceneManagement;
using UnityEngine;
using Wulato.UIKit.Editor.Authoring;
namespace Wulato.UIKit.Acceptance.QuickStart.Editor
{
    public static class QuickStartPreparation
    {
        static Type Operation=>typeof(UISampleAuthoring).Assembly.GetType("Wulato.UIKit.Editor.Authoring.UIAuthoringOperation",true);
        static object Call(string method,params object[] args)
        {try{return Operation.GetMethod(method,BindingFlags.Static|BindingFlags.NonPublic|BindingFlags.Public).Invoke(null,args);}catch(TargetInvocationException e){throw e.InnerException;}}
        static void Report()
        {
            var text=(string)Call("Describe");
            if(text.StartsWith("失败",StringComparison.Ordinal))throw new InvalidOperationException(text);
            File.WriteAllText(Path.Combine(Environment.GetEnvironmentVariable("UIKIT_EVIDENCE_DIR"),"quickstart-preparation.txt"),text);
        }
        public static void Begin()
        {
            foreach(var name in new[]{"Modals","Inventory"})if(AppDomain.CurrentDomain.GetAssemblies().Any(a=>a.GetName().Name=="Wulato.UIKit.Samples."+name))throw new InvalidOperationException("Unexpected sample assembly: "+name);
            var dir=UISampleAuthoring.SelectDirectory(UISampleAuthoring.FindDirectories("QuickStart"),null);
            if(dir.Split('/').Length!=5)throw new InvalidOperationException("Expected Package Manager versioned sample directory: "+dir);
#if ENABLE_INPUT_SYSTEM && !ENABLE_LEGACY_INPUT_MANAGER
            var driver=Type.GetType("Wulato.UIKit.InputSystem.InputSystemUIInputDriver, Wulato.UIKit.InputSystem",true);
#else
            var driver=typeof(LegacyUIInputDriver);
#endif
            Call("Sample","QuickStart",dir,driver);Report();
        }
        public static void Continue(){Call("Advance");Report();}
        static string OpenSample()
        {
            var dir=UISampleAuthoring.SelectDirectory(UISampleAuthoring.FindDirectories("QuickStart"),null);var path=dir+"/"+UISampleAuthoring.SceneName("QuickStart");
            EditorSceneManager.OpenScene(path);return path;
        }
        public static void BeginInOpenScene(){OpenSample();Begin();}
        public static void ContinueInOpenScene()
        {
            var path=OpenSample();Continue();
            if(SceneManager.GetActiveScene().path!=path)throw new InvalidOperationException("Sample setup closed the user's already-open scene.");
        }
        public static void ValidateInOpenScene(){OpenSample();Validate();}
        public static void Validate()
        {
            var text=(string)Call("Describe");if(!text.StartsWith("完成",StringComparison.Ordinal))throw new InvalidOperationException(text);
            var dir=UISampleAuthoring.SelectDirectory(UISampleAuthoring.FindDirectories("QuickStart"),null);UISampleAuthoring.ValidateOne("QuickStart",dir);Report();
        }
    }
}
