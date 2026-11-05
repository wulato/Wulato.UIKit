using System;
using System.Collections.Generic;
using Cysharp.Threading.Tasks;
namespace Wulato.UIKit
{
    internal sealed class UICommandQueue
    {
        sealed class Command { internal Func<UniTask> Execute; internal Action Rejected; }
        readonly Queue<Command> pending=new Queue<Command>();
        readonly UICompletionQueue notifications;
        readonly Action<Exception> onError;
        bool running;
        public UICommandQueue(UICompletionQueue notifications,Action<Exception> onError) { this.notifications=notifications;this.onError=onError; }
        public void Enqueue(Func<UniTask> command, Action rejected=null)
        {
            pending.Enqueue(new Command { Execute=command, Rejected=rejected });
            if(!running)Pump().Forget();
        }
        internal void RejectPending()
        {
            while(pending.Count!=0) pending.Dequeue().Rejected?.Invoke();
        }
        async UniTask Pump()
        {
            running=true;
            try
            {
                while(pending.Count!=0)
                {
                    try { await pending.Dequeue().Execute(); }
                    catch(Exception e) { onError(e); }
                    // Keep running set during synchronous user continuations. They only enqueue.
                    notifications.Drain();
                }
            }
            finally { running=false; }
        }
    }
}
