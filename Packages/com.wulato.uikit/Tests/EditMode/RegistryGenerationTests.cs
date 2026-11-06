using System;
using System.IO;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.Compilation;
using UnityEngine;
namespace Wulato.UIKit.Tests.Editor
{
    [Category("editor")]
    public class RegistryGenerationTests
    {
        const string Root="Assets/Wulato.UIKit.Generated";
        static Type Pipeline=>AppDomain.CurrentDomain.GetAssemblies().Select(a=>a.GetType("Wulato.UIKit.Editor.UICodegenPipeline")).First(t=>t!=null);
        static void Validate(){try{Pipeline.GetMethod("Validate").Invoke(null,null);}catch(TargetInvocationException e){throw e.InnerException;}}
        [Test] public void UnchangedGeneration_CompletesWithoutAnotherDomainReload()
        {
            try
            {
                Pipeline.GetMethod("Generate").Invoke(null,null);
                Assert.That(File.Exists("Library/Wulato.UIKit.codegen.pending.json"),Is.False,"Unchanged code cannot trigger a compilation reload to finish writeback.");
                Validate();
            }
            finally {Pipeline.GetMethod("Complete").Invoke(null,null);}
        }
        [Test] public void BindingsPartial_CompilesInOriginalAssembly()
        {
            Validate();
            var manifest=JsonUtility.FromJson<Manifest>(File.ReadAllText(Root+"/manifest.json"));
            foreach(var prefab in manifest.prefabs)
            {
                var marker=AssetDatabase.LoadAssetAtPath<GameObject>(prefab).GetComponent("UIBindingMarker");
                var script=(MonoScript)new SerializedObject(marker).FindProperty("viewScript").objectReferenceValue;
                var guid=AssetDatabase.AssetPathToGUID(AssetDatabase.GetAssetPath(script));
                var generated=manifest.files.Single(f=>f.path.EndsWith(guid+".Bindings.cs")).path;
                Assert.That(CompilationPipeline.GetAssemblyNameFromScriptPath(generated),Is.EqualTo(CompilationPipeline.GetAssemblyNameFromScriptPath(AssetDatabase.GetAssetPath(script))));
                var root=AssetDatabase.LoadAssetAtPath<GameObject>(prefab);var view=root.GetComponent(script.GetClass());
                var authoredBindings=new SerializedObject(marker).FindProperty("bindings");
                for(int i=0;i<authoredBindings.arraySize;i++)
                {
                    var binding=authoredBindings.GetArrayElementAtIndex(i);var field=binding.FindPropertyRelative("fieldName").stringValue;
                    Assert.That(new SerializedObject(view).FindProperty(field).objectReferenceValue,Is.EqualTo(binding.FindPropertyRelative("target").objectReferenceValue),prefab+" / "+field);
                }
            }
            CollectionAssert.AreEquivalent(new[]{"ViewA","ViewB","DefaultView","TemplatePage","TemplateModal","TemplateDialog"},
                manifest.prefabs.Where(p=>p.StartsWith("Assets/UIKitCodegenFixture/",StringComparison.Ordinal)).Select(Path.GetFileNameWithoutExtension),"Every original and Workbench template fixture must be generated exactly once.");
        }
        [Serializable] class FileEntry { public string path; }
        [Serializable] class Manifest { public string[] prefabs;public FileEntry[] files; }
        [Test] public void MixedAssemblyProviders_PopulateRegistryWithoutReverseReferences()
        {
            Validate();var registry=AssetDatabase.LoadAssetAtPath<UIRegistry>(Root+"/UIRegistry.asset");var entries=registry.CopyValidated().Entries;
            var fixtureEntries=entries.Where(e=>e.Key.Value.StartsWith("generated-",StringComparison.Ordinal)).ToArray();
            Assert.That(fixtureEntries.Length,Is.EqualTo(3));Assert.That(fixtureEntries.Select(e=>e.ViewType.Assembly).Distinct().Count(),Is.EqualTo(3));
            var providers=new SerializedObject(registry).FindProperty("providers");Assert.That(providers.arraySize,Is.EqualTo(entries.Select(e=>e.ViewType.Assembly).Distinct().Count()));
            for(int i=0;i<providers.arraySize;i++)
            {
                var provider=providers.GetArrayElementAtIndex(i).objectReferenceValue;
                var referenceNames=provider.GetType().Assembly.GetReferencedAssemblies().Select(a=>a.Name).ToArray();
                if(provider.GetType().Assembly.GetName().Name!="Assembly-CSharp")Assert.That(referenceNames,Does.Not.Contain("Assembly-CSharp"));
            }
        }
        [Test] public void ActualStaleFingerprint_PreventsBuild()
        {
            const string path="Assets/UIKitCodegenFixture/ViewA.prefab";var original=File.ReadAllBytes(path);
            var prefab=AssetDatabase.LoadAssetAtPath<GameObject>(path);
            var marker=prefab.GetComponent("UIBindingMarker");var so=new SerializedObject(marker);var key=so.FindProperty("typeKey");var old=key.stringValue;
            try{key.stringValue="mutated";so.ApplyModifiedPropertiesWithoutUndo();Assert.That(Assert.Throws<InvalidOperationException>(Validate).Message,Does.Contain("Stale"));}
            finally{AssetDatabase.SaveAssets();File.WriteAllBytes(path,original);AssetDatabase.ImportAsset(path,ImportAssetOptions.ForceUpdate);}
        }
        [Test] public void WrongAssemblyRoute_PreventsBuild()
        {
            var path=Root+"/Assembly.asmref";
            try{File.WriteAllText(path,"{\"reference\":\"Assembly-CSharp\"}");Assert.That(Assert.Throws<InvalidOperationException>(Validate).Message,Does.Contain("Wrong assembly route"));}
            finally{File.Delete(path);}
        }
    }
}
