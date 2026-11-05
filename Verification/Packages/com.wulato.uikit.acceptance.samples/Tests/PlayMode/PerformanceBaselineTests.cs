#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using Cysharp.Threading.Tasks;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.Profiling;
using Unity.Profiling;
using Unity.Profiling.LowLevel.Unsafe;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using Wulato.UIKit.Samples.Inventory;
namespace Wulato.UIKit.Acceptance.Samples
{
    public sealed class BaselineRegistry : UIRegistryProvider
    {
        public GameObject Prefab;public UIClosePolicy Policy;
        public override void Populate(UIRegistryBuilder builder)=>builder.Add<InventoryView,InventoryArgs>(new UIViewTypeKey("baseline-modal"),UIViewAsset.Direct(Prefab),UILayer.Modal,UIMultiplicity.Single,Policy);
    }
    [Category("performance")]
    public class PerformanceBaselineTests
    {
        [Serializable] sealed class Metric {public string name,unit;public bool available;public int samples;public long total,max;}
        [Serializable] sealed class Baseline
        {
            public string mode,editor=Application.unityVersion,platform=Application.platform.ToString(),execution="Editor PlayMode",backend,diagnostics="Basic",parameters="baseline-player / Equipment";
            public int iterations=50,created,liveLeases,liveInstances,pendingCleanup,cacheCapacity=1,postClearLiveLeases,postClearLiveInstances;public double[] elapsedMs,openMs,closeMs;
            public Metric gc,canvas;public long memoryBefore,memoryAfter;public string allocationScope="All captured frames in isolated sample scene, includes Editor/test harness; not per-command attribution";
        }
        static Metric Read(ProfilerRecorder recorder,string name,string unit)
        {
            var values=new List<ProfilerRecorderSample>();if(recorder.Valid)recorder.CopyTo(values);
            return new Metric{name=name,unit=unit,available=recorder.Valid,samples=values.Count,total=values.Sum(v=>v.Value),max=values.Count==0?0:values.Max(v=>v.Value)};
        }
        static ProfilerRecorder CanvasRecorder(out string name)
        {
            var handles=new List<ProfilerRecorderHandle>();ProfilerRecorderHandle.GetAvailable(handles);
            foreach(var candidate in new[]{"Canvas.BuildBatch","UGUI.Rendering.UpdateBatches","Canvas.SendWillRenderCanvases"})
                foreach(var handle in handles)
                {
                    var description=ProfilerRecorderHandle.GetDescription(handle);
                    if(description.Name==candidate){name=candidate;return ProfilerRecorder.StartNew(description.Category,candidate,4096);}
                }
            name="Canvas.BuildBatch (unavailable)";return default;
        }
        static UIRegistry Registry(UIClosePolicy policy,out BaselineRegistry provider)
        {
            provider=ScriptableObject.CreateInstance<BaselineRegistry>();provider.Policy=policy;
            provider.Prefab=AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Samples/Wulato UIKit/Inventory/InventoryView.prefab");Assert.That(provider.Prefab,Is.Not.Null);
            var registry=ScriptableObject.CreateInstance<UIRegistry>();var serialized=new SerializedObject(registry);serialized.FindProperty("providers").arraySize=1;serialized.FindProperty("providers").GetArrayElementAtIndex(0).objectReferenceValue=provider;serialized.ApplyModifiedPropertiesWithoutUndo();return registry;
        }
        [UnityTest] public IEnumerator Modal_ThreeModes_FiftyCycles()=>UniTask.ToCoroutine(async()=>
        {
            var previous=SceneManager.GetActiveScene();var scene=SceneManager.CreateScene("T10 isolated baseline");SceneManager.SetActiveScene(scene);
            try
            {
                foreach(var mode in new[]{"first-load","warm-release-rebuild","cache-one-reopen"})
                {
                    var registry=Registry(mode=="cache-one-reopen"?UIClosePolicy.CacheOne:UIClosePolicy.Release,out var provider);
                    SampleFixture f=null;var result=new Baseline{mode=mode,elapsedMs=new double[50],openMs=new double[50],closeMs=new double[50]};
                    ProfilerRecorder gc=default,canvas=default;
                    try
                    {
                        f=new SampleFixture(registry);result.backend=f.Events.GetComponents<UnityEngine.EventSystems.BaseInputModule>().Single().GetType().Name;
                        if(mode!="first-load")
                        {
                            var warm=await f.UI.OpenAsync<InventoryView,InventoryArgs>(new InventoryArgs("baseline-player",InventoryTab.Equipment));await warm.CloseAsync();await SampleFixture.Stable();
                        }
                        // Ensure marker/canvas registration, then record real frame samples, including async waits.
                        Canvas.ForceUpdateCanvases();gc=ProfilerRecorder.StartNew(ProfilerCategory.Memory,"GC Allocated In Frame",4096);canvas=CanvasRecorder(out var canvasName);
                        Assert.That(gc.Valid,Is.True,"GC frame recorder required");
                        result.memoryBefore=Profiler.GetTotalAllocatedMemoryLong();int created=0;
                        for(int i=0;i<50;i++)
                        {
                            if(mode=="first-load"&&i>0){created+=f.Loader.Created;await f.UI.ShutdownAsync();f.Dispose();await SampleFixture.Stable();f=new SampleFixture(registry);}
                            var clock=Stopwatch.StartNew();var session=await f.UI.OpenAsync<InventoryView,InventoryArgs>(new InventoryArgs("baseline-player",InventoryTab.Equipment));result.openMs[i]=clock.Elapsed.TotalMilliseconds;
                            var beforeClose=clock.Elapsed.TotalMilliseconds;var completion=await session.CloseAsync();result.closeMs[i]=clock.Elapsed.TotalMilliseconds-beforeClose;
                            await SampleFixture.Stable();result.elapsedMs[i]=clock.Elapsed.TotalMilliseconds;
                            Assert.That(completion.Errors.Count,Is.Zero);Assert.That(completion.HasPendingCleanup,Is.False);
                            int expected=mode=="cache-one-reopen"?1:0;
                            Assert.That(f.Loader.Live,Is.EqualTo(expected));Assert.That(UnityEngine.Object.FindObjectsOfType<InventoryView>(true).Length,Is.EqualTo(expected));
                            Assert.That(f.UI.GetDiagnostics().PendingCleanupCount,Is.Zero);Assert.That(f.UI.GetDiagnostics().IsolatedLeaseCount,Is.Zero);Assert.That(f.UI.GetDiagnostics().CacheLru.Count,Is.LessThanOrEqualTo(1));
                        }
                        gc.Stop();if(canvas.Valid)canvas.Stop();result.gc=Read(gc,"GC Allocated In Frame","bytes/frame");result.canvas=Read(canvas,canvasName,"nanoseconds/frame");
                        result.created=created+f.Loader.Created;result.memoryAfter=Profiler.GetTotalAllocatedMemoryLong();result.liveLeases=f.Loader.Live;result.liveInstances=UnityEngine.Object.FindObjectsOfType<InventoryView>(true).Length;result.pendingCleanup=f.UI.GetDiagnostics().PendingCleanupCount;
                        Assert.That(result.gc.samples,Is.GreaterThan(0));Assert.That(result.created,Is.EqualTo(mode=="first-load"?50:mode=="warm-release-rebuild"?51:1));
                        await f.UI.ClearCacheAsync();await SampleFixture.Stable();Assert.That(f.Loader.Live,Is.Zero);Assert.That(UnityEngine.Object.FindObjectsOfType<InventoryView>(true).Length,Is.Zero);
                        result.postClearLiveLeases=f.Loader.Live;result.postClearLiveInstances=UnityEngine.Object.FindObjectsOfType<InventoryView>(true).Length;
                        SampleFixture.Trace("performance-"+mode,result);
                    }
                    finally{gc.Dispose();canvas.Dispose();if(f!=null){await f.UI.ShutdownAsync();f.Dispose();await SampleFixture.Stable();}UnityEngine.Object.Destroy(registry);UnityEngine.Object.Destroy(provider);}
                }
                // A zero-capacity Context must not retain CacheOne resources.
                using(var zero=new SampleFixture(capacity:0))try{var s=await zero.UI.OpenAsync<InventoryView,InventoryArgs>(new InventoryArgs("capacity",InventoryTab.Items));await s.CloseAsync();await SampleFixture.Stable();Assert.That(zero.Loader.Live,Is.Zero);Assert.That(zero.UI.GetDiagnostics().CacheLru.Count,Is.Zero);}finally{await zero.UI.ShutdownAsync();}
                SampleFixture.Trace(nameof(Modal_ThreeModes_FiftyCycles),"50 first-load, 50 warmed Release, 50 CacheOne; raw per-frame GC and Canvas samples; actual leases/instances; zero-capacity and explicit clear release all");
            }
            finally{SceneManager.SetActiveScene(previous);await SceneManager.UnloadSceneAsync(scene);}
        });
    }
}
#endif
