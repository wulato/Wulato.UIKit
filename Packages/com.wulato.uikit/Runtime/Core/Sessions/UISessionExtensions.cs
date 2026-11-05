using Cysharp.Threading.Tasks;
namespace Wulato.UIKit
{
    internal interface IUISessionReference { IUISession Session { get; } }
    internal static class UISessionExtensions
    {
        sealed class Typed<TView> : IUISession<TView>, IUISessionReference where TView : UIViewBase
        {
            readonly IUISession session;
            public Typed(IUISession session) { this.session=session; }
            public IUISession Session => session;
            public UISessionId Id => session.Id;
            public UIState State => session.State;
            public UniTask<UICompletion> Closed => session.Closed;
            public UniTask<UICompletion> CloseAsync(UICloseReason reason=UICloseReason.Owner) => session.CloseAsync(reason);
        }
        internal static async UniTask<IUISession<TView>> AsTyped<TView>(this UniTask<IUISession> task) where TView : UIViewBase => new Typed<TView>(await task);
    }
}
