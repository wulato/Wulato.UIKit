using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;

namespace Wulato.UIKit
{
    public sealed class UniTaskUIAsyncScheduler : IUIAsyncScheduler
    {
        public double UnscaledTime => Time.realtimeSinceStartupAsDouble;
        public async UniTask SwitchToMainThreadAsync(CancellationToken cancellationToken) => await UniTask.SwitchToMainThread(cancellationToken);
        public UniTask NextFrameAsync(CancellationToken cancellationToken) => UniTask.NextFrame(PlayerLoopTiming.Update, cancellationToken);
        public UniTask DelayAsync(TimeSpan duration, bool ignoreTimeScale, CancellationToken cancellationToken)
            => UniTask.Delay(duration, ignoreTimeScale ? DelayType.Realtime : DelayType.DeltaTime, cancellationToken: cancellationToken);
    }
}
