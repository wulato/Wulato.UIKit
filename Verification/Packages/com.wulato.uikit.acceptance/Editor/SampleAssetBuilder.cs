using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
namespace Wulato.UIKit.Acceptance.Editor
{
    public static class SampleAssetBuilder
    {
        [Serializable] sealed class Report {public string editor=Application.unityVersion;public string status="PASS";public string[] checks;}
        static readonly string[] Names={"QuickStart","Modals","Inventory"};
        static void ReportChecks(params string[] checks)=>File.WriteAllText(Path.Combine(Environment.GetEnvironmentVariable("UIKIT_EVIDENCE_DIR"),"samples-report.json"),JsonUtility.ToJson(new Report{checks=checks},true));
        public static void CheckAssemblies()
        {
            foreach(var name in Names)
                if(!AppDomain.CurrentDomain.GetAssemblies().Any(a=>a.GetName().Name=="Wulato.UIKit.Samples."+name))throw new InvalidOperationException("Sample assembly missing: "+name);
            ReportChecks("ThreeSampleAssembliesCompiled");
        }
        static void Call(string method)
        {
            var type=Type.GetType("Wulato.UIKit.Samples.Editor.SampleSetup, Wulato.UIKit.Samples.Editor",true);
            type.GetMethod(method).Invoke(null,null);
            if(Directory.Exists("Assets/Samples/Wulato UIKit/BusinessFlow")&&(method=="Author"||method=="Validate"))
            {
                var flow=Type.GetType("Wulato.UIKit.Samples.BusinessFlow.Editor.BusinessFlowSetup, Wulato.UIKit.Samples.BusinessFlow.Editor",true);
                flow.GetMethod(method).Invoke(null,null);
            }
            ReportChecks(method);
        }
        public static void Author()=>Call("Author");
        public static void Generate()=>Call("Generate");
        public static void Complete()=>Call("Complete");
        public static void FinalizeSamples()
        {
            Call("FinalizeSamples");
            // Consumer host owns backend choice. Serialized test scenes need their driver present
            // before synchronous LoadSceneInPlayMode runs MonoBehaviour.Start callbacks.
            foreach(var name in Names)
            {
                var path="Assets/Samples/Wulato UIKit/"+name+"/"+(name=="QuickStart"?"Settings":name)+"Scene.unity";
                var scene=UnityEditor.SceneManagement.EditorSceneManager.OpenScene(path);
                var events=scene.GetRootGameObjects().FirstOrDefault(g=>g.GetComponent<UnityEngine.EventSystems.EventSystem>()!=null);
                if(events==null)
                {
                    events=new GameObject("Host-selected input",typeof(UnityEngine.EventSystems.EventSystem));
                    var input=Type.GetType("Wulato.UIKit.InputSystem.InputSystemUIInputDriver, Wulato.UIKit.InputSystem",false);
                    if(input!=null)events.AddComponent(input);else events.AddComponent<LegacyUIInputDriver>();
                    UnityEditor.SceneManagement.EditorSceneManager.SaveScene(scene,path);
                }
            }
            UnityEditor.SceneManagement.EditorSceneManager.NewScene(UnityEditor.SceneManagement.NewSceneSetup.EmptyScene,UnityEditor.SceneManagement.NewSceneMode.Single);
            ReportChecks("ThreeScenesWithHostSelectedInput");
        }
        public static void Validate()=>Call("Validate");
    }
}
