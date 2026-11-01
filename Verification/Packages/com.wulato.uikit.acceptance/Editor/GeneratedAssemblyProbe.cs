using System;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using Wulato.UIKit.Editor;
namespace Wulato.UIKit.Acceptance.Editor
{
    public static class GeneratedAssemblyProbe
    {
        public const string Folder="Assets/UIKitCodegenFixture";
        [Serializable] sealed class Report {public string editor=Application.unityVersion; public string status="PASS";public string[] checks;}
        static void ReportChecks(params string[] checks)
        {
            File.WriteAllText(Path.Combine(Environment.GetEnvironmentVariable("UIKIT_EVIDENCE_DIR"),"codegen-report.json"),JsonUtility.ToJson(new Report{checks=checks},true));
            Debug.Log("UIKIT_CODEGEN_OK");
        }
        static MonoScript Script(string name)=>AssetDatabase.FindAssets(name+" t:MonoScript",new[]{Folder}).Select(AssetDatabase.GUIDToAssetPath).Where(p=>Path.GetFileName(p)==name+".cs").Select(AssetDatabase.LoadAssetAtPath<MonoScript>).Single();
        public static void Prepare()
        {
            foreach(var name in new[]{"ViewA","ViewB","DefaultView"})
            {
                var path=Folder+"/"+name+".prefab";if(File.Exists(path))continue;
                var go=new GameObject(name,typeof(RectTransform));
                try
                {
                    var marker=go.AddComponent<UIBindingMarker>();marker.viewScript=Script(name);marker.typeKey="generated-"+name;
                    marker.layer=name=="ViewB"?UILayer.Modal:UILayer.Page;marker.closePolicy=name=="ViewA"?UIClosePolicy.CacheOne:UIClosePolicy.Release;
                    var label=new GameObject("Unconstrained label name",typeof(RectTransform));label.transform.SetParent(go.transform,false);
                    marker.bindings=new[]{new UIBindingMarker.Binding{fieldName="label",target=label.transform}};
                    PrefabUtility.SaveAsPrefabAsset(go,path);
                }
                finally {UnityEngine.Object.DestroyImmediate(go);}
            }
            // Exercise a real inherited Prefab Variant, with an excluded authoring base.
            var basePath=Folder+"/ViewA.Base.prefab";var variantPath=Folder+"/ViewA.prefab";
            if(!File.Exists(basePath))
            {
                var error=AssetDatabase.MoveAsset(variantPath,basePath);if(error!="")throw new InvalidOperationException(error);
                var baseRoot=PrefabUtility.LoadPrefabContents(basePath);
                try {baseRoot.GetComponent<UIBindingMarker>().includeInRegistry=false;PrefabUtility.SaveAsPrefabAsset(baseRoot,basePath);}
                finally {PrefabUtility.UnloadPrefabContents(baseRoot);}
                var instance=(GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(basePath));
                try
                {
                    instance.GetComponent<UIBindingMarker>().includeInRegistry=true;
                    instance.transform.GetChild(0).name="Renamed Variant Label";
                    PrefabUtility.RecordPrefabInstancePropertyModifications(instance.GetComponent<UIBindingMarker>());
                    PrefabUtility.SaveAsPrefabAsset(instance,variantPath);
                }
                finally {UnityEngine.Object.DestroyImmediate(instance);}
            }
            ReportChecks("FixtureBaseCompilation");
        }
        public static void Generate(){UICodegenPipeline.Generate();ReportChecks("GenerationRequested");}
        public static void Complete(){UICodegenPipeline.Complete();UICodegenPipeline.Validate();ReportChecks("BindingsPartial_CompilesInOriginalAssembly","SerializedBindings_NonNull");}
        public static void Verify()
        {
            UICodegenPipeline.Validate();
            foreach(var name in new[]{"ViewA","ViewB","DefaultView"})
            {
                var type=Script(name).GetClass();var route=UIBindingAssemblyRouter.Resolve(AssetDatabase.GetAssetPath(Script(name)));
                if(type.Assembly.GetName().Name!=route.Assembly)throw new InvalidOperationException("Original assembly mismatch: "+name);
                var prefab=AssetDatabase.LoadAssetAtPath<GameObject>(Folder+"/"+name+".prefab");var staging=new GameObject("inactive");staging.SetActive(false);
                try {var instance=UnityEngine.Object.Instantiate(prefab,staging.transform);var view=instance.GetComponent(type);
                    if(!(bool)type.GetMethod("BindingReady").Invoke(view,null))throw new InvalidOperationException("Binding usage failed: "+name);
                } finally {UnityEngine.Object.DestroyImmediate(staging);}
            }
            var variant=AssetDatabase.LoadAssetAtPath<GameObject>(Folder+"/ViewA.prefab");
            if(PrefabUtility.GetPrefabAssetType(variant)!=PrefabAssetType.Variant || variant.GetComponent<UIBindingMarker>().bindings[0].target.name!="Renamed Variant Label")throw new InvalidOperationException("Variant/rename binding fixture was not retained.");
            var registry=AssetDatabase.LoadAssetAtPath<UIRegistry>(UICodegenPipeline.RegistryPath);
            var entries=registry.CopyValidated().Entries;
            if(entries.Count(e=>e.Key.Value.StartsWith("generated-",StringComparison.Ordinal))!=3)throw new InvalidOperationException("Expected three mixed-assembly providers.");
            var serialized=new SerializedObject(registry);if(serialized.FindProperty("providers").arraySize!=entries.Select(e=>e.ViewType.Assembly).Distinct().Count())throw new InvalidOperationException("Missing provider assets.");
            var marker=AssetDatabase.LoadAssetAtPath<GameObject>(Folder+"/ViewA.prefab").GetComponent<UIBindingMarker>();var old=marker.typeKey;
            try {marker.typeKey="stale";ExpectRejected(UICodegenPipeline.Validate,"StaleFingerprint_PreventsBuild");}finally{marker.typeKey=old;}
            var wrong=UICodegenPipeline.Root+"/Assembly.asmref";
            try {File.WriteAllText(wrong,"{\"reference\":\"Assembly-CSharp\"}");ExpectRejected(UICodegenPipeline.Validate,"WrongAssemblyRoute_PreventsBuild");}finally{File.Delete(wrong);}
            UICodegenPipeline.Validate();
            ReportChecks("RenameAndVariant_PreserveBindings","BindingsPartial_CompilesInOriginalAssembly","BindingUsageProbe_CompilesAndRuns","MixedAssemblyProviders_PopulateRegistryWithoutReverseReferences","StaleFingerprint_PreventsBuild","WrongAssemblyRoute_PreventsBuild");
        }
        static void ExpectRejected(Action action,string name){try{action();}catch(InvalidOperationException){return;}throw new InvalidOperationException("Missing rejection: "+name);}
    }
}
