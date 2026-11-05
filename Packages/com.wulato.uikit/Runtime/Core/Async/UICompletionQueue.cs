using System;
using System.Collections.Generic;
namespace Wulato.UIKit
{
    internal sealed class UICompletionQueue
    {
        readonly Queue<Action> pending=new Queue<Action>();
        readonly Action<Exception> onError;
        bool draining;
        internal int PendingCount => pending.Count;
        public UICompletionQueue(Action<Exception> onError=null) { this.onError=onError; }
        public void Post(Action action) { if(action==null)throw new ArgumentNullException(nameof(action));pending.Enqueue(action); }
        public void Drain()
        {
            if(draining)return;
            draining=true;
            try { while(pending.Count!=0) { try { pending.Dequeue()(); } catch(Exception e) { try { onError?.Invoke(e); } catch { /* A failed receiver must not starve later completions. */ } } } }
            finally { draining=false; }
        }
    }
}
