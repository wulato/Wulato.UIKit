using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
namespace Wulato.UIKit.Tests.Support
{
    public sealed class ManualScheduler : IUIAsyncScheduler
    {
        public double UnscaledTime { get; private set; }
        readonly int mainThread = Thread.CurrentThread.ManagedThreadId;
        readonly Queue<UniTaskCompletionSource> switches = new Queue<UniTaskCompletionSource>();
        readonly List<UniTaskCompletionSource> frames = new List<UniTaskCompletionSource>();
        readonly List<Timer> timers = new List<Timer>();
        sealed class Timer { public double Due; public UniTaskCompletionSource Source; public CancellationTokenRegistration Registration; }
        public UniTask SwitchToMainThreadAsync(CancellationToken ct)
        {
            ct.ThrowIfCancellationRequested();
            if (Thread.CurrentThread.ManagedThreadId == mainThread) return UniTask.CompletedTask;
            var completion = new UniTaskCompletionSource(); lock (switches) switches.Enqueue(completion); return completion.Task;
        }
        public UniTask NextFrameAsync(CancellationToken ct)
        {
            var source = new UniTaskCompletionSource(); frames.Add(source); return source.Task.AttachExternalCancellation(ct);
        }
        public UniTask DelayAsync(TimeSpan duration, bool ignoreTimeScale, CancellationToken ct)
        {
            if (ct.IsCancellationRequested) return UniTask.FromCanceled(ct);
            var timer = new Timer { Due = UnscaledTime + duration.TotalSeconds, Source = new UniTaskCompletionSource() };
            timers.Add(timer);
            timer.Registration = ct.Register(() => { timers.Remove(timer); timer.Source.TrySetCanceled(ct); });
            return timer.Source.Task;
        }
        public void Advance(double seconds = 1)
        {
            UniTaskCompletionSource[] ready;
            lock (switches) { ready = switches.ToArray(); switches.Clear(); }
            foreach (var source in ready) source.TrySetResult();
            UnscaledTime += seconds;
            var copy = frames.ToArray(); frames.Clear(); foreach (var source in copy) source.TrySetResult();
            foreach (var timer in timers.ToArray())
            {
                if (timer.Due > UnscaledTime || !timers.Remove(timer)) continue;
                timer.Registration.Dispose(); timer.Source.TrySetResult();
            }
        }
    }
}
