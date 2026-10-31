using System;
using System.Collections.Generic;
using System.Threading;
namespace Wulato.UIKit
{
    internal sealed class UIInstanceScope : UIResourceScope
    {
        readonly CancellationTokenSource source=new CancellationTokenSource();
        readonly CancellationToken token;
        readonly List<IDisposable> resources=new List<IDisposable>();
        bool ended;
        public UIInstanceScope() { token=source.Token; }
        public override CancellationToken Token => token;
        public override void Track(IDisposable resource)
        {
            if(resource==null)throw new ArgumentNullException(nameof(resource));
            if(ended)resource.Dispose();else resources.Add(resource);
        }
        public override void Dispose() => End(null);
        internal void End(Action afterCancellation)
        {
            if(ended)return;
            ended=true;var errors=new List<Exception>();
            try { source.Cancel(); } catch(Exception e) { errors.Add(e); }
            try { afterCancellation?.Invoke(); } catch(Exception e) { errors.Add(e); }
            for(int i=resources.Count-1;i>=0;i--)try { resources[i].Dispose(); } catch(Exception e) { errors.Add(e); }
            resources.Clear();source.Dispose();
            if(errors.Count!=0)throw new AggregateException(errors);
        }
    }
}
