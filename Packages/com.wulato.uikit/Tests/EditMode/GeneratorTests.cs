using System;
using System.IO;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using Wulato.UIKit.Tests.Support;
namespace Wulato.UIKit.Tests.Editor
{
    [Category("editor")]
    public class GeneratorTests
    {
        const string Folder="Assets/UIKitGeneratorTests";
        protected string Prefab => Folder+"/Probe.prefab";
        protected Type Tool(string name)
        {
            var type=AppDomain.CurrentDomain.GetAssemblies().Select(a=>a.GetType("Wulato.UIKit.Editor."+name)).FirstOrDefault(t=>t!=null);
            Assert.That(type,Is.Not.Null,"T9 Editor tool is missing: "+name);return type;
        }
        protected object Call(string method,params object[] args)
        {
            try {return Tool("UICodegenPipeline").GetMethod(method,BindingFlags.Static|BindingFlags.Public).Invoke(null,args);}
            catch(TargetInvocationException e){throw e.InnerException;}
        }
        [SetUp] public void Setup(){AssetDatabase.CreateFolder("Assets","UIKitGeneratorTests");}
        [TearDown] public void Cleanup(){AssetDatabase.DeleteAsset(Folder);}
        protected void CreatePrefab()
        {
            var marker=AppDomain.CurrentDomain.GetAssemblies().Select(a=>a.GetType("Wulato.UIKit.UIBindingMarker")).FirstOrDefault(t=>t!=null);
            Assert.That(marker,Is.Not.Null,"Runtime marker is missing");
            var go=new GameObject("Probe",typeof(RectTransform));
            try {
                var view=go.AddComponent<CodegenTestView>();var mark=go.AddComponent(marker);
                var child=new GameObject("Label",typeof(RectTransform));child.transform.SetParent(go.transform,false);
                var so=new SerializedObject(mark);so.FindProperty("viewScript").objectReferenceValue=MonoScript.FromMonoBehaviour(view);
                var bindings=so.FindProperty("bindings");bindings.arraySize=1;
                bindings.GetArrayElementAtIndex(0).FindPropertyRelative("fieldName").stringValue="label";
                bindings.GetArrayElementAtIndex(0).FindPropertyRelative("target").objectReferenceValue=child.transform;
                so.ApplyModifiedPropertiesWithoutUndo();PrefabUtility.SaveAsPrefabAsset(go,Prefab);
            } finally {UnityEngine.Object.DestroyImmediate(go);}
        }
        [Test] public void UnselectedPrefabAndEditorOnlyView_AreNotRuntimeRegistrations()
        {
            CreatePrefab();var root=AssetDatabase.LoadAssetAtPath<GameObject>(Prefab);var marker=root.GetComponent("UIBindingMarker");var so=new SerializedObject(marker);
            var include=so.FindProperty("includeInRegistry");Assert.That(include,Is.Not.Null,"Prefab variants require an explicit registration choice.");
            include.boolValue=false;so.ApplyModifiedPropertiesWithoutUndo();
            var paths=(string[])Tool("UICodegenPipeline").GetMethod("Discover",BindingFlags.Static|BindingFlags.NonPublic).Invoke(null,null);
            Assert.That(paths,Does.Not.Contain(Prefab));
            so.Update();so.FindProperty("includeInRegistry").boolValue=true;
            var editorScript=AssetDatabase.LoadAssetAtPath<MonoScript>("Packages/com.wulato.uikit/Tests/EditMode/EditorOnlyCodegenView.cs");
            Assert.That(editorScript,Is.Not.Null);Assert.That(editorScript.GetClass(),Is.EqualTo(typeof(EditorOnlyCodegenView)));
            so.FindProperty("viewScript").objectReferenceValue=editorScript;so.ApplyModifiedPropertiesWithoutUndo();
            paths=(string[])Tool("UICodegenPipeline").GetMethod("Discover",BindingFlags.Static|BindingFlags.NonPublic).Invoke(null,null);Assert.That(paths,Does.Not.Contain(Prefab));
        }
        [Test] public void ExternalRegistration_GeneratesDescriptorWithoutPrefabField()
        {
            CreatePrefab();var asset=AssetDatabase.LoadAssetAtPath<GameObject>(Prefab);var so=new SerializedObject(asset.GetComponent("UIBindingMarker"));
            Assert.That(so.FindProperty("externalProviderId"),Is.Not.Null,"External marker fields missing");
            so.FindProperty("externalProviderId").stringValue="addressables";so.FindProperty("externalAssetId").stringValue="UI/Probe";so.ApplyModifiedPropertiesWithoutUndo();
            var text=(string)Call("Preview",new object[]{new[]{Prefab}});
            Assert.That(text,Does.Contain("UIViewAsset.External(\"addressables\",\"UI/Probe\")"));
            Assert.That(text,Does.Not.Contain("GameObject prefab"));Assert.That(text,Does.Contain("RectTransform"));
            so.FindProperty("externalAssetId").stringValue="UI/Changed";so.ApplyModifiedPropertiesWithoutUndo();
            Assert.That(Call("Preview",new object[]{new[]{Prefab}}),Is.Not.EqualTo(text));
        }
        [Test] public void ExternalRenameAndVariant_PreserveDescriptorAndBindings()
        {
            CreatePrefab();var source=AssetDatabase.LoadAssetAtPath<GameObject>(Prefab);var marker=source.GetComponent<UIBindingMarker>();
            marker.externalProviderId="addressables";marker.externalAssetId="UI/Variant";EditorUtility.SetDirty(marker);AssetDatabase.SaveAssets();
            var instance=(GameObject)PrefabUtility.InstantiatePrefab(source);
            var variant=Folder+"/Variant.prefab";
            try{instance.transform.GetChild(0).name="RenamedLabel";PrefabUtility.SaveAsPrefabAsset(instance,variant);}
            finally{UnityEngine.Object.DestroyImmediate(instance);}
            Assert.That(AssetDatabase.MoveAsset(Prefab,Folder+"/RenamedBase.prefab"),Is.Empty);
            var text=(string)Call("Preview",new object[]{new[]{variant}});
            Assert.That(text,Does.Contain("UIViewAsset.External(\"addressables\",\"UI/Variant\")"));
            Assert.That(text,Does.Contain("RectTransform").And.Contain("label"));Assert.That(text,Does.Not.Contain("GameObject prefab"));
        }
        [Test] public void ExternalRegistration_PartialOrBlankDescriptorFailsAtAsset()
        {
            CreatePrefab();var asset=AssetDatabase.LoadAssetAtPath<GameObject>(Prefab);var so=new SerializedObject(asset.GetComponent("UIBindingMarker"));
            Assert.That(so.FindProperty("externalProviderId"),Is.Not.Null);
            so.FindProperty("externalProviderId").stringValue="addressables";so.ApplyModifiedPropertiesWithoutUndo();
            Assert.That(Assert.Throws<InvalidOperationException>(()=>Call("Preview",new object[]{new[]{Prefab}})).Message,Does.Contain(Prefab));
            so.FindProperty("externalAssetId").stringValue=" ";so.ApplyModifiedPropertiesWithoutUndo();
            Assert.Throws<InvalidOperationException>(()=>Call("Preview",new object[]{new[]{Prefab}}));
        }
        [Test] public void BusinessAssignments_AreNotFieldConflicts()
        {
            var check=Tool("UICodegenPipeline").GetMethod("ConflictsWithMember",BindingFlags.Static|BindingFlags.NonPublic);
            Assert.That(check.Invoke(null,new object[]{"void Bind() { label = GetComponent<RectTransform>(); }","label"}),Is.False);
            Assert.That(check.Invoke(null,new object[]{"private RectTransform label;","label"}),Is.True);
            Assert.That(check.Invoke(null,new object[]{"public RectTransform label { get; set; }","label"}),Is.True);
        }
        [Test] public void SecondGeneration_ProducesNoDiff()
        {
            CreatePrefab();var first=(string)Call("Preview",new object[]{new[]{Prefab}});
            Assert.That(Call("Preview",new object[]{new[]{Prefab}}),Is.EqualTo(first));
        }
        [Test] public void UserPartialIsNeverOverwritten()
        {
            var path=Folder+"/UserView.cs";File.WriteAllText(path,"// user content");
            Tool("UIBindingGenerator").GetMethod("CreateUserPartial").Invoke(null,new object[]{path,"Demo","UserView"});
            Assert.That(File.ReadAllText(path),Is.EqualTo("// user content"));
        }
        [Test] public void RenameAndVariant_PreserveBindings()
        {
            CreatePrefab();var asset=AssetDatabase.LoadAssetAtPath<GameObject>(Prefab);
            var instance=(GameObject)PrefabUtility.InstantiatePrefab(asset);
            try {instance.transform.GetChild(0).name="Renamed";PrefabUtility.SaveAsPrefabAsset(instance,Folder+"/Variant.prefab");}
            finally {UnityEngine.Object.DestroyImmediate(instance);}
            var text=(string)Call("Preview",new object[]{new[]{Folder+"/Variant.prefab"}});
            Assert.That(text,Does.Contain("label").And.Contain("RectTransform"));
        }
        [Test] public void CrossPrefabReference_FailsWithAssetLocation()
        {
            CreatePrefab();var other=new GameObject("Other",typeof(RectTransform));
            try {
                PrefabUtility.SaveAsPrefabAsset(other,Folder+"/Other.prefab");
                var asset=AssetDatabase.LoadAssetAtPath<GameObject>(Prefab);var mark=asset.GetComponent("UIBindingMarker");
                var so=new SerializedObject(mark);so.FindProperty("bindings").GetArrayElementAtIndex(0).FindPropertyRelative("target").objectReferenceValue=AssetDatabase.LoadAssetAtPath<GameObject>(Folder+"/Other.prefab").transform;so.ApplyModifiedPropertiesWithoutUndo();
                Assert.That(Assert.Throws<InvalidOperationException>(()=>Call("Preview",new object[]{new[]{Prefab}})).Message,Does.Contain(Prefab));
            } finally {UnityEngine.Object.DestroyImmediate(other);}
        }
        [Test] public void DuplicateTypeOrMissingComponent_PreventsBuild()
        {
            CreatePrefab();AssetDatabase.CopyAsset(Prefab,Folder+"/Duplicate.prefab");
            Assert.Throws<InvalidOperationException>(()=>Call("Preview",new object[]{new[]{Prefab,Folder+"/Duplicate.prefab"}}));
            var asset=AssetDatabase.LoadAssetAtPath<GameObject>(Prefab);var so=new SerializedObject(asset.GetComponent("UIBindingMarker"));
            so.FindProperty("bindings").GetArrayElementAtIndex(0).FindPropertyRelative("target").objectReferenceValue=null;so.ApplyModifiedPropertiesWithoutUndo();
            Assert.Throws<InvalidOperationException>(()=>Call("Preview",new object[]{new[]{Prefab}}));
        }
        [Test] public void StaleFingerprint_PreventsBuild()
        {
            Assert.Throws<InvalidOperationException>(()=>Call("ValidateFor",new object[]{new[]{Folder+"/Missing.prefab"}}));
        }
    }
}
