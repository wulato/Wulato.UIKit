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
    public class LayoutReadyTests : LoadingLayoutTestBase
    {
        [UnityTest] public IEnumerator ContentSizeFitterAndLayoutGroup_AreReadyAfterOneUpdate() => UniTask.ToCoroutine(async () =>
        {
            using (var f = new LoadingLayoutFixture())
            {
                var host = f.NewHost(); var lease = await f.Load(host); lease.View.InvokeCreate(); host.AttachHidden(lease.View);
                var content = ((T4ProbeView)lease.View).SerializedChild;
                var element = content.GetChild(1).GetComponent<LayoutElement>(); element.preferredWidth = 161; element.preferredHeight = 63;
                int frame = Time.frameCount;
                await new UILayoutReady().WaitLayoutAsync(lease.View, CancellationToken.None);
                T4ProbeView.Measurements.Add("layout:start-frame=" + frame + ":end-frame=" + Time.frameCount + ":content=" + content.rect.size);
                Assert.That(Time.frameCount, Is.EqualTo(frame + 1));
                Assert.That(content.rect.width, Is.EqualTo(185).Within(.01)); Assert.That(content.rect.height, Is.EqualTo(114).Within(.01));
            }
        });
        [UnityTest] public IEnumerator LayoutInsideOpen_DoesNotQueueCommand() => UniTask.ToCoroutine(async () =>
        {
            await using (var f = new LoadingLayoutFixture())
            {
                int insideLayoutFrame = 0;
                T4ProbeView.Opening = async (view, scope, ct) =>
                {
                    view.SerializedChild.GetChild(1).GetComponent<LayoutElement>().preferredHeight = 63;
                    await scope.WaitLayoutAsync(ct); insideLayoutFrame = Time.frameCount;
                    Assert.That(view.SerializedChild.rect.height, Is.EqualTo(114).Within(.01));
                };
                var session = await f.CreateEngine().OpenAsync<T4ProbeView>();
                Assert.That(session.State, Is.EqualTo(UIState.Open));
                Assert.That(f.EnterFrame, Is.EqualTo(insideLayoutFrame + 1), "Final layout barrier is one additional Update");
                Assert.That(f.SizeAtEnter.y, Is.EqualTo(114).Within(.01));
            }
        });
        [UnityTest] public IEnumerator FinalLayout_IsVisibleToEnterWithoutAnotherFrame() => UniTask.ToCoroutine(async () =>
        {
            await using (var f = new LoadingLayoutFixture())
            {
                int openFrame = 0;
                T4ProbeView.Opening = (view, scope, ct) =>
                {
                    openFrame = Time.frameCount; var element = view.SerializedChild.GetChild(1).GetComponent<LayoutElement>();
                    element.preferredWidth = 161; element.preferredHeight = 63; return UniTask.CompletedTask;
                };
                await f.CreateEngine().OpenAsync<T4ProbeView>();
                Assert.That(f.EnterFrame, Is.EqualTo(openFrame + 1));
                Assert.That(f.SizeAtEnter.x, Is.EqualTo(185).Within(.01)); Assert.That(f.SizeAtEnter.y, Is.EqualTo(114).Within(.01));
            }
        });
        [UnityTest] public IEnumerator CanceledLayout_DoesNotCompleteSuccessfully() => UniTask.ToCoroutine(async () =>
        {
            using (var f = new LoadingLayoutFixture()) using (var ct = new CancellationTokenSource())
            {
                var host = f.NewHost(); var lease = await f.Load(host); lease.View.InvokeCreate(); host.AttachHidden(lease.View);
                var pending = new UILayoutReady().WaitLayoutAsync(lease.View, ct.Token); ct.Cancel(); bool canceled = false;
                try { await pending; } catch (OperationCanceledException) { canceled = true; }
                Assert.That(canceled, Is.True);
            }
        });
        [UnityTest] public IEnumerator TimeScaleZero_LayoutStillBecomesReady() => UniTask.ToCoroutine(async () =>
        {
            float old = Time.timeScale;
            try
            {
                Time.timeScale = 0;
                using (var f = new LoadingLayoutFixture())
                {
                    var host = f.NewHost(); var lease = await f.Load(host); lease.View.InvokeCreate(); host.AttachHidden(lease.View);
                    await new UILayoutReady().WaitLayoutAsync(lease.View, CancellationToken.None);
                    Assert.That(((T4ProbeView)lease.View).SerializedChild.rect.height, Is.EqualTo(91).Within(.01));
                }
            }
            finally { Time.timeScale = old; }
        });
    }
}
