using System;
using System.IO;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using Wulato.UIKit.Tests.Support;
namespace Wulato.UIKit.Tests.Editor
{
    [Category("authoring")]
    public class AuthoringTests
    {
        const string Folder="Assets/UIKitAuthoringTests";
        Scene scene; UIRegistry registry;
        static Type Driver {
            get {
#if ENABLE_INPUT_SYSTEM && !ENABLE_LEGACY_INPUT_MANAGER
                return Type.GetType("Wulato.UIKit.InputSystem.InputSystemUIInputDriver, Wulato.UIKit.InputSystem",true);
#else
                return typeof(LegacyUIInputDriver);
#endif
            }
        }
        object Call(string service,string method,params object[] args)
        {
            var type=AppDomain.CurrentDomain.GetAssemblies().Select(a=>a.GetType("Wulato.UIKit.Editor.Authoring."+service)).FirstOrDefault(t=>t!=null);
            Assert.That(type,Is.Not.Null,"Authoring service missing: "+service);
            try{return type.GetMethod(method,BindingFlags.Static|BindingFlags.Public|BindingFlags.NonPublic).Invoke(null,args);}
            catch(TargetInvocationException e){throw e.InnerException;}
        }
        [SetUp] public void Setup()
        {
            Assert.That(AssetDatabase.IsValidFolder(Folder),Is.False);AssetDatabase.CreateFolder("Assets","UIKitAuthoringTests");
            scene=EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);
            registry=ScriptableObject.CreateInstance<UIRegistry>();
        }
        [TearDown] public void Cleanup()
        {
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);UnityEngine.Object.DestroyImmediate(registry);AssetDatabase.DeleteAsset(Folder);
        }
        [Test] public void SceneApply_IsIdempotentAndUndoable()
        {
            var root=(UIRoot)Call("UISceneAuthoring","Apply",scene,null,registry,null,Driver);
            var events=scene.GetRootGameObjects().SelectMany(g=>g.GetComponentsInChildren<EventSystem>(true)).Single();
            Assert.That(Call("UISceneAuthoring","Apply",scene,root,registry,events,Driver),Is.SameAs(root));
            Assert.That(scene.GetRootGameObjects().Length,Is.EqualTo(2));Assert.That(scene.isDirty,Is.True);
            Undo.PerformUndo();Undo.PerformUndo();
            Assert.That(scene.GetRootGameObjects(),Is.Empty);
        }
        [Test] public void SceneConflict_DoesNotAllocateRoot()
        {
            for(int i=0;i<2;i++){var go=new GameObject("Events",typeof(EventSystem));SceneManager.MoveGameObjectToScene(go,scene);}
            Assert.Throws<InvalidOperationException>(()=>Call("UISceneAuthoring","Apply",scene,null,registry,null,Driver));
            Assert.That(scene.GetRootGameObjects().Length,Is.EqualTo(2));
        }
        [Test] public void InvalidDriverOrMissingRegistry_DoesNotMutateScene()
        {
            Assert.Throws<ArgumentException>(()=>Call("UISceneAuthoring","Apply",scene,null,registry,null,typeof(StandaloneInputModule)));
            Assert.Throws<ArgumentException>(()=>Call("UISceneAuthoring","Apply",scene,null,null,null,Driver));
            Assert.That(scene.GetRootGameObjects(),Is.Empty);
        }
        [Test] public void EnabledForeignModule_IsNotReplaced()
        {
            var go=new GameObject("Events",typeof(EventSystem),typeof(StandaloneInputModule));SceneManager.MoveGameObjectToScene(go,scene);
            Assert.Throws<InvalidOperationException>(()=>Call("UISceneAuthoring","Apply",scene,null,registry,go.GetComponent<EventSystem>(),Driver));
            Assert.That(go.GetComponent<StandaloneInputModule>(),Is.Not.Null);Assert.That(go.GetComponent<LegacyUIInputDriver>(),Is.Null);
        }
        [Test] public void ViewSource_ValidatesNamesAndPreservesExistingFile()
        {
            var path=Folder+"/Example.cs.txt";
            Call("UIViewAuthoring","WriteSource",path,"Game.UI","Example",null);
            Assert.That(File.ReadAllText(path),Does.Contain("partial class Example : global::Wulato.UIKit.UIView"));
            Assert.Throws<IOException>(()=>Call("UIViewAuthoring","WriteSource",path,"Game.UI","Example",null));
            Assert.Throws<ArgumentException>(()=>Call("UIViewAuthoring","WriteSource",Folder+"/Bad.txt","Game..UI","Example",null));
        }
        [Test] public void TypedViewSource_UsesCompiledArgumentType()
        {
            var path=Folder+"/Typed.txt";Call("UIViewAuthoring","WriteSource",path,"","Typed",typeof(string));
            Assert.That(File.ReadAllText(path),Does.Contain("UIView<global::System.String>"));
            Assert.Throws<ArgumentException>(()=>Call("UIViewAuthoring","WriteSource",Folder+"/Bad.txt","","Bad",typeof(System.Collections.Generic.List<>)));
        }
        [Test] public void ExistingPrefab_PreservesGuidAndRejectsVariant()
        {
            var go=new GameObject("View",typeof(RectTransform),typeof(CodegenTestView));
            var path=Folder+"/View.prefab";
            try{PrefabUtility.SaveAsPrefabAsset(go,path);}finally{UnityEngine.Object.DestroyImmediate(go);}
            var guid=AssetDatabase.AssetPathToGUID(path);
            Call("UIViewAuthoring","Configure",path,MonoScript.FromMonoBehaviour(AssetDatabase.LoadAssetAtPath<GameObject>(path).GetComponent<CodegenTestView>()));
            Assert.That(AssetDatabase.AssetPathToGUID(path),Is.EqualTo(guid));
            var prefab=AssetDatabase.LoadAssetAtPath<GameObject>(path);Assert.That(prefab.GetComponent<UIBindingMarker>().typeKey,Is.Not.Empty);
            var instance=(GameObject)PrefabUtility.InstantiatePrefab(prefab);
            try{PrefabUtility.SaveAsPrefabAsset(instance,Folder+"/Variant.prefab");}finally{UnityEngine.Object.DestroyImmediate(instance);}
            Assert.Throws<ArgumentException>(()=>Call("UIViewAuthoring","Configure",Folder+"/Variant.prefab",MonoScript.FromMonoBehaviour(prefab.GetComponent<CodegenTestView>())));
        }
        [Test] public void SampleDirectoryResolution_RequiresExplicitChoiceWhenAmbiguous()
        {
            var a=Folder+"/QuickStart";Directory.CreateDirectory(a);File.WriteAllText(a+"/QuickStartDemo.cs.txt","a");
            var b=Folder+"/0.3.0/QuickStart";Directory.CreateDirectory(b);File.WriteAllText(b+"/QuickStartDemo.cs.txt","b");
            Assert.Throws<InvalidOperationException>(()=>Call("UISampleAuthoring","SelectDirectory",new[]{a,b},null));
            Assert.That(Call("UISampleAuthoring","SelectDirectory",new[]{a,b},b),Is.EqualTo(b));
        }
        [Test] public void UsageConflict_DoesNotOverwriteConsumerCode()
        {
            var source=Folder+"/Usage.cs.txt";var target=Folder+"/Usage.txt";File.WriteAllText(source,"new");File.WriteAllText(target,"consumer");
            Assert.Throws<IOException>(()=>Call("UISampleAuthoring","InstallUsage",source,target));Assert.That(File.ReadAllText(target),Is.EqualTo("consumer"));
        }
        [Test] public void GenerationState_RoundTripsFailureAndPreventsReentry()
        {
            Call("UIAuthoringOperation","Reset");
            try
            {
                Assert.That(Call("UIAuthoringOperation","TryBegin","generate"),Is.EqualTo(true));
                Assert.That(Call("UIAuthoringOperation","TryBegin","generate"),Is.EqualTo(false));
                Call("UIAuthoringOperation","Fail","writeback",Folder+"/Probe.prefab","input changed");
                Assert.That(Call("UIAuthoringOperation","Describe").ToString(),Does.Contain("writeback").And.Contain("input changed"));
                Assert.That(Call("UIAuthoringOperation","TryBegin","generate"),Is.EqualTo(true));
            }
            finally{Call("UIAuthoringOperation","Reset");}
        }
        [Test] public void DuplicateBinding_RejectsBeforeSavingPrefab()
        {
            var go=new GameObject("View",typeof(RectTransform),typeof(CodegenTestView),typeof(UIBindingMarker));
            var path=Folder+"/Duplicate.prefab";
            var script=MonoScript.FromMonoBehaviour(go.GetComponent<CodegenTestView>());
            go.GetComponent<UIBindingMarker>().viewScript=script;
            go.GetComponent<UIBindingMarker>().bindings=new[]{new UIBindingMarker.Binding{fieldName="same",target=go.transform},new UIBindingMarker.Binding{fieldName="same",target=go.transform}};
            try{PrefabUtility.SaveAsPrefabAsset(go,path);}finally{UnityEngine.Object.DestroyImmediate(go);}
            var before=File.ReadAllBytes(path);
            Assert.Throws<ArgumentException>(()=>Call("UIViewAuthoring","Configure",path,script));Assert.That(File.ReadAllBytes(path),Is.EqualTo(before));
        }
        [Test] public void TypedView_RejectsEditorOnlyArgumentBeforeWriting()
        {
            var path=Folder+"/EditorArgs.txt";
            Assert.Throws<ArgumentException>(()=>Call("UIViewAuthoring","WriteSource",path,"","EditorArgs",typeof(EditorWindow)));
            Assert.That(File.Exists(path),Is.False);
        }
        [Test] public void InvalidRootLayers_DoNotAllocateInputObjects()
        {
            var go=new GameObject("Invalid root",typeof(RectTransform),typeof(UIRoot));SceneManager.MoveGameObjectToScene(go,scene);
            var so=new SerializedObject(go.GetComponent<UIRoot>());so.FindProperty("layers").arraySize=3;so.ApplyModifiedPropertiesWithoutUndo();
            Assert.Throws<InvalidOperationException>(()=>Call("UISceneAuthoring","Apply",scene,go.GetComponent<UIRoot>(),registry,null,Driver));Assert.That(scene.GetRootGameObjects().Length,Is.EqualTo(1));
        }
        [Test] public void CancelledRequest_BlocksAutomaticWritebackAcrossReload()
        {
            Call("UIAuthoringOperation","Reset");
            try
            {
                Call("UIAuthoringOperation","TryBegin","generate");
                var type=AppDomain.CurrentDomain.GetAssemblies().Select(a=>a.GetType("Wulato.UIKit.Editor.Authoring.UIAuthoringOperation")).First(t=>t!=null);
                Call("UIAuthoringOperation","ObserveGeneration",true);
                Call("UIAuthoringOperation","Cancel");
                var saved=Call("UIAuthoringOperation","Read");type.GetField("state",BindingFlags.NonPublic|BindingFlags.Static).SetValue(null,saved);
                Assert.That(type.GetProperty("SuspendAutomaticWriteback",BindingFlags.NonPublic|BindingFlags.Static).GetValue(null),Is.EqualTo(true));
                Assert.That(Call("UIAuthoringOperation","Describe").ToString(),Does.Contain("已取消"));
                Assert.That(Call("UIAuthoringOperation","TryBegin","generate"),Is.EqualTo(true));
                Assert.That(type.GetProperty("SuspendAutomaticWriteback",BindingFlags.NonPublic|BindingFlags.Static).GetValue(null),Is.EqualTo(false));
            }
            finally{Call("UIAuthoringOperation","Reset");}
        }
        [Test] public void ExistingViewConflict_DoesNotChangeAsset()
        {
            var go=new GameObject("View",typeof(RectTransform),typeof(T4ProbeView));var path=Folder+"/Conflict.prefab";
            var other=new GameObject("Other",typeof(CodegenTestView));var script=MonoScript.FromMonoBehaviour(other.GetComponent<CodegenTestView>());
            try{PrefabUtility.SaveAsPrefabAsset(go,path);}finally{UnityEngine.Object.DestroyImmediate(go);UnityEngine.Object.DestroyImmediate(other);}
            var before=File.ReadAllBytes(path);
            Assert.Throws<InvalidOperationException>(()=>Call("UIViewAuthoring","Configure",path,script));Assert.That(File.ReadAllBytes(path),Is.EqualTo(before));
        }
        [Test] public void GenerateMenu_RejectsConcurrentWorkbenchRequest()
        {
            var go=new GameObject("Invalid input",typeof(RectTransform),typeof(UIBindingMarker));
            try{PrefabUtility.SaveAsPrefabAsset(go,Folder+"/Invalid.prefab");}finally{UnityEngine.Object.DestroyImmediate(go);}
            Call("UIAuthoringOperation","Reset");Call("UIAuthoringOperation","TryBegin","sample");
            try
            {
                var type=AppDomain.CurrentDomain.GetAssemblies().Select(a=>a.GetType("Wulato.UIKit.Editor.UICodegenPipeline")).First(t=>t!=null);
                var error=Assert.Throws<TargetInvocationException>(()=>type.GetMethod("Generate").Invoke(null,null));
                Assert.That(error.InnerException,Is.TypeOf<InvalidOperationException>());Assert.That(error.InnerException.Message,Does.Contain("进行中"));
            }
            finally{Call("UIAuthoringOperation","Reset");}
        }
        [Test] public void Cancel_DoesNotRestoreOverUserChangedSceneLayout()
        {
            Call("UIAuthoringOperation","Reset");Call("UIAuthoringOperation","TryBegin","sample");
            var type=AppDomain.CurrentDomain.GetAssemblies().Select(a=>a.GetType("Wulato.UIKit.Editor.Authoring.UIAuthoringOperation")).First(t=>t!=null);
            var request=type.GetProperty("Current",BindingFlags.Static|BindingFlags.NonPublic).GetValue(null);
            Assert.That(EditorSceneManager.SaveScene(scene,Folder+"/Before.unity"),Is.True);
            request.GetType().GetField("scenes").SetValue(request,EditorSceneManager.GetSceneManagerSetup());
            scene=EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);
            Assert.That(EditorSceneManager.SaveScene(scene,Folder+"/UserChanged.unity"),Is.True);
            try{Call("UIAuthoringOperation","Cancel");Assert.That(SceneManager.GetActiveScene().path,Is.EqualTo(Folder+"/UserChanged.unity"));}
            finally{Call("UIAuthoringOperation","Reset");}
        }
        [Test] public void CompilerFailure_PreservesSampleTargetForRetry()
        {
            Call("UIAuthoringOperation","Reset");Call("UIAuthoringOperation","TryBegin","sample");
            try
            {
                var type=AppDomain.CurrentDomain.GetAssemblies().Select(a=>a.GetType("Wulato.UIKit.Editor.Authoring.UIAuthoringOperation")).First(t=>t!=null);
                var request=type.GetProperty("Current",BindingFlags.Static|BindingFlags.NonPublic).GetValue(null);
                var target=request.GetType().GetField("sampleDirectory");Assert.That(target,Is.Not.Null,"Sample target must be separate from diagnostic asset.");
                target.SetValue(request,Folder+"/QuickStart");
                Call("UIAuthoringOperation","Fail","compile",Folder+"/Broken.cs","compiler error");
                var saved=Call("UIAuthoringOperation","Read");Assert.That(target.GetValue(saved),Is.EqualTo(Folder+"/QuickStart"));
                Assert.That(saved.GetType().GetField("asset").GetValue(saved),Is.EqualTo(Folder+"/Broken.cs"));
            }
            finally{Call("UIAuthoringOperation","Reset");}
        }
        [Test] public void SamplePreviewCamera_IsIdempotentAndPreservesAuthoredCamera()
        {
            var camera=(Camera)Call("UISampleAuthoring","EnsurePreviewCamera",scene);
            Assert.That(camera,Is.Not.Null);Assert.That(camera.enabled,Is.True);Assert.That(camera.cullingMask,Is.Zero);
            Assert.That(Call("UISampleAuthoring","EnsurePreviewCamera",scene),Is.SameAs(camera));
            Assert.That(scene.GetRootGameObjects().Count(g=>g.GetComponent<Camera>()!=null),Is.EqualTo(1));
            camera.cullingMask=123;
            Assert.That(Call("UISampleAuthoring","EnsurePreviewCamera",scene),Is.SameAs(camera));Assert.That(camera.cullingMask,Is.EqualTo(123));
        }
        [Test] public void WorkbenchPrefabSelection_BackfillsScriptAndClearsStaleSelection()
        {
            var go=new GameObject("View",typeof(RectTransform),typeof(CodegenTestView),typeof(UIBindingMarker));
            var script=MonoScript.FromMonoBehaviour(go.GetComponent<CodegenTestView>());go.GetComponent<UIBindingMarker>().viewScript=script;
            var path=Folder+"/Selected.prefab";try{PrefabUtility.SaveAsPrefabAsset(go,path);}finally{UnityEngine.Object.DestroyImmediate(go);}
            var type=AppDomain.CurrentDomain.GetAssemblies().Select(a=>a.GetType("Wulato.UIKit.Editor.Authoring.UIAuthoringWorkbench")).First(t=>t!=null);
            var window=ScriptableObject.CreateInstance(type);
            try
            {
                var select=type.GetMethod("SelectPrefab",BindingFlags.Instance|BindingFlags.NonPublic);Assert.That(select,Is.Not.Null);
                select.Invoke(window,new object[]{AssetDatabase.LoadAssetAtPath<GameObject>(path)});
                Assert.That(type.GetField("viewScript",BindingFlags.Instance|BindingFlags.NonPublic).GetValue(window),Is.EqualTo(script));
                select.Invoke(window,new object[]{null});
                Assert.That(type.GetField("viewScript",BindingFlags.Instance|BindingFlags.NonPublic).GetValue(window),Is.Null);
            }
            finally{UnityEngine.Object.DestroyImmediate(window);}
        }
    }
}
