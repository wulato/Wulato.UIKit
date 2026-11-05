using System;
using System.Threading;
namespace Wulato.UIKit
{
    /// <summary>Context stop signal. Callbacks must not wait for commands on this Context.</summary>
    public interface IUIContextLifetime
    {
        /// <summary>Canceled after stop state is committed and before owned services are disposed.</summary>
        CancellationToken Stopping { get; }
    }
    internal sealed class UIContextLifetime : IUIContextLifetime
    {
        internal readonly CancellationTokenSource Source = new CancellationTokenSource();
        public CancellationToken Stopping { get; }
        internal UIContextLifetime() { Stopping = Source.Token; }
    }
}
