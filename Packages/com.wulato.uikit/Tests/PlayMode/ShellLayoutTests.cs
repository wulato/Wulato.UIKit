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
    public class ShellLayoutTests : LoadingLayoutTestBase
    {
        static void AssertRectMatches(Transform actual, Transform reference)
        {
            var a = (RectTransform)actual; var b = (RectTransform)reference;
            T4ProbeView.Measurements.Add("rect:actual=" + a.rect + ":reference=" + b.rect + ":pivot=" + a.pivot);
            Assert.That(a.anchorMin, Is.EqualTo(b.anchorMin)); Assert.That(a.anchorMax, Is.EqualTo(b.anchorMax));
            Assert.That(a.pivot, Is.EqualTo(b.pivot)); Assert.That(a.sizeDelta, Is.EqualTo(b.sizeDelta));
            Assert.That(a.anchoredPosition, Is.EqualTo(b.anchoredPosition));
            Assert.That(a.localScale, Is.EqualTo(b.localScale));
            var ac = new Vector3[4]; var bc = new Vector3[4]; a.GetWorldCorners(ac); b.GetWorldCorners(bc);
            for (int i = 0; i < 4; i++) Assert.That(Vector3.Distance(ac[i], bc[i]), Is.LessThan(.02f));
        }
        async UniTask PreserveRect(bool camera, bool safe)
        {
            using (var f = new LoadingLayoutFixture(camera, safe))
            {
                var host = f.NewHost(); var lease = await f.Load(host);
                lease.View.InvokeCreate(); host.AttachHidden(lease.View);
                var layer = f.Root.GetLayer(UILayer.Page);
                var reference = f.Own(UnityEngine.Object.Instantiate(LoadingLayoutFixture.Prefab, layer, false));
                Canvas.ForceUpdateCanvases();
                AssertRectMatches(lease.View.transform, reference.transform);
                Assert.That(host.AnimationRoot.parent.parent, Is.SameAs(layer));
                foreach (RectTransform wrapper in new[] { host.AnimationRoot, (RectTransform)host.AnimationRoot.parent })
                {
                    Assert.That(wrapper.anchorMin, Is.EqualTo(Vector2.zero)); Assert.That(wrapper.anchorMax, Is.EqualTo(Vector2.one));
                    Assert.That(wrapper.offsetMin, Is.EqualTo(Vector2.zero)); Assert.That(wrapper.offsetMax, Is.EqualTo(Vector2.zero));
                    Assert.That(wrapper.pivot, Is.EqualTo(new Vector2(.5f, .5f))); Assert.That(wrapper.localScale, Is.EqualTo(Vector3.one));
                    Assert.That(wrapper.localRotation, Is.EqualTo(Quaternion.identity));
                }
            }
        }
        [UnityTest] public IEnumerator NonStretchPrefab_WrapperPreservesRect() => UniTask.ToCoroutine(() => PreserveRect(false, false));
        [UnityTest] public IEnumerator OverlaySafeArea_PreservesRect() => UniTask.ToCoroutine(() => PreserveRect(false, true));
        [UnityTest] public IEnumerator CameraMode_PreservesRect() => UniTask.ToCoroutine(() => PreserveRect(true, false));
        [UnityTest] public IEnumerator CameraSafeArea_PreservesRect() => UniTask.ToCoroutine(() => PreserveRect(true, true));
        [UnityTest] public IEnumerator CacheParentResize_UsesCurrentLayerRect() => UniTask.ToCoroutine(async () =>
        {
            using (var f = new LoadingLayoutFixture(false, true))
            {
                var host = f.NewHost(); var lease = await f.Load(host); lease.View.InvokeCreate(); host.AttachHidden(lease.View);
                host.MoveToCache(); Assert.That(lease.View.gameObject.activeInHierarchy, Is.False);
                var cache = (RectTransform)host.AnimationRoot.parent.parent;
                cache.anchorMin = cache.anchorMax = Vector2.zero; cache.sizeDelta = new Vector2(17, 29);
                f.Root.SafeArea.anchorMax = new Vector2(.6f, .55f);
                host.AttachHidden(lease.View); await new UILayoutReady().WaitLayoutAsync(lease.View, CancellationToken.None);
                var reference = f.Own(UnityEngine.Object.Instantiate(LoadingLayoutFixture.Prefab, f.Root.GetLayer(UILayer.Page), false));
                Canvas.ForceUpdateCanvases(); AssertRectMatches(lease.View.transform, reference.transform);
                Assert.That(T4ProbeView.Events.Count(s => s == "create"), Is.EqualTo(1));
                Assert.That(T4ProbeView.Events.Count(s => s == "awake"), Is.EqualTo(2), "One hosted instance and one direct-parent reference");
            }
        });
        [UnityTest] public IEnumerator HiddenActiveView_IsNotRaycastable_VisibleOwnerIs() => UniTask.ToCoroutine(async () =>
        {
            using (var f = new LoadingLayoutFixture())
            {
                var host = f.NewHost(); var lease = await f.Load(host); lease.View.InvokeCreate(); host.AttachHidden(lease.View);
                await new UILayoutReady().WaitLayoutAsync(lease.View, CancellationToken.None);
                var eventSystem = f.Own(new GameObject("T4Events", typeof(UnityEngine.EventSystems.EventSystem))).GetComponent<UnityEngine.EventSystems.EventSystem>();
                var point = RectTransformUtility.WorldToScreenPoint(null, ((RectTransform)lease.View.transform).TransformPoint(((RectTransform)lease.View.transform).rect.center));
                var data = new UnityEngine.EventSystems.PointerEventData(eventSystem) { position = point };
                var hits = new System.Collections.Generic.List<UnityEngine.EventSystems.RaycastResult>();
                var raycaster = f.Root.GetLayer(UILayer.Page).GetComponent<GraphicRaycaster>();
                raycaster.Raycast(data, hits); T4ProbeView.Measurements.Add("hidden-raycasts=" + hits.Count); Assert.That(hits.Any(h => h.gameObject == lease.View.gameObject), Is.False);
                Assert.That(lease.View.gameObject.activeInHierarchy, Is.True);
                host.SetVisible(true); host.SetInteractive(true); hits.Clear(); raycaster.Raycast(data, hits);
                T4ProbeView.Measurements.Add("visible-owner-raycasts=" + hits.Count);
                Assert.That(hits.Any(h => h.gameObject == lease.View.gameObject), Is.True);
                host.SetVisible(false); hits.Clear(); raycaster.Raycast(data, hits);
                Assert.That(hits.Any(h => h.gameObject == lease.View.gameObject), Is.False);
            }
        });
        [UnityTest] public IEnumerator ChildCanvasOrGroup_CannotBypassShellGate() => UniTask.ToCoroutine(async () =>
        {
            using (var f = new LoadingLayoutFixture())
            {
                var firstHost = f.NewHost(); var first = await f.Load(firstHost);
                first.View.gameObject.AddComponent<CanvasGroup>().ignoreParentGroups = true;
                Assert.Throws<InvalidOperationException>(() => firstHost.AttachHidden(first.View));
                var secondHost = f.NewHost();
                var second = await new ResourcesUIViewLoader().CreateAsync(UIViewAsset.Resources("T4/OverrideCanvas"), secondHost.InactiveParent, CancellationToken.None);
                f.Leases.Add(second);
                T4ProbeView.Events.Add("canvas-before:" + second.View.GetComponent<Canvas>().overrideSorting);
                Assert.Throws<InvalidOperationException>(() => secondHost.AttachHidden(second.View));
            }
        });
        [UnityTest] public IEnumerator LayerLayoutGroup_DoesNotDriveWrappers() => UniTask.ToCoroutine(async () =>
        {
            using (var f = new LoadingLayoutFixture())
            {
                var layer = f.Root.GetLayer(UILayer.Page);
                var group = layer.gameObject.AddComponent<VerticalLayoutGroup>();
                group.childControlWidth = group.childControlHeight = true;
                var host = f.NewHost(); var lease = await f.Load(host); lease.View.InvokeCreate(); host.AttachHidden(lease.View);
                var reference = f.Own(UnityEngine.Object.Instantiate(LoadingLayoutFixture.Prefab, layer, false));
                reference.AddComponent<LayoutElement>().ignoreLayout = true;
                Canvas.ForceUpdateCanvases(); AssertRectMatches(lease.View.transform, reference.transform);
                Assert.That(host.AnimationRoot.parent.GetComponent<RectTransform>().anchorMax, Is.EqualTo(Vector2.one));
            }
        });
        [UnityTest] public IEnumerator OnOpenDynamicBypass_IsRejectedBeforeCommit() => UniTask.ToCoroutine(async () =>
        {
            await using (var f = new LoadingLayoutFixture())
            {
                T4ProbeView.Opening = (view, scope, ct) =>
                {
                    var child = new GameObject("DynamicView", typeof(RectTransform), typeof(Image), typeof(CanvasGroup));
                    child.transform.SetParent(view.transform, false);
                    child.GetComponent<CanvasGroup>().ignoreParentGroups = true;
                    return UniTask.CompletedTask;
                };
                Exception failure = null;
                try { await f.CreateEngine().OpenAsync<T4ProbeView>(); } catch (Exception e) { failure = e; }
                Assert.That(failure, Is.TypeOf<UIException>()); Assert.That(f.Releases, Is.EqualTo(1));
                Assert.That(f.UI.GetDiagnostics().Sessions, Is.Empty);
            }
        });
        [UnityTest] public IEnumerator AsyncOpenDynamicBypass_IsHiddenAcrossFrame_AndRejected() => UniTask.ToCoroutine(async () =>
        {
            await using (var f = new LoadingLayoutFixture())
            {
                bool hiddenAfterFrame = false;
                T4ProbeView.Opening = async (view, scope, ct) =>
                {
                    view.gameObject.AddComponent<CanvasGroup>().ignoreParentGroups = true;
                    await UniTask.NextFrame();
                    hiddenAfterFrame = !view.gameObject.activeInHierarchy;
                };
                Exception failure = null;
                try { await f.CreateEngine().OpenAsync<T4ProbeView>(); } catch (Exception e) { failure = e; }
                Assert.That(hiddenAfterFrame, Is.True); Assert.That(failure, Is.TypeOf<UIException>());
                Assert.That(f.Releases, Is.EqualTo(1));
            }
        });
        [UnityTest] public IEnumerator GateChangeRejectsNewBypass_AndRemovesActualRaycast() => UniTask.ToCoroutine(async () =>
        {
            using (var f = new LoadingLayoutFixture())
            {
                var host = f.NewHost(); var lease = await f.Load(host); lease.View.InvokeCreate(); host.AttachHidden(lease.View);
                host.SetVisible(true); host.SetInteractive(true);
                await new UILayoutReady().WaitLayoutAsync(lease.View, CancellationToken.None);
                var eventSystem = f.Own(new GameObject("T4Events", typeof(UnityEngine.EventSystems.EventSystem))).GetComponent<UnityEngine.EventSystems.EventSystem>();
                var point = RectTransformUtility.WorldToScreenPoint(null, ((RectTransform)lease.View.transform).TransformPoint(((RectTransform)lease.View.transform).rect.center));
                var data = new UnityEngine.EventSystems.PointerEventData(eventSystem) { position = point };
                var hits = new System.Collections.Generic.List<UnityEngine.EventSystems.RaycastResult>();
                var raycaster = f.Root.GetLayer(UILayer.Page).GetComponent<GraphicRaycaster>();
                raycaster.Raycast(data, hits); Assert.That(hits.Any(h => h.gameObject == lease.View.gameObject), Is.True);
                lease.View.gameObject.AddComponent<CanvasGroup>().ignoreParentGroups = true;
                Exception failure = null;
                try { host.SetVisible(false); } catch (Exception e) { failure = e; }
                hits.Clear(); raycaster.Raycast(data, hits);
                Assert.That(hits.Any(h => h.gameObject == lease.View.gameObject), Is.False);
                Assert.That(failure, Is.TypeOf<InvalidOperationException>());
            }
        });
        [UnityTest] public IEnumerator OnEnableCannotInstallBypassBeforeOpen() => UniTask.ToCoroutine(async () =>
        {
            using (var f = new LoadingLayoutFixture())
            {
                var host = f.NewHost(); var lease = await f.Load(host);
                T4ProbeView.Enabled = view =>
                {
                    var canvas = view.gameObject.AddComponent<Canvas>(); canvas.overrideSorting = true;
                    Assert.That(canvas.overrideSorting, Is.True, "Control must install an actual active Canvas bypass");
                };
                Assert.Throws<InvalidOperationException>(() => host.AttachHidden(lease.View));
                Assert.That(lease.View.gameObject.activeInHierarchy, Is.False);
            }
        });
        [Test] public void UnsupportedRootMode_IsRejected()
        {
            var go = new GameObject("T4WorldCanvas", typeof(RectTransform), typeof(UIRoot));
            try
            {
                go.GetComponent<Canvas>().renderMode = RenderMode.WorldSpace;
                Assert.Throws<InvalidOperationException>(() => new UIViewHostFactory(go.GetComponent<UIRoot>()));
            }
            finally { UnityEngine.Object.DestroyImmediate(go); }
        }
    }
}
