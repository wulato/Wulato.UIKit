using System;
#if UIKIT_05_SMOKE
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;
using Wulato.UIKit.Navigation;
using Wulato.UIKit.PopupQueue;
using Wulato.UIKit.Samples.BusinessFlow;
using Wulato.UIKit.Samples.Addressables;
#endif
namespace Wulato.UIKit.Acceptance
{
    [Serializable] public sealed class MobilePerformance
    {
        public string model,os,cpu,gpu,unity,architecture,scope="Player main-thread Open/Close wall time; five warmup, fifty cached cycles; not a game FPS budget";
        public int warmup=5,samples=50,processorCount,systemMemoryMb,activeLeasesAfterClear,pendingCleanupAfterClear,inputLocksAfterClear;
        public double p50Ms,p95Ms,maxMs;
        public long managedBefore,managedAfter;
    }
}
#if UIKIT_05_SMOKE
namespace Wulato.UIKit.Acceptance
{
    public static class Mobile05Smoke
    {
        static void Require(bool value,string message){if(!value)throw new InvalidOperationException(message);}
        sealed class DelayLoader : IUIViewLoader
        {
            readonly AddressablesUIViewLoader real=new AddressablesUIViewLoader();
            internal readonly UniTaskCompletionSource Started=new UniTaskCompletionSource(),Release=new UniTaskCompletionSource();
            internal int Disposed;
            public async UniTask<IUIViewLease> CreateAsync(UIViewAsset asset,Transform parent,CancellationToken ct)
            {
                // Real SDK lease; delay only its return to exercise cancellation/late ownership.
                var lease=await real.CreateAsync(asset,parent,CancellationToken.None);
                Started.TrySetResult();await Release.Task;return new ObservedLease(lease,this);
            }
            sealed class ObservedLease:IUIViewLease
            {
                IUIViewLease inner;readonly DelayLoader owner;
                internal ObservedLease(IUIViewLease inner,DelayLoader owner){this.inner=inner;this.owner=owner;}
                public UIViewBase View=>inner.View;
                public void Dispose(){var value=inner;inner=null;if(value!=null){value.Dispose();owner.Disposed++;}}
            }
        }
        public static async UniTask RunAsync(UIRoot root,UIRegistry flow,UIRegistry addressables,Action<string> passed)
        {
            var ui=new UIContextBuilder(root,flow).Build();
            try
            {
                await ui.OpenAsync<HomePage,FlowActions>(default);await ui.PushAsync<SettingsPage,FlowActions>(default);
                Require(await ui.PopToAsync<HomePage>(),"PopTo original Home");
                Require(ui.GetService<IUINavigator>().History.Count==1,"PopTo closes upper page");
                var replacement=await ui.ReplaceAsync<SettingsPage,FlowActions>(default);
                Require(ui.GetService<IUINavigator>().History.Count==1,"Replace preserves stack depth");
                Require(!await ui.BackAsync(),"root Back is not handled");
                await replacement.CloseAsync();
                passed("PageOpenReplacePopTo");
                var old=await ui.OpenAsync<HomePage,FlowActions>(default);await old.CloseAsync();
                var current=await ui.OpenAsync<HomePage,FlowActions>(default);await old.CloseAsync();
                Require(current.State==UIState.Open&&!old.Id.Equals(current.Id),"old Session cannot close cached reopen");passed("SessionIsolation");
                var queue=ui.GetService<IUIPopupQueue>();var pause=queue.AcquirePause();
                try
                {
                    using(var cancel=new CancellationTokenSource())
                    {
                        var wait=ui.PopupAsync<RewardPage,int>(7,priority:1,deduplicationKey:"mobile-cancel",cancellationToken:cancel.Token);
                        cancel.Cancel();bool canceled=false;try{await wait;}catch(OperationCanceledException){canceled=true;}
                        Require(canceled,"queued popup token cancel");
                    }
                }finally{pause.Dispose();}
                passed("PopupTokenCancel");
            }finally{await ui.ShutdownAsync();}
            ui=new UIContextBuilder(root,addressables).Build();
            try
            {
                var first=await ui.OpenAsync<AddressableInventoryView,int>(12);var id=UnityEngine.Object.FindObjectOfType<AddressableInventoryView>().GetInstanceID();await first.CloseAsync();
                var second=await ui.OpenAsync<AddressableInventoryView,int>(24);Require(UnityEngine.Object.FindObjectOfType<AddressableInventoryView>().GetInstanceID()==id,"Addressables cache ownership");
                await second.CloseAsync();await ui.ClearCacheAsync();Require(ui.GetDiagnostics().ActiveLeaseCount==0,"Addressables clear releases leases");passed("AddressablesCacheOwnership");
                using(var cancel=new CancellationTokenSource())
                {cancel.Cancel();bool canceled=false;try{await ui.OpenAsync<AddressableInventoryView,int>(99,cancel.Token);}catch(OperationCanceledException){canceled=true;}Require(canceled&&ui.GetDiagnostics().ActiveLeaseCount==0,"pre-cancel Addressables");}
                passed("AddressablesCanceledLoad");
            }finally{await ui.ShutdownAsync();}
            var delayed=new DelayLoader();ui=new UIContextBuilder(root,addressables).WithLoader(delayed).Build();
            try
            {
                using(var cancel=new CancellationTokenSource())
                {
                    var pending=ui.OpenAsync<AddressableInventoryView,int>(42,cancel.Token);
                    await delayed.Started.Task.Timeout(TimeSpan.FromSeconds(30));cancel.Cancel();bool canceled=false;
                    try{await pending;}catch(OperationCanceledException){canceled=true;}
                    Require(canceled,"in-flight SDK lease cancellation");delayed.Release.TrySetResult();
                    await UniTask.WaitUntil(()=>delayed.Disposed==1&&ui.GetDiagnostics().PendingCleanupCount==0).Timeout(TimeSpan.FromSeconds(10));
                    Require(ui.GetDiagnostics().ActiveLeaseCount==0,"late lease released");passed("LateLeaseCleanup");
                }
            }finally{delayed.Release.TrySetResult();await ui.ShutdownAsync();}
        }
        public static async UniTask<MobilePerformance> MeasureAsync(UIRoot root,UIRegistry registry)
        {
            var report=new MobilePerformance{model=SystemInfo.deviceModel,os=SystemInfo.operatingSystem,cpu=SystemInfo.processorType,gpu=SystemInfo.graphicsDeviceName,unity=Application.unityVersion,architecture=IntPtr.Size==8?"64bit":"32bit",processorCount=SystemInfo.processorCount,systemMemoryMb=SystemInfo.systemMemorySize};
            var ui=new UIContextBuilder(root,registry).Build();var samples=new List<double>();
            try
            {
                for(int i=0;i<report.warmup;i++){var s=await ui.OpenAsync<MobileSmokeView,MobileSmokeArgs>(new MobileSmokeArgs(i));await s.CloseAsync();}
                report.managedBefore=GC.GetTotalMemory(false);
                for(int i=0;i<report.samples;i++)
                {
                    var start=Stopwatch.GetTimestamp();var s=await ui.OpenAsync<MobileSmokeView,MobileSmokeArgs>(new MobileSmokeArgs(i));await s.CloseAsync();
                    samples.Add((Stopwatch.GetTimestamp()-start)*1000d/Stopwatch.Frequency);
                }
                report.managedAfter=GC.GetTotalMemory(false);samples.Sort();report.p50Ms=samples[24];report.p95Ms=samples[47];report.maxMs=samples[49];
                await ui.ClearCacheAsync();await UniTask.NextFrame();var d=ui.GetDiagnostics();report.activeLeasesAfterClear=d.ActiveLeaseCount;report.pendingCleanupAfterClear=d.PendingCleanupCount;report.inputLocksAfterClear=d.InputLockCount;
                Require(d.ActiveLeaseCount==0&&d.PendingCleanupCount==0&&d.InputLockCount==0,"performance resource convergence");return report;
            }finally{await ui.ShutdownAsync();}
        }
    }
}
#endif
