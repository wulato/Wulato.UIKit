using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;

namespace Wulato.UIKit
{
    /// <summary>Synchronous Session binding notifications, called on the main thread.</summary>
    public interface IUISessionLifecycleObserver
    {
        IDisposable OnPreparing(UIViewBase view,UIArguments args,UISessionScope session);
        void OnTerminating(UISessionScope session,UICloseReason reason);
        void OnTerminated(UICompletion completion);
    }
}
