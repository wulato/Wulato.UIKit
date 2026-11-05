using System;
using System.Threading;
namespace Wulato.UIKit
{
    internal sealed class DefaultUIForegroundScope : UIForegroundScope
    {
        readonly UIInstanceScope resources=new UIInstanceScope();
        public override CancellationToken Token => resources.Token;
        public override void Track(IDisposable resource) => resources.Track(resource);
        public override void Dispose() => resources.Dispose();
    }
}
