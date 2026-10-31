using System;
using System.Collections.Generic;
using Cysharp.Threading.Tasks;
namespace Wulato.UIKit
{
    internal sealed class UICommandQueue
    {
        readonly Queue<Func<UniTask>> pending=new Queue<Func<UniTask>>();
        readonly UICompletionQueue notifications;
        readonly Action<Exception> onError;
        bool running;
        public UICommandQueue(UICompletionQueue notifications,Action<Exception> onError) { this.notifications=notifications;this.onError=onError; }
        public void Enqueue(Func<UniTask> command)
        {
            pending.Enqueue(command);
            if(!running)Pump().Forget();
        }
        async UniTask Pump()
        {
            running=true;
            try
            {
                while(pending.Count!=0)
                {
                    try { await pending.Dequeue()(); }
                    catch(Exception e) { onError(e); }
                    // Keep running set during synchronous user continuations. They only enqueue.
                    notifications.Drain();
                }
            }
            finally { running=false; }
        }
    }
}
