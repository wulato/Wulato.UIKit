using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
namespace Wulato.UIKit.Tests.Support
{
    public sealed class ManualScheduler : IUIAsyncScheduler
    {
        public double UnscaledTime { get; private set; }
        readonly int mainThread=Thread.CurrentThread.ManagedThreadId;
        readonly Queue<UniTaskCompletionSource> switches=new Queue<UniTaskCompletionSource>();
        readonly List<UniTaskCompletionSource> frames = new List<UniTaskCompletionSource>();
        public UniTask SwitchToMainThreadAsync(CancellationToken ct)
        {
            ct.ThrowIfCancellationRequested();
            if(Thread.CurrentThread.ManagedThreadId==mainThread)return UniTask.CompletedTask;
            var completion=new UniTaskCompletionSource();lock(switches)switches.Enqueue(completion);return completion.Task;
        }
        public UniTask NextFrameAsync(CancellationToken ct) { var s=new UniTaskCompletionSource(); frames.Add(s); return s.Task.AttachExternalCancellation(ct); }
        public UniTask DelayAsync(TimeSpan duration,bool ignoreTimeScale,CancellationToken ct) => NextFrameAsync(ct);
        public void Advance() { UniTaskCompletionSource[] ready;lock(switches) { ready=switches.ToArray();switches.Clear(); } foreach(var s in ready)s.TrySetResult();UnscaledTime++; var copy=frames.ToArray(); frames.Clear(); foreach(var s in copy)s.TrySetResult(); }
    }
}
