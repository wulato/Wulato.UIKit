#if UIKIT_PHASE2_SMOKE
using System;
using System.Linq;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;
using Wulato.UIKit.Navigation;
using Wulato.UIKit.Dialogs;
using Wulato.UIKit.PopupQueue;
using Wulato.UIKit.Presentation;
using Wulato.UIKit.Samples.BusinessFlow;
namespace Wulato.UIKit.Acceptance
{
    /// <summary>Actual IL2CPP extension paths; native touch remains independently required by TouchClose.</summary>
    public static class PhaseTwoSmoke
    {
        sealed class Pair:INavigationTransition
        {
            internal bool Fail;
            public UITransitionWrites FromWrites=>UITransitionWrites.None;public UITransitionWrites ToWrites=>UITransitionWrites.None;
            public UniTask PlayAsync(UINavigationTransitionContext context,CancellationToken ct)=>Fail?UniTask.FromException(new InvalidOperationException("expected navigation rollback")):UniTask.CompletedTask;
            public void Stop(UINavigationTransitionContext context){}public void Reset(UINavigationTransitionContext context){}
        }
        sealed class Sink:IUIErrorSink{public void Report(UIException error){}}
        static void Require(bool value,string reason){if(!value)throw new InvalidOperationException(reason);}
        static bool Open(IUIContext ui,string key)=>ui.GetDiagnostics().Sessions.Any(s=>s.Type.Value==key&&s.State==UIState.Open);
        public static async UniTask RunAsync(UIRoot root,UIRegistry registry,Action<string> passed)
        {
            Require(registry!=null,"phase2 registry");var pair=new Pair();var presenters=new BusinessFlowDemo.PresenterFactory();
            var ui=new UIContextBuilder(root,registry).WithErrorSink(new Sink()).UseNavigation(pair).UsePresentation(presenters)
                .UseDialogs(new DialogRegistry().Register<ConfirmRequest,bool>(new UIViewTypeKey("flow-confirm"))).UsePopupQueue().Build();
            try
            {
                var nav=ui.GetService<IUINavigator>();await nav.PushAsync<HomePage,FlowActions>(default);await nav.PushAsync<SettingsPage,FlowActions>(default);
                Require(await nav.BackAsync(),"navigation back");Require(nav.History.Count==1&&Open(ui,"flow-home"),"navigation history");passed("NavigationPushBack");
                pair.Fail=true;bool failed=false;
                try{await nav.PushAsync<SettingsPage,FlowActions>(default);}catch(UIException error){failed=error.Code==UIErrorCode.TransitionFailed;}
                pair.Fail=false;Require(failed&&nav.History.Count==1&&Open(ui,"flow-home"),"navigation rollback");passed("NavigationRollback");
                using(var caller=new CancellationTokenSource())
                {
                    var dialog=ui.GetService<IUIDialogs>().ShowAsync(new ConfirmRequest(),caller.Token);await UniTask.WaitUntil(()=>Open(ui,"flow-confirm"));caller.Cancel();bool canceled=false;
                    try{await dialog;}catch(OperationCanceledException){canceled=true;}
                    Require(canceled&&!Open(ui,"flow-confirm"),"dialog cancellation after open");passed("DialogTokenCancel");
                }
                var queue=ui.GetService<IUIPopupQueue>();var first=queue.AcquirePause();var second=queue.AcquirePause();
                var reward=queue.Enqueue(new BusinessFlowDemo.RewardRequest(7),100,"batch");first.Dispose();Require(!Open(ui,"flow-reward"),"multi-source pause");second.Dispose();
                await UniTask.WaitUntil(()=>Open(ui,"flow-reward"));var instance=UnityEngine.Object.FindObjectOfType<RewardPage>().GetInstanceID();
                await ui.CloseAllOfTypeAsync<RewardPage>();Require((await reward).Status==PopupQueueStatus.Completed,"queue Closed barrier");passed("PopupPauseResume");
                var count=presenters.Created;var reopened=queue.Enqueue(new BusinessFlowDemo.RewardRequest(8),100,"batch");await UniTask.WaitUntil(()=>Open(ui,"flow-reward"));
                Require(UnityEngine.Object.FindObjectOfType<RewardPage>().GetInstanceID()==instance&&presenters.Created>count,"cache new Session Presenter");
                await ui.CloseAllOfTypeAsync<RewardPage>();Require((await reopened).Status==PopupQueueStatus.Completed,"cached reward completion");passed("PresenterCacheReopen");
                var story=queue.AcquirePause();var system=queue.AcquirePause();var waiting=queue.Enqueue(new BusinessFlowDemo.RewardRequest(9),100,"waiting");
                var shutdown=await ui.ShutdownAsync();Require((await waiting).Status==PopupQueueStatus.ContextStopped,"paused queue shutdown");story.Dispose();system.Dispose();await queue.StopAsync();
                Require(shutdown.Errors.Count==0&&shutdown.PendingCleanupCount==0&&ui.GetDiagnostics().ActiveLeaseCount==0,"phase2 resource convergence");
                Require(presenters.Created==presenters.Released,"Presenter ownership");passed("PausedQueueShutdown");
            }
            finally{await ui.ShutdownAsync();}
        }
    }
}
#endif
