using System.Threading;
using Cysharp.Threading.Tasks;
namespace Wulato.UIKit
{
    /// <summary>The default transition completes in the calling frame.</summary>
    public sealed class ImmediateUITransition : IUITransition
    {
        public UITransitionWrites Writes => UITransitionWrites.None;
        public UniTask EnterAsync(UITransitionContext context, CancellationToken cancellationToken)
        { cancellationToken.ThrowIfCancellationRequested(); return UniTask.CompletedTask; }
        public UniTask ExitAsync(UITransitionContext context, CancellationToken cancellationToken)
        { cancellationToken.ThrowIfCancellationRequested(); return UniTask.CompletedTask; }
        public void Stop(UITransitionContext context) { }
        public void Reset(UITransitionContext context) { }
    }
}
