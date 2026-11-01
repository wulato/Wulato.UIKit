using System;
using System.Threading;
using Cysharp.Threading.Tasks;

namespace Wulato.UIKit
{
    internal sealed class UIShutdownBudget : IDisposable
    {
        readonly IUIAsyncScheduler clock;
        readonly TimeSpan duration;
        readonly CancellationTokenSource expired = new CancellationTokenSource();
        readonly CancellationToken token;
        UIWaitTimer timer;
        double deadline;
        public bool Started { get; private set; }
        public CancellationToken Token => token;
        public TimeSpan Remaining => !Started ? duration : Until(deadline, clock.UnscaledTime, duration);
        internal static TimeSpan Until(double deadline, double now, TimeSpan maximum)
        {
            var seconds = Math.Max(0, deadline - now);
            // Clamp before converting: double rounds MaxValue's ticks above Int64.MaxValue.
            if (seconds >= maximum.TotalSeconds) return maximum;
            var ticks = seconds * TimeSpan.TicksPerSecond;
            if (ticks >= TimeSpan.MaxValue.Ticks) return TimeSpan.MaxValue;
            return TimeSpan.FromTicks((long)ticks);
        }
        public UIShutdownBudget(IUIAsyncScheduler clock, TimeSpan duration)
        { this.clock = clock; this.duration = duration; token = expired.Token; }
        public void Start()
        {
            if (Started) return;
            Started = true; deadline = clock.UnscaledTime + duration.TotalSeconds;
            timer = new UIWaitTimer(clock, duration, expired);
        }
        public TimeSpan Clamp(TimeSpan stage) => Started && Remaining < stage ? Remaining : stage;
        public void Dispose() { timer?.Dispose(); expired.Dispose(); }
    }
    internal sealed class UIWaitTimer : IDisposable
    {
        readonly CancellationTokenSource stop = new CancellationTokenSource();
        readonly CancellationToken stopToken;
        readonly CancellationTokenSource expired;
        readonly bool ownsExpired;
        public CancellationToken Token { get; }
        public Exception Failure { get; private set; }
        public UIWaitTimer(IUIAsyncScheduler scheduler, TimeSpan duration, CancellationTokenSource shared = null)
        {
            expired = shared ?? new CancellationTokenSource(); ownsExpired = shared == null;
            Token = expired.Token; stopToken = stop.Token;
            Run(scheduler, duration).Forget();
        }
        async UniTask Run(IUIAsyncScheduler scheduler, TimeSpan duration)
        {
            try
            {
                if (duration > TimeSpan.Zero) await scheduler.DelayAsync(duration, true, stopToken);
                await scheduler.SwitchToMainThreadAsync(CancellationToken.None);
                if (!stopToken.IsCancellationRequested) expired.Cancel();
            }
            catch (OperationCanceledException) { }
            catch (Exception e)
            {
                Failure = e;
                await scheduler.SwitchToMainThreadAsync(CancellationToken.None);
                if (!stopToken.IsCancellationRequested) expired.Cancel();
            }
        }
        public void Dispose()
        {
            stop.Cancel(); stop.Dispose(); if (ownsExpired) expired.Dispose();
        }
    }
}
