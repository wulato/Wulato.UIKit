using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;
namespace Wulato.UIKit.Editor.Verification
{
    /// <summary>Explicit CLI entry, never invoked by ordinary package builds.</summary>
    public static class UIPlayerBuild
    {
        [Serializable] public sealed class Request { public string editor,backend,target,profile,build_id,fingerprint; }
        [Serializable] sealed class Report { public string status,output,application_id,build_id,architecture="ARM64",scripting_backend="IL2CPP";public string[] player_assemblies;public int errors; }
        static string Out=>Environment.GetEnvironmentVariable("UIKIT_EVIDENCE_DIR");
        static Request Input=>JsonUtility.FromJson<Request>(File.ReadAllText(Environment.GetEnvironmentVariable("UIKIT_BUILD_REQUEST")));
        static void OK()=>Debug.Log("UIKIT_BUILD_STAGE_OK");
        static void Sample(string name)
        {
            var type=Type.GetType("Wulato.UIKit.Samples.Editor.SampleSetup, Wulato.UIKit.Samples.Editor",true);
            type.GetMethod(name).Invoke(null,null);
        }
        [Serializable] sealed class SupportReport {public string editor=Application.unityVersion;public bool android,ios;}
        public static void CheckSupport()
        {
            var report=new SupportReport{android=BuildPipeline.IsBuildTargetSupported(BuildTargetGroup.Android,BuildTarget.Android),ios=BuildPipeline.IsBuildTargetSupported(BuildTargetGroup.iOS,BuildTarget.iOS)};
            File.WriteAllText(Path.Combine(Out,"unity-platform-support.json"),JsonUtility.ToJson(report,true));
            Debug.Log("UIKIT_PLATFORM_SUPPORT "+JsonUtility.ToJson(report));OK();
        }
        public static void PrepareSamples(){Sample("CheckAssemblies");Sample("Author");if(Input.profile=="smoke")Type.GetType("Wulato.UIKit.Acceptance.Editor.MobileSmokeSceneBuilder, Wulato.UIKit.MobileSmoke.Editor",true).GetMethod("Author").Invoke(null,null);OK();}
        public static void Generate(){Sample("Generate");OK();}
        public static void Complete(){Sample("Complete");OK();}
        public static void Wire()
        {
            Sample("FinalizeSamples");
            foreach(var name in new[]{"QuickStart","Modals","Inventory"})
            {
                var path="Assets/Samples/Wulato UIKit/"+name+"/"+(name=="QuickStart"?"Settings":name)+"Scene.unity";
                var scene=UnityEditor.SceneManagement.EditorSceneManager.OpenScene(path);
                var events=new GameObject("Host input",typeof(UnityEngine.EventSystems.EventSystem));
                var driver=Type.GetType("Wulato.UIKit.InputSystem.InputSystemUIInputDriver, Wulato.UIKit.InputSystem",false);
                if(driver!=null)events.AddComponent(driver);else events.AddComponent<LegacyUIInputDriver>();
                UnityEditor.SceneManagement.EditorSceneManager.SaveScene(scene,path);
            }
            if(Input.profile=="smoke")Type.GetType("Wulato.UIKit.Acceptance.Editor.MobileSmokeSceneBuilder, Wulato.UIKit.MobileSmoke.Editor",true).GetMethod("Create").Invoke(null,null);
            OK();
        }
        [Serializable] sealed class Package {public string name,version,source,resolvedPath;}
        [Serializable] sealed class Dependencies {public string editor;public Package[] packages;}
        public static void Validate()
        {
            Sample("Validate");UICodegenPipeline.Validate();
            var packages=UnityEditor.PackageManager.PackageInfo.GetAllRegisteredPackages().Where(p=>p.name.StartsWith("com.wulato.uikit")||p.name=="com.cysharp.unitask"||p.name=="com.unity.ugui").Select(p=>new Package{name=p.name,version=p.version,source=p.source.ToString(),resolvedPath=p.resolvedPath}).ToArray();
            foreach(var name in new[]{"com.wulato.uikit", "com.cysharp.unitask","com.unity.ugui"})
                if(!packages.Any(p=>p.name==name))throw new InvalidOperationException("Missing installed package: "+name);
            if(packages.Single(p=>p.name=="com.wulato.uikit").source!="Git"||packages.Single(p=>p.name=="com.cysharp.unitask").version!="2.5.11")throw new InvalidOperationException("Expected Git package and pinned UniTask");
            File.WriteAllText(Path.Combine(Out,"dependency-snapshot.json"),JsonUtility.ToJson(new Dependencies{editor=Application.unityVersion,packages=packages},true));OK();
        }
        public static void Build()
        {
            var input=Input;if(Application.unityVersion!=input.editor)throw new InvalidOperationException("Editor mismatch");
            UICodegenPipeline.Validate();
            var target=input.target=="Android"?BuildTarget.Android:BuildTarget.iOS;
            var group=BuildPipeline.GetBuildTargetGroup(target);
            if(!BuildPipeline.IsBuildTargetSupported(group,target))throw new InvalidOperationException("Missing build support: "+target);
            PlayerSettings.SetScriptingBackend(group,ScriptingImplementation.IL2CPP);
            PlayerSettings.SetManagedStrippingLevel(group,ManagedStrippingLevel.High);
            if(target==BuildTarget.Android)PlayerSettings.Android.targetArchitectures=AndroidArchitecture.ARM64;
            else {PlayerSettings.SetArchitecture(group,1);PlayerSettings.iOS.sdkVersion=iOSSdkVersion.DeviceSDK;}
            var app="com.wulato.uikit.acceptance."+input.backend+"."+input.profile;
            PlayerSettings.SetApplicationIdentifier(group,app);
            var scenes=input.profile=="smoke"?new[]{"Assets/UIKitMobileSmokeScene.unity"}:new[]{"Assets/Samples/Wulato UIKit/QuickStart/SettingsScene.unity"};
            var output=Path.Combine(Out,target==BuildTarget.Android?"UIKit.apk":"Xcode");
            var result=BuildPipeline.BuildPlayer(new BuildPlayerOptions{scenes=scenes,locationPathName=output,target=target,options=input.profile=="smoke"?BuildOptions.Development:BuildOptions.None});
            var assemblies=result.GetFiles().Select(f=>f.path).Where(p=>p.EndsWith(".dll",StringComparison.OrdinalIgnoreCase)).Select(Path.GetFileName).Distinct().OrderBy(n=>n).ToArray();
            // BuildReport files alone can omit IL2CPP inputs. Preserve the actual managed stripping input inventory.
            var managed="Library/Bee/artifacts";
            if(Directory.Exists(managed))assemblies=assemblies.Concat(Directory.GetFiles(managed,"*.dll",SearchOption.AllDirectories).Where(p=>p.Contains("ManagedStripped")).Select(Path.GetFileName)).Distinct().OrderBy(n=>n).ToArray();
            var report=new Report{status=result.summary.result==BuildResult.Succeeded?"PASS":"FAIL",output=output,application_id=app,build_id=input.build_id,errors=(int)result.summary.totalErrors,player_assemblies=assemblies};
            File.WriteAllText(Path.Combine(Out,"build-report.json"),JsonUtility.ToJson(report,true));
            if(input.profile=="clean" && (!assemblies.Any() || assemblies.Any(n=>new[]{"test","acceptance","verification","nunit"}.Any(s=>n.ToLowerInvariant().Contains(s)))))throw new InvalidOperationException("Clean player contains verification/test assemblies, or no assembly evidence");
            if(report.status!="PASS")throw new InvalidOperationException("Player build failed");OK();
        }
    }
}
