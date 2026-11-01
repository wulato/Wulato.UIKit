using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;
using Wulato.UIKit.Tests.Support;
namespace Wulato.UIKit.Tests.PlayMode
{
    internal sealed class CleanupRuntimeFixture : IDisposable, IUITransition, IUIErrorSink
    {
        public readonly GameObject RootObject;
        public readonly UIViewHostFactory Hosts;
        public readonly FakeInputArbiter Input = new FakeInputArbiter();
        public readonly ControlledLoader Loader = new ControlledLoader();
        public readonly ManualScheduler Clock = new ManualScheduler();
        public readonly List<UIException> Errors = new List<UIException>();
        public readonly UIContextEngine UI;
        public bool SinkThrows;
        public Action<UIException> Reporting;
        public CleanupRuntimeFixture(bool cache = false, IUITransition transition = null, bool transactions = false, bool multi = false)
        {
            ProbeView.ResetProbeState();
            RootObject = new GameObject("T5Root", typeof(RectTransform), typeof(UIRoot));
            RootObject.GetComponent<Canvas>().renderMode = RenderMode.ScreenSpaceOverlay;
            Hosts = new UIViewHostFactory(RootObject.GetComponent<UIRoot>());
            var registry = new UIRegistryBuilder();
            registry.Add<ProbeView>(new UIViewTypeKey("probe"), UIViewAsset.Resources("probe"), multiplicity: multi ? UIMultiplicity.Multi : UIMultiplicity.Single, closePolicy: cache ? UIClosePolicy.CacheOne : UIClosePolicy.Release);
            UI = new UIContextEngine(new UIContextId(Guid.NewGuid()), new UIRuntimeDependencies(registry.Build(), new UIContextSettings().CopyValidated(),
                Loader, Clock, Hosts, new UILayoutReady(), transition ?? this, Input, this, null, null, transactions));
        }
        public UIRuntimeHost Runtime => (UIRuntimeHost)((IUIRuntimeHostProvider)Hosts).RuntimeHost;
        public UITransitionWrites Writes => UITransitionWrites.None;
        public UniTask EnterAsync(UITransitionContext context, CancellationToken ct) => UniTask.CompletedTask;
        public UniTask ExitAsync(UITransitionContext context, CancellationToken ct) => UniTask.CompletedTask;
        public void Stop(UITransitionContext context) { }
        public void Reset(UITransitionContext context) { }
        public void Report(UIException error) { Errors.Add(error); Reporting?.Invoke(error); if (SinkThrows) throw new Exception("sink"); }
        public async UniTask DisposeAsync()
        {
            // Failure cleanup must not hang when an assertion interrupted a manual-clock scenario.
            var stop = UI.ShutdownAsync();
            if (!stop.Status.IsCompleted()) { await UniTask.NextFrame(); Clock.Advance(100); }
            await stop; Dispose();
        }
        public void Dispose()
        {
            ProbeView.Open = null; ProbeView.Close = null;
            Hosts.Dispose(); if (RootObject != null) UnityEngine.Object.DestroyImmediate(RootObject);
        }
    }
}
