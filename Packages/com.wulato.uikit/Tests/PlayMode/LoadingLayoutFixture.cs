using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using Cysharp.Threading.Tasks;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.UI;
using Wulato.UIKit.Tests.Support;
namespace Wulato.UIKit.Tests.PlayMode
{
    internal sealed class LoadingLayoutFixture : IDisposable, IUIAsyncScheduler, IUITransition, IUIErrorSink
    {
        public readonly UIRoot Root;
        public readonly UIViewHostFactory Hosts;
        public readonly List<IUIViewLease> Leases = new List<IUIViewLease>();
        public readonly List<IUIViewHost> CreatedHosts = new List<IUIViewHost>();
        public UIContextEngine UI;
        public T4ProbeView LastView;
        public int Releases, EnterFrame;
        public Vector2 SizeAtEnter;
        readonly List<GameObject> owned = new List<GameObject>();
        public LoadingLayoutFixture(bool camera = false, bool safeArea = false)
        {
            T4ProbeView.ClearProbe();
            var go = Own(new GameObject("T4EmptyCanvas", typeof(RectTransform), typeof(UIRoot)));
            Root = go.GetComponent<UIRoot>();
            var canvas = go.GetComponent<Canvas>();
            canvas.renderMode = camera ? RenderMode.ScreenSpaceCamera : RenderMode.ScreenSpaceOverlay;
            if (camera)
            {
                var cameraObject = Own(new GameObject("T4Camera", typeof(Camera)));
                cameraObject.transform.position = new Vector3(0, 0, -10);
                canvas.worldCamera = cameraObject.GetComponent<Camera>(); canvas.planeDistance = 5;
            }
            if (safeArea)
            {
                var safe = new GameObject("AuthoredSafeArea", typeof(RectTransform)).GetComponent<RectTransform>();
                safe.SetParent(go.transform, false); safe.anchorMin = new Vector2(.1f, .2f);
                safe.anchorMax = new Vector2(.85f, .9f); safe.offsetMin = safe.offsetMax = Vector2.zero;
                Root.SafeArea = safe;
            }
            Hosts = new UIViewHostFactory(Root);
        }
        public GameObject Own(GameObject go) { owned.Add(go); return go; }
        public static GameObject Prefab => Resources.Load<GameObject>("T4/Probe");
        public IUIViewHost NewHost() { var host = Hosts.Create(UILayer.Page); CreatedHosts.Add(host); return host; }
        public async UniTask<IUIViewLease> Load(IUIViewHost host, bool resources = false)
        {
            var lease = await new ResourcesUIViewLoader().CreateAsync(resources ? UIViewAsset.Resources("T4/Probe") : UIViewAsset.Direct(Prefab), host.InactiveParent, CancellationToken.None);
            Leases.Add(lease); LastView = (T4ProbeView)lease.View; return lease;
        }
        public UIContextEngine CreateEngine()
        {
            var registry = new UIRegistryBuilder(); registry.Add<T4ProbeView>(new UIViewTypeKey("t4"), UIViewAsset.Direct(Prefab));
            UI = new UIContextEngine(new UIContextId(Guid.NewGuid()), new UIRuntimeDependencies(registry.Build(),
                new UIContextSettings().CopyValidated(), new CountingLoader(this), this, Hosts, new UILayoutReady(), this,
                new FakeInputArbiter(), this, null, null));
            return UI;
        }
        sealed class CountingLoader : IUIViewLoader
        {
            readonly LoadingLayoutFixture fixture;
            public CountingLoader(LoadingLayoutFixture fixture) { this.fixture = fixture; }
            public async UniTask<IUIViewLease> CreateAsync(UIViewAsset asset, Transform parent, CancellationToken ct)
            {
                var inner = await new ResourcesUIViewLoader().CreateAsync(asset, parent, ct);
                fixture.LastView = (T4ProbeView)inner.View;
                return new CountingLease(fixture, inner);
            }
        }
        sealed class CountingLease : IUIViewLease
        {
            readonly LoadingLayoutFixture fixture; readonly Transform animationParent; IUIViewLease lease;
            public CountingLease(LoadingLayoutFixture fixture, IUIViewLease lease) { this.fixture = fixture; this.lease = lease; animationParent = lease.View.transform.parent; }
            public UIViewBase View => lease?.View;
            public void Dispose()
            {
                if (lease == null) return;
                T4ProbeView.Events.Add("lease-release");
                Assert.That(lease.View.transform.parent, Is.Not.EqualTo(animationParent), "View must be detached before lease release");
                Assert.That(animationParent, Is.Not.Null, "Wrapper must remain alive until lease release is attempted");
                Assert.That(lease.View.gameObject.activeInHierarchy, Is.False);
                T4ProbeView.Measurements.Add("release:detached=" + (lease.View.transform.parent != animationParent) + ":wrapper-alive=" + (animationParent != null));
                fixture.Releases++; lease.Dispose(); lease = null;
            }
        }
        public UniTask SwitchToMainThreadAsync(CancellationToken ct) { ct.ThrowIfCancellationRequested(); return UniTask.CompletedTask; }
        public UniTask NextFrameAsync(CancellationToken ct) => UniTask.NextFrame(PlayerLoopTiming.Update, ct);
        public UniTask DelayAsync(TimeSpan delay, bool ignoreTimeScale, CancellationToken ct) => UniTask.Delay(delay, ignoreTimeScale, cancellationToken: ct);
        public double UnscaledTime => Time.realtimeSinceStartupAsDouble;
        public UITransitionWrites Writes => UITransitionWrites.None;
        public UniTask EnterAsync(UITransitionContext ctx, CancellationToken ct) { EnterFrame = Time.frameCount; SizeAtEnter = LastView.SerializedChild.rect.size; T4ProbeView.Measurements.Add("enter:frame=" + EnterFrame + ":content-size=" + SizeAtEnter); return UniTask.CompletedTask; }
        public UniTask ExitAsync(UITransitionContext ctx, CancellationToken ct) => UniTask.CompletedTask;
        public void Stop(UITransitionContext ctx) { }
        public void Reset(UITransitionContext ctx) { }
        public void Report(UIException error) { }
        public async UniTask DisposeAsync()
        {
            if (UI != null) await UI.ShutdownAsync();
            Dispose();
        }
        public void Dispose()
        {
            T4ProbeView.Opening = null; T4ProbeView.Disposing = null; T4ProbeView.Enabled = null;
            if (UI != null) UI.ShutdownAsync().GetAwaiter().GetResult();
            foreach (var host in CreatedHosts) host.DetachView();
            foreach (var lease in Leases) lease.Dispose();
            foreach (var host in CreatedHosts) host.Dispose();
            Hosts.Dispose();
            foreach (var go in owned) if (go != null) UnityEngine.Object.DestroyImmediate(go);
        }
    }
    public abstract class LoadingLayoutTestBase
    {
        [TearDown] public void SaveTrace()
        {
            var dir = Environment.GetEnvironmentVariable("UIKIT_EVIDENCE_DIR");
            if (string.IsNullOrEmpty(dir)) return;
            Directory.CreateDirectory(dir);
            File.WriteAllText(Path.Combine(dir, TestContext.CurrentContext.Test.Name + ".trace.json"), JsonUtility.ToJson(new Trace { events = T4ProbeView.Events.ToArray(), measurements = T4ProbeView.Measurements.ToArray(), frame = Time.frameCount }));
        }
        [Serializable] sealed class Trace { public string[] events, measurements; public int frame; }
    }
}
