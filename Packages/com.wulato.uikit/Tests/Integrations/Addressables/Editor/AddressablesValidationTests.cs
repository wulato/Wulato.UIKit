using System;
using System.Linq;
using System.IO;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.AddressableAssets;
using UnityEditor.AddressableAssets.Settings;
using UnityEditor.AddressableAssets.Settings.GroupSchemas;
using UnityEngine;
using Wulato.UIKit.Editor;
using Object = UnityEngine.Object;
namespace Wulato.UIKit.AddressablesTests.Editor
{
    [Category("addressables")]
    public sealed class AddressablesValidationTests
    {
        const string Folder="Assets/UIKitAddressablesValidationTests";
        AddressableAssetSettings settings;AddressableAssetGroup group;
        string settingsJson,settingsPath;byte[] settingsBytes;
        GameObject prefab;UIBindingMarker marker;string guid;
        [SetUp] public void Setup()
        {
            AssetDatabase.CreateFolder("Assets","UIKitAddressablesValidationTests");
            var go=new GameObject("EditorProbe",typeof(RectTransform),typeof(EditorProbeView),typeof(UIBindingMarker));
            prefab=PrefabUtility.SaveAsPrefabAsset(go,Folder+"/Probe.prefab");Object.DestroyImmediate(go);
            marker=prefab.GetComponent<UIBindingMarker>();marker.externalProviderId="addressables";marker.externalAssetId="validation/probe";
            guid=AssetDatabase.AssetPathToGUID(Folder+"/Probe.prefab");
            settings=AddressableAssetSettingsDefaultObject.GetSettings(true);
            settingsPath=AssetDatabase.GetAssetPath(settings);settingsJson=EditorJsonUtility.ToJson(settings);settingsBytes=File.ReadAllBytes(settingsPath);
            group=settings.CreateGroup("UIKitValidation-"+Guid.NewGuid().ToString("N"),false,false,false,null,typeof(BundledAssetGroupSchema));
            group.GetSchema<BundledAssetGroupSchema>().IncludeGUIDInCatalog=true;
        }
        void Register()=>settings.CreateOrMoveEntry(guid,group).address="validation/probe";
        [TearDown] public void Cleanup()
        {
            settings.RemoveGroup(group);AssetDatabase.DeleteAsset(Folder);
            EditorJsonUtility.FromJsonOverwrite(settingsJson,settings);EditorUtility.SetDirty(settings);AssetDatabase.SaveAssets();
            // Settings cache a serialization hash while transient test groups exist. Restore the fixture bytes too.
            File.WriteAllBytes(settingsPath,settingsBytes);
        }
        [Test] public void LeaseReleaseFailure_IsNotRetriedAfterImmediateDestruction()
            => LoaderOwnershipTests.AssertEditorReleaseFailureIsNotRetried();
        [Test] public void ExplicitAddressAndGuid_AreAcceptedWithoutChangingSettings()
        {
            Register();var count=settings.groups.Count;
            marker.externalProviderId="";marker.externalAssetId="";EditorUtility.SetDirty(marker);AssetDatabase.SaveAssetIfDirty(marker);
            AddressablesUIValidator.ValidateSource(prefab,"validation/probe");Assert.That(marker.externalProviderId,Is.Empty,"Preflight must be read-only.");
            var coreEditor=AppDomain.CurrentDomain.GetAssemblies().Single(a=>a.GetName().Name=="Wulato.UIKit.Editor");
            var apply=coreEditor.GetType("Wulato.UIKit.Editor.Authoring.UIAssetSourceAuthoring",true).GetMethod("Apply",System.Reflection.BindingFlags.Static|System.Reflection.BindingFlags.Public);
            apply.Invoke(null,new object[]{AssetDatabase.GetAssetPath(prefab),1,"","validation/probe"});Assert.That(marker.externalProviderId,Is.EqualTo("addressables"));
            var bytes=File.ReadAllBytes(AssetDatabase.GetAssetPath(prefab));
            Assert.That(Assert.Throws<System.Reflection.TargetInvocationException>(()=>apply.Invoke(null,new object[]{AssetDatabase.GetAssetPath(prefab),1,"","wrong"})).InnerException,Is.TypeOf<InvalidOperationException>());
            Assert.That(File.ReadAllBytes(AssetDatabase.GetAssetPath(prefab)),Is.EqualTo(bytes));
            AddressablesUIValidator.ValidatePrefab(prefab);marker.externalAssetId=guid;AddressablesUIValidator.ValidatePrefab(prefab);
            Assert.That(settings.groups.Count,Is.EqualTo(count));Assert.That(settings.FindAssetEntry(guid).address,Is.EqualTo("validation/probe"));
        }
        [Test] public void MissingEntryOrMismatchedAddress_IsRejectedWithAssetLocation()
        {
            Assert.That(Assert.Throws<InvalidOperationException>(()=>AddressablesUIValidator.ValidatePrefab(prefab)).Message,Does.Contain(Folder));
            Register();marker.externalAssetId="wrong";Assert.Throws<InvalidOperationException>(()=>AddressablesUIValidator.ValidatePrefab(prefab));
        }
        [Test] public void DuplicateAddressAndLabel_AreRejected()
        {
            Register();AssetDatabase.CopyAsset(Folder+"/Probe.prefab",Folder+"/Duplicate.prefab");
            var duplicate=settings.CreateOrMoveEntry(AssetDatabase.AssetPathToGUID(Folder+"/Duplicate.prefab"),group);duplicate.address="validation/probe";
            Assert.Throws<InvalidOperationException>(()=>AddressablesUIValidator.ValidatePrefab(prefab));
            duplicate.address="different";settings.AddLabel("single-label");settings.FindAssetEntry(guid).SetLabel("single-label",true);marker.externalAssetId="single-label";
            Assert.Throws<InvalidOperationException>(()=>AddressablesUIValidator.ValidatePrefab(prefab));settings.RemoveLabel("single-label");
        }
        [Test] public void GuidExcludedOrInvalidRoot_IsRejected()
        {
            Register();marker.externalAssetId=guid;group.GetSchema<BundledAssetGroupSchema>().IncludeGUIDInCatalog=false;
            Assert.Throws<InvalidOperationException>(()=>AddressablesUIValidator.ValidatePrefab(prefab));
            marker.externalAssetId="validation/probe";prefab.GetComponent<EditorProbeView>().enabled=false;
            Assert.Throws<InvalidOperationException>(()=>AddressablesUIValidator.ValidatePrefab(prefab));
        }
    }
}
