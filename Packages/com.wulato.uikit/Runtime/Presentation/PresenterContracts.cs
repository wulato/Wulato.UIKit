using System;
namespace Wulato.UIKit.Presentation
{
    /// <summary>One Presenter per Session. Dispose unbinds and releases only Presenter-owned resources.</summary>
    public interface IUIPresenter : IDisposable
    {
        /// <summary>Bind synchronously after Awake and before OnOpenAsync.</summary>
        void Bind();
    }
    /// <summary>Return null for Views without a Presenter. Caller owns this injected factory.</summary>
    public interface IPresenterFactory
    {
        /// <summary>Create one Presenter for the Session, or null to skip this View; clean partial resources before throwing.</summary>
        IUIPresenter Create(UIViewBase view,UIArguments arguments,UISessionScope session);
    }
}
