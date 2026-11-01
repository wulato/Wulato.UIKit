using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;

namespace Wulato.UIKit
{
    [Flags] public enum UITransitionWrites { None=0, Opacity=1, Scale=2, Position=4, Rotation=8 }
    /// <summary>One Session animation surface. Visibility and business layout are outside its write set.</summary>
    public sealed class UITransitionContext
    {
        UITransitionWrites captured;
        Vector3 scale;
        Vector2 position;
        Quaternion rotation;
        float opacity;
        public UISessionScope Session { get; }
        /// <summary>Capture immediately after layout, before the adapter's first write.</summary>
        internal void Capture(UITransitionWrites writes)
        {
            if ((writes & ~(UITransitionWrites.Opacity | UITransitionWrites.Scale | UITransitionWrites.Position | UITransitionWrites.Rotation)) != 0)
                throw new ArgumentOutOfRangeException(nameof(writes));
            captured = writes;
            if ((writes & UITransitionWrites.Scale) != 0) scale = AnimationRoot.localScale;
            if ((writes & UITransitionWrites.Position) != 0) position = AnimationRoot.anchoredPosition;
            if ((writes & UITransitionWrites.Rotation) != 0) rotation = AnimationRoot.localRotation;
            if ((writes & UITransitionWrites.Opacity) != 0) opacity = AnimationOpacity.alpha;
        }
        /// <summary>Restore only the declared animation properties; never business layout or Shell gates.</summary>
        public void RestoreBaseline()
        {
            if ((captured & UITransitionWrites.Scale) != 0) AnimationRoot.localScale = scale;
            if ((captured & UITransitionWrites.Position) != 0) AnimationRoot.anchoredPosition = position;
            if ((captured & UITransitionWrites.Rotation) != 0) AnimationRoot.localRotation = rotation;
            if ((captured & UITransitionWrites.Opacity) != 0) AnimationOpacity.alpha = opacity;
        }
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
