#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Cysharp.Threading.Tasks;
using NUnit.Framework;
using Unity.Profiling;
using UnityEngine;
using UnityEngine.TestTools;
using Wulato.UIKit.Samples.Inventory;
namespace Wulato.UIKit.Acceptance.Samples
{
    [Category("performance"),Category("performance_markers")]
    public class ProfilerStageTests
    {
        [UnityTest] public IEnumerator Markers_CoverSynchronousStagesWithoutCrossFrameScopes()=>UniTask.ToCoroutine(async()=>
        {
            using(var f=new SampleFixture())try
            {
                // Register markers through a complete real operation before starting recorder handles.
                var warm=await f.UI.OpenAsync<InventoryView,InventoryArgs>(new InventoryArgs("fixed",InventoryTab.Items));await warm.CloseAsync();await f.UI.ClearCacheAsync();await SampleFixture.Stable();
                var recorders=new List<ProfilerRecorder>();
                try
                {
                    foreach(var name in new[]{"Load","Layout","Hooks","Transition","Cache"})recorders.Add(ProfilerRecorder.StartNew(ProfilerCategory.Scripts,"Wulato.UIKit."+name+".Sync",256));
                    var s=await f.UI.OpenAsync<InventoryView,InventoryArgs>(new InventoryArgs("fixed",InventoryTab.Items));await s.CloseAsync();await SampleFixture.Stable();
                    foreach(var recorder in recorders)
                    {
                        Assert.That(recorder.Valid,Is.True,"stage marker is registered");var values=new List<ProfilerRecorderSample>();recorder.CopyTo(values);Assert.That(values.Sum(v=>v.Count),Is.GreaterThan(0),"stage marker has real samples");
                    }
                    SampleFixture.Trace(nameof(Markers_CoverSynchronousStagesWithoutCrossFrameScopes),"load/layout/hooks/transition/cache markers recorded around synchronous work; wall time measured separately");
                }finally{foreach(var r in recorders)r.Dispose();}
            }finally{await f.UI.ShutdownAsync();}
        });
    }
}
#endif
