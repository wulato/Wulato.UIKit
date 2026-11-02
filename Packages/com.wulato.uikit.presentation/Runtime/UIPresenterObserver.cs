using System;
namespace Wulato.UIKit.Presentation
{
    /// <summary>Session-scoped synchronous binding. External Models and the factory remain caller-owned.</summary>
    public sealed class UIPresenterObserver : IUISessionLifecycleObserver
    {
        readonly IPresenterFactory factory;
        /// <inheritdoc/>
        public UIPresenterObserver(IPresenterFactory factory){this.factory=factory??throw new ArgumentNullException(nameof(factory));}
        /// <inheritdoc/>
        public IDisposable OnPreparing(UIViewBase view,UIArguments args,UISessionScope session)
        {
            var presenter=factory.Create(view,args,session);
            if(presenter==null)return new Binding(null);
            try{presenter.Bind();return new Binding(presenter);}
            catch(Exception bindingError)
            {
                try{presenter.Dispose();}catch(Exception cleanupError){throw new AggregateException(bindingError,cleanupError);}
                throw;
            }
        }
        /// <inheritdoc/>
        public void OnTerminating(UISessionScope session,UICloseReason reason){}
        /// <inheritdoc/>
        public void OnTerminated(UICompletion completion){}
        sealed class Binding:IDisposable
        {
            IUIPresenter presenter;
            internal Binding(IUIPresenter presenter){this.presenter=presenter;}
            public void Dispose(){var value=presenter;presenter=null;value?.Dispose();}
        }
    }
    /// <summary>Opt-in Presenter composition; the factory is not disposed by the Context.</summary>
    public static class PresentationBuilderExtensions
    {
        /// <inheritdoc/>
        public static UIContextBuilder UsePresentation(this UIContextBuilder builder,IPresenterFactory factory)
            =>builder.WithServiceFactory<UIPresenterObserver>(_=>new UIPresenterObserver(factory));
    }
}
