using System;
using System.Threading;
using Cysharp.Threading.Tasks;
namespace Wulato.UIKit.Tests.Support
{
    public sealed class SingleAwaitSource<T> : IUniTaskSource<T>
    {
        UniTaskCompletionSourceCore<T> core;
        public int GetResultCount { get; private set; }
        public UniTask<T> Task => new UniTask<T>(this,core.Version);
        public void Succeed(T value) => core.TrySetResult(value);
        public void Fail(Exception error) => core.TrySetException(error);
        public T GetResult(short token) { if(++GetResultCount!=1)throw new InvalidOperationException("Consumed twice"); return core.GetResult(token); }
        void IUniTaskSource.GetResult(short token) => GetResult(token);
        public UniTaskStatus GetStatus(short token) => core.GetStatus(token);
        public UniTaskStatus UnsafeGetStatus() => core.UnsafeGetStatus();
        public void OnCompleted(Action<object> continuation,object state,short token) => core.OnCompleted(continuation,state,token);
    }
}
