using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;

namespace Wulato.UIKit
{
    [Flags] public enum UITransitionWrites { None=0, Opacity=1, Scale=2, Position=4, Rotation=8 }
    public enum UITransitionOwner { CoreDefault, External }
    /// <summary>One Session animation surface. Visibility and business layout are outside its write set.</summary>
    public sealed class UITransitionContext
    {
        public UISessionScope Session { get; }
        public RectTransform AnimationRoot { get; }
        public CanvasGroup AnimationOpacity { get; }
        public UITransitionContext(UISessionScope session,RectTransform animationRoot,CanvasGroup animationOpacity)
        { Session=session ?? throw new ArgumentNullException(nameof(session));AnimationRoot=animationRoot;AnimationOpacity=animationOpacity; }
    }
    public interface IUITransition
    {
        UITransitionWrites Writes { get; }
        UniTask EnterAsync(UITransitionContext context,CancellationToken cancellationToken);
        UniTask ExitAsync(UITransitionContext context,CancellationToken cancellationToken);
        void Stop(UITransitionContext context);
        void Reset(UITransitionContext context);
    }
}
