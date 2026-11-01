using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;

namespace Wulato.UIKit
{
    /// <summary>First-party assembly seam. Concrete defaults are assembled later by the UGUI builder.</summary>
    internal sealed class UIRuntimeDependencies
    {
        internal Action<Action<Exception>> StopResources { get; }
        public bool EnableTransactions { get; }
        public UIRegistrySnapshot Registry { get; }
        public UIContextSettingsSnapshot Settings { get; }
        public IUIViewLoader Loader { get; }
        public IUIAsyncScheduler Scheduler { get; }
        public IUIViewHostFactory HostFactory { get; }
        public IUILayoutReady LayoutReady { get; }
        public IUITransition Transition { get; }
        public IUIInputArbiter InputArbiter { get; }
        public IUIErrorSink ErrorSink { get; }
        public IReadOnlyList<IUISessionLifecycleObserver> Observers { get; }
        public IReadOnlyDictionary<Type,object> Services { get; }
        public UIRuntimeDependencies(UIRegistrySnapshot registry,UIContextSettingsSnapshot settings,IUIViewLoader loader,
            IUIAsyncScheduler scheduler,IUIViewHostFactory hostFactory,IUILayoutReady layoutReady,IUITransition transition,
            IUIInputArbiter inputArbiter,IUIErrorSink errorSink,IEnumerable<IUISessionLifecycleObserver> observers,IDictionary<Type,object> services, bool enableTransactions = false, Action<Action<Exception>> stopResources = null)
        {
            StopResources=stopResources;EnableTransactions=enableTransactions;
            Registry=registry ?? throw new ArgumentNullException(nameof(registry));Settings=settings ?? throw new ArgumentNullException(nameof(settings));
            Loader=loader ?? throw new ArgumentNullException(nameof(loader));Scheduler=scheduler ?? throw new ArgumentNullException(nameof(scheduler));
            HostFactory=hostFactory ?? throw new ArgumentNullException(nameof(hostFactory));LayoutReady=layoutReady ?? throw new ArgumentNullException(nameof(layoutReady));
            Transition=transition ?? throw new ArgumentNullException(nameof(transition));InputArbiter=inputArbiter ?? throw new ArgumentNullException(nameof(inputArbiter));
            ErrorSink=errorSink ?? throw new ArgumentNullException(nameof(errorSink));Observers=UIFrozen.Copy(observers);
            Services=new System.Collections.ObjectModel.ReadOnlyDictionary<Type,object>(services==null ? new Dictionary<Type,object>() : new Dictionary<Type,object>(services));
        }
    }
}
