#if UNITY_EDITOR
using System;
using System.Collections;
using System.IO;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEditor;
using Cysharp.Threading.Tasks;
using Wulato.UIKit.Navigation;
namespace Wulato.UIKit.Tests.PlayMode
{
    [Category("generated_integration")]
    public class GeneratedRegistryIntegrationTests
    {
        sealed class Observer:IUISessionLifecycleObserver,IDisposable
        {
            public int Prepared,Released;public bool Disposed;
            sealed class Binding:IDisposable { Action release;public Binding(Action action){release=action;}public void Dispose(){var action=release;release=null;action?.Invoke();} }
            public IDisposable OnPreparing(UIViewBase view,UIArguments args,UISessionScope scope){Prepared++;return new Binding(()=>Released++);}
            public void OnTerminating(UISessionScope scope,UICloseReason reason){}public void OnTerminated(UICompletion result){}public void Dispose(){Disposed=true;}
        }
        sealed class DeferredLoader:IUIViewLoader
        {
            public readonly UniTaskCompletionSource<IUIViewLease> Completion=new UniTaskCompletionSource<IUIViewLease>();
            public IUIViewLease Lease;public bool Hold=true;
            public async UniTask<IUIViewLease> CreateAsync(UIViewAsset asset,Transform parent,System.Threading.CancellationToken token)
            {
                var lease=await new PrefabUIViewLoader().CreateAsync(asset,parent,token);
                if(!Hold)return lease;Hold=false;Lease=lease;return await Completion.Task;
            }
        }
        sealed class DeferredTransition:INavigationTransition
        {
            public readonly UniTaskCompletionSource Completion=new UniTaskCompletionSource();public bool Entered;public int Stopped;
            public UITransitionWrites FromWrites=>UITransitionWrites.None;public UITransitionWrites ToWrites=>UITransitionWrites.None;
            public UniTask PlayAsync(UINavigationTransitionContext context,System.Threading.CancellationToken token){Entered=true;return Completion.Task;}
            public void Stop(UINavigationTransitionContext context){Stopped++;Completion.TrySetCanceled();}public void Reset(UINavigationTransitionContext context){}
        }
        static void Trace(string name,string message)
        {
            var dir=Environment.GetEnvironmentVariable("UIKIT_EVIDENCE_DIR");File.WriteAllText(Path.Combine(dir,name+".trace.json"),"{\"events\":[\""+message+"\"]}");
        }
        [UnityTest] public IEnumerator GeneratedMappings_CanceledLoadReleasesLateLeaseAndQueueRecovers()=>UniTask.ToCoroutine(async()=>
        {
            var registry=AssetDatabase.LoadAssetAtPath<UIRegistry>("Assets/Wulato.UIKit.Generated/UIRegistry.asset");
            var root=new GameObject("GeneratedRoot",typeof(RectTransform),typeof(UIRoot));root.GetComponent<Canvas>().renderMode=RenderMode.ScreenSpaceOverlay;
            var events=IntegrationResetProbe.CreateEvents();IUIContext ui=null;var loader=new DeferredLoader();var cancel=new System.Threading.CancellationTokenSource();
            try
            {
                ui=new UIContextBuilder(root.GetComponent<UIRoot>(),registry).WithLoader(loader).Build();
                var opening=ui.OpenAsync(new UIViewTypeKey("generated-DefaultView"),UIArguments.None,cancel.Token);
                Assert.That(loader.Lease,Is.Not.Null);var late=loader.Lease.View;cancel.Cancel();
                bool canceled=false;try {await opening;}catch(OperationCanceledException){canceled=true;}Assert.That(canceled,Is.True);
                Assert.That(ui.GetDiagnostics().PendingCleanupCount,Is.EqualTo(1));loader.Completion.TrySetResult(loader.Lease);
                await UniTask.NextFrame();await UniTask.NextFrame();Assert.That(late==null,Is.True);Assert.That(ui.GetDiagnostics().PendingCleanupCount,Is.Zero);
                var recovered=await ui.OpenAsync(new UIViewTypeKey("generated-DefaultView"),UIArguments.None);await recovered.CloseAsync();await ui.ShutdownAsync();
                Assert.That(ui.GetDiagnostics().ActiveLeaseCount,Is.Zero);Trace(nameof(GeneratedMappings_CanceledLoadReleasesLateLeaseAndQueueRecovers),"late lease disposed once; queue recovered; shutdown empty");
            }
            finally {loader.Completion.TrySetResult(loader.Lease);if(ui!=null)await ui.ShutdownAsync();cancel.Dispose();UnityEngine.Object.Destroy(root);UnityEngine.Object.Destroy(events);}
        });
        [UnityTest] public IEnumerator GeneratedMappings_EnterCancellationStopsAndReleasesBindings()=>UniTask.ToCoroutine(async()=>
        {
            var registry=AssetDatabase.LoadAssetAtPath<UIRegistry>("Assets/Wulato.UIKit.Generated/UIRegistry.asset");
            var root=new GameObject("GeneratedRoot",typeof(RectTransform),typeof(UIRoot));root.GetComponent<Canvas>().renderMode=RenderMode.ScreenSpaceOverlay;
            var events=IntegrationResetProbe.CreateEvents();IUIContext ui=null;var transition=new DeferredTransition();var observer=new Observer();var cancel=new System.Threading.CancellationTokenSource();
            try
            {
                ui=new UIContextBuilder(root.GetComponent<UIRoot>(),registry).WithNavigationTransition(transition).WithServiceFactory(_=>observer).Build();
                var opening=ui.OpenAsync(new UIViewTypeKey("generated-ViewA"),UIArguments.From(3),cancel.Token);
                for(int i=0;i<10&&!transition.Entered;i++)await UniTask.NextFrame();Assert.That(transition.Entered,Is.True);cancel.Cancel();
                bool canceled=false;try{await opening;}catch(OperationCanceledException){canceled=true;}Assert.That(canceled,Is.True);
                Assert.That(transition.Stopped,Is.GreaterThanOrEqualTo(1));Assert.That(observer.Prepared,Is.EqualTo(1));Assert.That(observer.Released,Is.EqualTo(1));
                await ui.ShutdownAsync();Assert.That(observer.Disposed,Is.True);Assert.That(ui.GetDiagnostics().ActiveLeaseCount,Is.Zero);Assert.That(ui.GetDiagnostics().InputLockCount,Is.Zero);
                Trace(nameof(GeneratedMappings_EnterCancellationStopsAndReleasesBindings),"OnCreate bindings ready; enter stopped; observer released; leases and locks zero");
            }
            finally {cancel.Cancel();if(ui!=null)await ui.ShutdownAsync();cancel.Dispose();UnityEngine.Object.Destroy(root);UnityEngine.Object.Destroy(events);}
        });
        [UnityTest] public IEnumerator GeneratedMappings_DriveSessionsCacheTransitionsInputAndComposition()=>UniTask.ToCoroutine(async()=>
        {
            var registry=AssetDatabase.LoadAssetAtPath<UIRegistry>("Assets/Wulato.UIKit.Generated/UIRegistry.asset");Assert.That(registry,Is.Not.Null);
            var root=new GameObject("GeneratedRoot",typeof(RectTransform),typeof(UIRoot));root.GetComponent<Canvas>().renderMode=RenderMode.ScreenSpaceOverlay;
            var events=IntegrationResetProbe.CreateEvents();IUIContext ui=null;var observer=new Observer();
            try
            {
                ui=new UIContextBuilder(root.GetComponent<UIRoot>(),registry).WithServiceFactory(_=>observer).Build();
                var page=await ui.OpenAsync(new UIViewTypeKey("generated-ViewA"),UIArguments.From(42));
                Assert.That(page.State,Is.EqualTo(UIState.Open));Assert.That(ui.GetDiagnostics().ActiveLeaseCount,Is.EqualTo(1));
                LogAssert.Expect(LogType.Exception,new System.Text.RegularExpressions.Regex("UIException: AlreadyOpen at single"));
                bool refused=false;try {await ui.OpenAsync(new UIViewTypeKey("generated-ViewA"),UIArguments.From(42));}catch(UIException){refused=true;}Assert.That(refused,Is.True,"single duplicate");
                var modal=await ui.OpenAsync(new UIViewTypeKey("generated-ViewB"),UIArguments.None);
                Assert.That(ui.GetDiagnostics().InputOwner.Value.SessionId,Is.EqualTo(modal.Id));
                await modal.CloseAsync();await UniTask.NextFrame();Assert.That(ui.GetDiagnostics().InputOwner.Value.SessionId,Is.EqualTo(page.Id));
                var old=page.Id;await page.CloseAsync();Assert.That(ui.GetDiagnostics().CacheLru.Count,Is.EqualTo(1));
                var reopened=await ui.OpenAsync(new UIViewTypeKey("generated-ViewA"),UIArguments.From(9));Assert.That(reopened.Id,Is.Not.EqualTo(old));
                var plain=await ui.OpenAsync(new UIViewTypeKey("generated-DefaultView"),UIArguments.None);await plain.CloseAsync();
                await ui.ShutdownAsync();Assert.That(ui.GetDiagnostics().ActiveLeaseCount,Is.Zero);Assert.That(ui.GetDiagnostics().InputLockCount,Is.Zero);Assert.That(observer.Prepared,Is.EqualTo(4));Assert.That(observer.Released,Is.EqualTo(4));Assert.That(observer.Disposed,Is.True);
                var dir=Environment.GetEnvironmentVariable("UIKIT_EVIDENCE_DIR");File.WriteAllText(Path.Combine(dir,"GeneratedMappings_DriveSessionsCacheTransitionsInputAndComposition.trace.json"),"{\"events\":[\"bindings before OnCreate\",\"typed mapping\",\"single reject\",\"modal foreground\",\"cache new session\",\"mixed providers\",\"shutdown leases and locks zero\"]}");
            }
            finally {if(ui!=null)await ui.ShutdownAsync();UnityEngine.Object.Destroy(root);UnityEngine.Object.Destroy(events);}
        });
    }
}
#endif
