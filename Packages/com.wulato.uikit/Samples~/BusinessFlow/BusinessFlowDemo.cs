using System;
using Cysharp.Threading.Tasks;
using UnityEngine;
using Wulato.UIKit.Navigation;
using Wulato.UIKit.Dialogs;
using Wulato.UIKit.PopupQueue;
using Wulato.UIKit.Presentation;
namespace Wulato.UIKit.Samples.BusinessFlow
{
    public sealed class BusinessFlowDemo:MonoBehaviour
    {
        [SerializeField] UIRoot root;[SerializeField] UIRegistry registry;IUIContext ui;
        readonly PresenterFactory presenters=new PresenterFactory();
        FlowActions actions;
        public IUIContext Context=>ui;
        public UIRegistry Registry=>registry;
        public PresenterFactory Presenters=>presenters;
        public IUIContext CreateContext()
        {
            if(ui!=null)throw new InvalidOperationException("A Context already exists.");
            ui=new UIContextBuilder(root,registry)
                .UsePresentation(presenters).Build();
            actions=new FlowActions(()=>ui.PushAsync<SettingsPage,FlowActions>(actions).Forget(Debug.LogException),
                ()=>ui.BackAsync().Forget(Debug.LogException),()=>ConfirmAsync().Forget(Debug.LogException),QueueRewards);
            return ui;
        }
        async void Start(){try{CreateContext();await ui.PushAsync<HomePage,FlowActions>(actions);}catch(Exception error){Debug.LogException(error);}}
        async UniTask ConfirmAsync(){var result=await ui.GetService<IUIDialogs>().ShowAsync(new ConfirmRequest());Debug.Log("Dialog: "+result.Status);}
        void QueueRewards()
        {
            var queue=ui.GetService<IUIPopupQueue>();var story=queue.AcquirePause();var system=queue.AcquirePause();
            queue.Enqueue(new RewardRequest(1),100,"batch-1").Forget(Debug.LogException);queue.Enqueue(new RewardRequest(2),50,"batch-2").Forget(Debug.LogException);
            story.Dispose();system.Dispose();
        }
        public async UniTask ShutdownAsync(){if(ui!=null)await ui.ShutdownAsync();}
        void OnDestroy(){if(ui!=null)ui.ShutdownAsync().Forget(Debug.LogException);}
        public sealed class RewardRequest:IPopupRequest
        {public UIViewTypeKey ViewType=>new UIViewTypeKey("flow-reward");public UIArguments Arguments{get;}public RewardRequest(int batch){Arguments=UIArguments.From(batch);}}
        public sealed class PresenterFactory:IPresenterFactory
        {
            public int Created{get;private set;}public int Released{get;private set;}
            public IUIPresenter Create(UIViewBase view,UIArguments args,UISessionScope scope){Created++;return new Presenter(this);}
            sealed class Presenter:IUIPresenter
            {readonly PresenterFactory owner;public Presenter(PresenterFactory owner){this.owner=owner;}public void Bind(){}public void Dispose(){owner.Released++;}}
        }
    }
}
