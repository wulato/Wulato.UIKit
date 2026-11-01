using System;
using System.Threading;
using Cysharp.Threading.Tasks;
namespace Wulato.UIKit
{
    internal sealed class DefaultUISessionScope : UISessionScope
    {
        readonly UIInstanceScope resources=new UIInstanceScope();
        Func<CancellationToken,UniTask> layout;
        bool terminationNotified;
        public override UISessionId Id { get; }
        public override UIContextId ContextId { get; }
        public override IUIAsyncScheduler Scheduler { get; }
        public override bool IsCurrent => !Token.IsCancellationRequested;
        public override CancellationToken Token => resources.Token;
        public DefaultUISessionScope(UISessionId id,UIContextId contextId,IUIAsyncScheduler scheduler,Func<CancellationToken,UniTask> layout)
        { Id=id;ContextId=contextId;Scheduler=scheduler;this.layout=layout; }
        public override void Track(IDisposable resource) => resources.Track(resource);
        public override void Dispose()
        {
            try { resources.Dispose(); } finally { layout=null; }
        }
        internal void End(Action afterCancellation,Action<Exception> onError)
        {
            if(terminationNotified)return;
            terminationNotified=true;
            try
            {
                if(resources.Token.IsCancellationRequested)afterCancellation();
                else resources.End(afterCancellation);
            }
            catch(Exception e) { onError(e); }
            finally { layout=null; }
        }
        public override UniTask WaitLayoutAsync(CancellationToken cancellationToken)
        {
            var prepare=layout;
            if(!IsCurrent || prepare==null)return UniTask.FromCanceled(Token);
            return prepare(cancellationToken);
        }
    }
}
