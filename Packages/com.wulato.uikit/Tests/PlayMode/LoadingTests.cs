using System;
using System.Collections;
using System.Linq;
using System.Threading;
using Cysharp.Threading.Tasks;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UI;
using Wulato.UIKit.Tests.Support;
namespace Wulato.UIKit.Tests.PlayMode
{
    [Category("loading_layout")]
    public class LoadingTests : LoadingLayoutTestBase
    {
        [UnityTest] public IEnumerator LeaseDisposeTwice_ReleasesOnce() => UniTask.ToCoroutine(async () =>
        {
            using (var f = new LoadingLayoutFixture())
            {
                var host = f.NewHost(); var lease = await f.Load(host);
                lease.View.InvokeCreate(); host.AttachHidden(lease.View);
                host.DetachView(); lease.Dispose(); lease.Dispose();
                Assert.That(lease.View, Is.Null);
                await UniTask.NextFrame();
                Assert.That(T4ProbeView.DestroyCount, Is.EqualTo(1));
            }
        });
        [UnityTest] public IEnumerator LoaderFailsBeforeHandoff_OwnsPartialResources() => UniTask.ToCoroutine(async () =>
        {
            using (var f = new LoadingLayoutFixture())
            {
                var host = f.NewHost();
                var invalid = f.Own(new GameObject("T4MissingView", typeof(RectTransform)));
                Exception error = null;
                try { await new PrefabUIViewLoader().CreateAsync(UIViewAsset.Direct(invalid), host.InactiveParent, CancellationToken.None); }
                catch (Exception e) { error = e; }
                Assert.That(error, Is.Not.Null);
                Assert.That(host.InactiveParent.childCount, Is.Zero);
                await UniTask.NextFrame();
                Assert.That(Resources.FindObjectsOfTypeAll<GameObject>().Count(go => go.name == "T4MissingView(Clone)" && go.scene.IsValid()), Is.Zero);
            }
        });
        [UnityTest] public IEnumerator InactiveLoad_DoesNotRunAwake() => UniTask.ToCoroutine(async () =>
        {
            using (var f = new LoadingLayoutFixture())
            {
                var host = f.NewHost(); var lease = await f.Load(host);
                var probe = (T4ProbeView)lease.View;
                Assert.That(probe.gameObject.activeSelf, Is.True);
                Assert.That(probe.gameObject.activeInHierarchy, Is.False);
                Assert.That(probe.Awoke, Is.False); Assert.That(T4ProbeView.Events, Is.Empty);
                Assert.That(probe.SerializedChild, Is.Not.Null);
                Assert.That(probe.transform.parent, Is.SameAs(host.InactiveParent));
                probe.InvokeCreate(); host.AttachHidden(probe);
                Assert.That(probe.CreatedBeforeAwake, Is.True);
                Assert.That(T4ProbeView.Events, Is.EqualTo(new[] { "create", "awake", "enable" }));
            }
        });
        [UnityTest] public IEnumerator ResourcesLoad_UsesSameInactiveLeaseContract() => UniTask.ToCoroutine(async () =>
        {
            using (var f = new LoadingLayoutFixture())
            {
                var host = f.NewHost(); var lease = await f.Load(host, true);
                Assert.That(lease.View, Is.TypeOf<T4ProbeView>());
                Assert.That(((T4ProbeView)lease.View).Awoke, Is.False);
                Assert.That(lease.View.transform.parent, Is.SameAs(host.InactiveParent));
                host.DetachView(); lease.Dispose();
                Assert.That(LoadingLayoutFixture.Prefab, Is.Not.Null, "Releasing an instance must not destroy its shared prefab");
            }
        });
        [UnityTest] public IEnumerator MissingResources_FailsWithoutInstance() => UniTask.ToCoroutine(async () =>
        {
            using (var f = new LoadingLayoutFixture())
            {
                var host = f.NewHost(); Exception failure = null;
                try { await new ResourcesUIViewLoader().CreateAsync(UIViewAsset.Resources("T4/DoesNotExist"), host.InactiveParent, CancellationToken.None); }
                catch (Exception e) { failure = e; }
                Assert.That(failure, Is.Not.Null); Assert.That(host.InactiveParent.childCount, Is.Zero);
            }
        });
        [UnityTest] public IEnumerator ActiveParentAndInactivePrefab_AreRejectedWithoutAwake() => UniTask.ToCoroutine(async () =>
        {
            using (var f = new LoadingLayoutFixture())
            {
                var loader = new PrefabUIViewLoader(); Exception first = null, second = null;
                try { await loader.CreateAsync(UIViewAsset.Direct(LoadingLayoutFixture.Prefab), f.Root.GetLayer(UILayer.Page), CancellationToken.None); }
                catch (Exception e) { first = e; }
                var inactive = f.Own(UnityEngine.Object.Instantiate(LoadingLayoutFixture.Prefab, f.NewHost().InactiveParent, false));
                inactive.SetActive(false); T4ProbeView.ClearProbe();
                try { await loader.CreateAsync(UIViewAsset.Direct(inactive), f.NewHost().InactiveParent, CancellationToken.None); }
                catch (Exception e) { second = e; }
                Assert.That(first, Is.Not.Null); Assert.That(second, Is.Not.Null); Assert.That(T4ProbeView.Events, Is.Empty);
            }
        });
        [UnityTest] public IEnumerator CanceledLoad_DoesNotCreateInstance() => UniTask.ToCoroutine(async () =>
        {
            using (var f = new LoadingLayoutFixture()) using (var ct = new CancellationTokenSource())
            {
                var host = f.NewHost(); ct.Cancel(); bool canceled = false;
                try { await new ResourcesUIViewLoader().CreateAsync(UIViewAsset.Resources("T4/Probe"), host.InactiveParent, ct.Token); }
                catch (OperationCanceledException) { canceled = true; }
                Assert.That(canceled, Is.True); Assert.That(host.InactiveParent.childCount, Is.Zero);
            }
        });
        [UnityTest] public IEnumerator RealEngine_CreatePrecedesAwake_AndDetachPrecedesRelease() => UniTask.ToCoroutine(async () =>
        {
            await using (var f = new LoadingLayoutFixture())
            {
                var ui = f.CreateEngine(); var session = await ui.OpenAsync<T4ProbeView>();
                Assert.That(f.LastView.CreatedBeforeAwake, Is.True);
                Assert.That(T4ProbeView.Events.Take(4), Is.EqualTo(new[] { "create", "awake", "enable", "open" }));
                var result = await session.CloseAsync();
                Assert.That(result.Errors, Is.Empty); Assert.That(f.Releases, Is.EqualTo(1));
                Assert.That(T4ProbeView.Events.IndexOf("dispose"), Is.LessThan(T4ProbeView.Events.IndexOf("lease-release")));
                Assert.That(ui.GetDiagnostics().ActiveLeaseCount, Is.Zero);
            }
        });
    }
}
