using System;
using System.Collections;
using System.IO;
using System.Linq;
using Cysharp.Threading.Tasks;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.ResourceManagement.AsyncOperations;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;
using AddressablesAPI = UnityEngine.AddressableAssets.Addressables;
namespace Wulato.UIKit.Verification.Addressables.Editor
{
    [Category("addressables_preflight")]
    public sealed class AddressablesPreflight
    {
        [UnityTest] public IEnumerator PackedBundle_DefersAwakeAndReleasesHandle() => UniTask.ToCoroutine(async () =>
        {
            Assert.That(AddressablesAPI.ResourceLocators, Is.Not.Null);
            var locations=AddressablesAPI.LoadResourceLocationsAsync("uikit/probe",typeof(GameObject));
            try
            {
                while(!locations.IsDone)await UniTask.NextFrame();
                Assert.That(locations.Result.Count,Is.EqualTo(1));
                Assert.That(locations.Result[0].ProviderId,Does.Contain("BundledAssetProvider"),"AssetDatabase/Fast Mode is not Bundle acceptance");
            }
            finally {AddressablesAPI.Release(locations);}
            AddressableProbeView.Events.Clear();
            var parent = new GameObject("InactiveAddressablesParent"); parent.SetActive(false);
            var op = AddressablesAPI.LoadAssetAsync<GameObject>("uikit/probe");
            GameObject instance = null;
            try
            {
                while (!op.IsDone) await UniTask.NextFrame();
                Assert.That(op.Status, Is.EqualTo(AsyncOperationStatus.Succeeded), op.OperationException?.ToString());
                instance = Object.Instantiate(op.Result,parent.transform,false);
                Assert.That(instance.activeInHierarchy, Is.False);
                Assert.That(AddressableProbeView.Events, Is.Empty);
                instance.SetActive(true); parent.SetActive(true);
                Assert.That(AddressableProbeView.Events, Is.EqualTo(new[]{"awake","enable"}));
                Object.Destroy(instance); await UniTask.NextFrame();
                Assert.That(instance == null, Is.True);
                Assert.That(AddressableProbeView.Events.Last(), Is.EqualTo("destroy"));
            }
            finally { if (instance) Object.DestroyImmediate(instance); Object.DestroyImmediate(parent); AddressablesAPI.Release(op); }
            Assert.That(op.IsValid(), Is.False);
            var guidOp = AddressablesAPI.LoadAssetAsync<GameObject>(Resources.Load<TextAsset>("UIKitAddressablesGuid").text.Trim());
            try { while(!guidOp.IsDone) await UniTask.NextFrame(); Assert.That(guidOp.Status,Is.EqualTo(AsyncOperationStatus.Succeeded)); }
            finally { AddressablesAPI.Release(guidOp); }
        });
        [TearDown] public void Trace()
        {
            var path=Environment.GetEnvironmentVariable("UIKIT_EVIDENCE_DIR");
            if(!string.IsNullOrEmpty(path)) File.WriteAllText(Path.Combine(path,TestContext.CurrentContext.Test.Name+".trace.json"),JsonUtility.ToJson(new Report{events=AddressableProbeView.Events.ToArray(),frame=Time.frameCount}));
        }
        [Serializable] sealed class Report { public string[] events; public int frame; }
    }
}
