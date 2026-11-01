using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;
namespace Wulato.UIKit
{
    /// <summary>Unscaled frame animation with synchronous Stop. Apply must write only the declared surface properties.</summary>
    public sealed class UITransitionRunner : IUITransition
    {
        sealed class Playback
        {
            internal readonly UniTaskCompletionSource Completion = new UniTaskCompletionSource();
            internal readonly CancellationTokenSource Stop = new CancellationTokenSource();
            internal bool Active = true;
        }
        readonly Dictionary<UITransitionContext, Playback> active = new Dictionary<UITransitionContext, Playback>();
        readonly TimeSpan duration;
        readonly Action<UITransitionContext, float, bool> apply;
        public UITransitionWrites Writes { get; }
        public UITransitionRunner(TimeSpan duration, UITransitionWrites writes, Action<UITransitionContext, float, bool> apply)
        {
            if (duration < TimeSpan.Zero) throw new ArgumentOutOfRangeException(nameof(duration));
            if ((writes & ~(UITransitionWrites.Opacity | UITransitionWrites.Scale | UITransitionWrites.Position | UITransitionWrites.Rotation)) != 0)
                throw new ArgumentOutOfRangeException(nameof(writes));
            this.duration = duration; Writes = writes; this.apply = apply ?? throw new ArgumentNullException(nameof(apply));
        }
        public UniTask EnterAsync(UITransitionContext context, CancellationToken cancellationToken) => Play(context, true, cancellationToken);
        public UniTask ExitAsync(UITransitionContext context, CancellationToken cancellationToken) => Play(context, false, cancellationToken);
        UniTask Play(UITransitionContext context, bool entering, CancellationToken token)
        {
            if (context == null) throw new ArgumentNullException(nameof(context));
            token.ThrowIfCancellationRequested();
            if (active.ContainsKey(context)) throw new InvalidOperationException("This surface is already animating.");
            var playback = new Playback(); active.Add(context, playback);
            Animate(context, playback, entering, token).Forget();
            return playback.Completion.Task;
        }
        async UniTask Animate(UITransitionContext context, Playback playback, bool entering, CancellationToken token)
        {
            try
            {
                using (var linked = CancellationTokenSource.CreateLinkedTokenSource(token, playback.Stop.Token))
                {
                    double start = Time.realtimeSinceStartupAsDouble;
                    float progress = duration == TimeSpan.Zero ? 1 : 0;
                    while (playback.Active)
                    {
                        linked.Token.ThrowIfCancellationRequested();
                        apply(context, progress, entering);
                        if (!playback.Active) return;
                        if (progress >= 1) break;
                        await UniTask.NextFrame(PlayerLoopTiming.Update, linked.Token);
                        progress = (float)Math.Min(1, (Time.realtimeSinceStartupAsDouble - start) / duration.TotalSeconds);
                    }
                    if (playback.Active)
                    { playback.Active = false; Remove(context, playback); playback.Completion.TrySetResult(); }
                }
            }
            catch (OperationCanceledException e)
            { playback.Active = false; Remove(context, playback); playback.Completion.TrySetCanceled(e.CancellationToken); }
            catch (Exception e)
            { playback.Active = false; Remove(context, playback); playback.Completion.TrySetException(e); }
            finally { playback.Stop.Dispose(); }
        }
        void Remove(UITransitionContext context, Playback playback)
        {
            if (active.TryGetValue(context, out var current) && ReferenceEquals(current, playback)) active.Remove(context);
        }
        public void Stop(UITransitionContext context)
        {
            if (!active.TryGetValue(context, out var playback)) return;
            playback.Active = false; active.Remove(context);
            playback.Completion.TrySetCanceled();
            playback.Stop.Cancel();
        }
        public void Reset(UITransitionContext context) => context.RestoreBaseline();
    }
}
