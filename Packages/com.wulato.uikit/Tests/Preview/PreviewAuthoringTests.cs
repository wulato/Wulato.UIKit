using System;
using System.IO;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
namespace Wulato.UIKit.Tests.Preview
{
    [Category("preview_authoring")]
    public class PreviewAuthoringTests
    {
        Type Find(string name)=>AppDomain.CurrentDomain.GetAssemblies().Select(a=>a.GetType("Wulato.UIKit.Editor.Authoring."+name)).FirstOrDefault(t=>t!=null);
        [Test] public void Profile_RejectsMissingRegistry_WithoutMutatingScene()
        {
            var type=Find("UIPreviewProfile");Assert.That(type,Is.Not.Null,"Preview profile not implemented");var profile=ScriptableObject.CreateInstance(type);
            try
            {
                var controller=Find("UIPreviewController");Assert.That(controller,Is.Not.Null);
                var error=Assert.Throws<TargetInvocationException>(()=>controller.GetMethod("ValidateProfile",BindingFlags.Public|BindingFlags.Static).Invoke(null,new object[]{profile}));
                Assert.That(error.InnerException,Is.TypeOf<ArgumentException>());
            }finally{UnityEngine.Object.DestroyImmediate(profile);}
        }
        [Test] public void Stop_PersistenceFailureDoesNotAbortCleanup()
        {
            var controller=Find("UIPreviewController");var operation=Find("UIPreviewOperation");var stateType=operation.Assembly.GetType("Wulato.UIKit.Editor.Authoring.UIPreviewState");var state=Activator.CreateInstance(stateType,true);
            var field=controller.GetField("state",BindingFlags.NonPublic|BindingFlags.Static);Assert.That(field.GetValue(null),Is.Null);
            var path="Library/WulatoUIKit/preview.json";Assert.That(File.Exists(path)||Directory.Exists(path),Is.False);Directory.CreateDirectory(path);field.SetValue(null,state);
            try
            {
                UnityEngine.TestTools.LogAssert.Expect(LogType.Exception,new System.Text.RegularExpressions.Regex("InvalidOperationException: Scene layout changed"));
                Assert.DoesNotThrow(()=>((Cysharp.Threading.Tasks.UniTask)controller.GetMethod("StopAsync",BindingFlags.Public|BindingFlags.Static).Invoke(null,null)).GetAwaiter().GetResult());
                StringAssert.Contains("could not be saved",(string)controller.GetProperty("LastMessage").GetValue(null));
                Assert.That(Directory.Exists(path),Is.True,"Persistence failure must preserve its blocking directory.");
            }
            finally{field.SetValue(null,null);Directory.Delete(path);if(File.Exists(path+".tmp"))File.Delete(path+".tmp");}
        }
        [Test] public void ClosingRecoveryWindow_DoesNotChooseRestoreForUser()
        {
            var operation=Find("UIPreviewOperation");var controller=Find("UIPreviewController");var stateType=operation.Assembly.GetType("Wulato.UIKit.Editor.Authoring.UIPreviewState");var state=Activator.CreateInstance(stateType,true);
            var field=controller.GetField("state",BindingFlags.NonPublic|BindingFlags.Static);Assert.That(field.GetValue(null),Is.Null);
            var path="Library/WulatoUIKit/preview.json";Assert.That(File.Exists(path),Is.False);
            stateType.GetField("phase").SetValue(state,"recovery");field.SetValue(null,state);
            try
            {
                operation.GetMethod("Save",BindingFlags.NonPublic|BindingFlags.Static).Invoke(null,new[]{state});
                var window=EditorWindow.GetWindow(Find("UIAuthoringWorkbench"));window.Close();
                Assert.That(stateType.GetField("phase").GetValue(state),Is.EqualTo("recovery"),"Closing the window must not implicitly choose recovery.");
                Assert.That(File.Exists(path),Is.True);
            }finally{field.SetValue(null,null);if(File.Exists(path))File.Delete(path);}
        }
        [Test] public void CorruptRecovery_IsRejectedAndPreserved()
        {
            var type=Find("UIPreviewOperation");Assert.That(type,Is.Not.Null);
            var path="Library/WulatoUIKit/preview.json";Assert.That(File.Exists(path),Is.False,"Existing recovery must be preserved before this test.");Directory.CreateDirectory(Path.GetDirectoryName(path));
            try
            {
                File.WriteAllText(path,"{\"schema\":1}");
                Assert.Throws<TargetInvocationException>(()=>type.GetMethod("Read",BindingFlags.NonPublic|BindingFlags.Static).Invoke(null,null));
                Assert.That(File.ReadAllText(path),Is.EqualTo("{\"schema\":1}"));
            }finally{File.Delete(path);}
        }
        [Test] public void OwnedDirectory_ModifiedFolderMetadata_IsNeverDeleted()
        {
            var type=Find("UIPreviewOperation");var stateType=type.Assembly.GetType("Wulato.UIKit.Editor.Authoring.UIPreviewState");var state=Activator.CreateInstance(stateType,true);
            var request=Guid.NewGuid().ToString("N");var folder="Assets/Wulato.UIKit.Preview/"+request;var parentExisted=Directory.Exists("Assets/Wulato.UIKit.Preview");
            Directory.CreateDirectory(folder);File.WriteAllText(folder+"/owned.txt","owned");AssetDatabase.Refresh();
            stateType.GetField("request").SetValue(state,request);stateType.GetField("directory").SetValue(state,folder);
            try
            {
                type.GetMethod("CaptureFiles",BindingFlags.NonPublic|BindingFlags.Static).Invoke(null,new[]{state});
                File.AppendAllText(folder+".meta","\nuserData: user-edited-folder\n");
                Assert.That(type.GetMethod("DeleteOwned",BindingFlags.NonPublic|BindingFlags.Static).Invoke(null,new[]{state}),Is.False);
                Assert.That(File.Exists(folder+"/owned.txt"),Is.True);
            }finally{AssetDatabase.DeleteAsset(folder);if(!parentExisted&&Directory.Exists("Assets/Wulato.UIKit.Preview")&&!Directory.EnumerateFileSystemEntries("Assets/Wulato.UIKit.Preview").Any())AssetDatabase.DeleteAsset("Assets/Wulato.UIKit.Preview");}
        }
        [Test] public void OwnedDirectory_WithNewUserFile_IsNeverDeleted()
        {
            var type=Find("UIPreviewOperation");var stateType=type.Assembly.GetType("Wulato.UIKit.Editor.Authoring.UIPreviewState");var state=Activator.CreateInstance(stateType,true);
            var request=Guid.NewGuid().ToString("N");var folder="Assets/Wulato.UIKit.Preview/"+request;var parentExisted=Directory.Exists("Assets/Wulato.UIKit.Preview");
            Directory.CreateDirectory(folder);File.WriteAllText(folder+"/owned.txt","owned");AssetDatabase.Refresh();
            stateType.GetField("request").SetValue(state,request);stateType.GetField("directory").SetValue(state,folder);
            try
            {
                type.GetMethod("CaptureFiles",BindingFlags.NonPublic|BindingFlags.Static).Invoke(null,new[]{state});
                File.WriteAllText(folder+"/user.txt","keep me");AssetDatabase.Refresh();
                Assert.That(type.GetMethod("DeleteOwned",BindingFlags.NonPublic|BindingFlags.Static).Invoke(null,new[]{state}),Is.False);
                Assert.That(File.ReadAllText(folder+"/user.txt"),Is.EqualTo("keep me"));
            }finally{AssetDatabase.DeleteAsset(folder);if(!parentExisted&&!Directory.EnumerateFileSystemEntries("Assets/Wulato.UIKit.Preview").Any())AssetDatabase.DeleteAsset("Assets/Wulato.UIKit.Preview");}
        }
        [Test] public void AssetOwnership_RejectsModifiedOrReplacedFiles()
        {
            var type=Find("UIPreviewOperation");Assert.That(type,Is.Not.Null,"Preview ownership not implemented");
            var folder="Assets/UIKitPreviewOwnershipTest";Assert.That(AssetDatabase.IsValidFolder(folder),Is.False);AssetDatabase.CreateFolder("Assets","UIKitPreviewOwnershipTest");
            try
            {
                var path=folder+"/data.txt";File.WriteAllText(path,"original");AssetDatabase.ImportAsset(path);
                var guid=AssetDatabase.AssetPathToGUID(path);var hash=(string)type.GetMethod("Hash",BindingFlags.Public|BindingFlags.Static).Invoke(null,new object[]{path});
                var method=type.GetMethod("Matches",BindingFlags.Public|BindingFlags.Static);
                Assert.That(method.Invoke(null,new object[]{path,guid,hash}),Is.True);
                File.WriteAllText(path,"user modified");Assert.That(method.Invoke(null,new object[]{path,guid,hash}),Is.False);
                Assert.That(method.Invoke(null,new object[]{path,"another-guid",hash}),Is.False);
            }finally{AssetDatabase.DeleteAsset(folder);}
        }
    }
}
