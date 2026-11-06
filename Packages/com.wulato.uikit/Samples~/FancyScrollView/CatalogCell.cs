using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using FancyScrollView;
using UnityEngine;
using UnityEngine.UI;

namespace Wulato.UIKit.Samples.FancyList
{
    public sealed class CatalogContext
    {
        public UISessionScope Session;
        public CatalogArgs Args;
        public float Height;
    }

    public sealed class CatalogCell : FancyCell<int, CatalogContext>
    {
        Text label;
        CancellationTokenSource request;
        int revision;
        public int ItemId { get; private set; } = -1;
        public int DisplayedId { get; private set; } = -1;
        public string Caption => label ? label.text : "";

        public override void Initialize()
        {
            var node = new GameObject("Caption", typeof(RectTransform), typeof(Text));
            node.transform.SetParent(transform, false);
            var rect = (RectTransform)node.transform;
            rect.anchorMin = Vector2.zero; rect.anchorMax = Vector2.one;
            rect.offsetMin = rect.offsetMax = Vector2.zero;
            label = node.GetComponent<Text>();
            label.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            label.fontSize = 22; label.alignment = TextAnchor.MiddleCenter;
            label.raycastTarget = false;
        }

        public override void UpdateContent(int itemId)
        {
            CancelRequest();
            ItemId = itemId; DisplayedId = -1;
            label.text = "Loading " + itemId;
            var session = Context.Session;
            if (session == null || !session.IsCurrent) return;
            request = CancellationTokenSource.CreateLinkedTokenSource(session.Token);
            LoadAsync(Context.Args, session, itemId, revision, request.Token).Forget();
        }

        async UniTask LoadAsync(CatalogArgs args, UISessionScope session, int itemId, int ticket, CancellationToken ct)
        {
            try
            {
                var text = await args.LoadCaption(itemId, ct);
                await UniTask.SwitchToMainThread();
                // A loader may ignore cancellation or reuse an item id: check the binding ticket too.
                if (!this || ct.IsCancellationRequested || !session.IsCurrent || ticket != revision) return;
                label.text = text; DisplayedId = itemId;
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested) { }
            catch (Exception error)
            {
                await UniTask.SwitchToMainThread();
                if (this && !ct.IsCancellationRequested && session.IsCurrent && ticket == revision)
                    Debug.LogException(error, this);
            }
        }

        public override void UpdatePosition(float position)
        {
            ((RectTransform)transform).anchoredPosition = new Vector2(0, Context.Height * (.5f - position) - CatalogList.RowHeight * .5f);
            // Position-driven presentation; movement and easing belong to the SDK's Scroller.
            transform.localScale = Vector3.one * (1f - .08f * Mathf.Abs(2f * position - 1f));
        }

        public override void SetVisible(bool visible)
        {
            if (!visible) CancelRequest();
            base.SetVisible(visible);
        }
        void OnDisable() => CancelRequest();
        void OnDestroy() => CancelRequest();
        void CancelRequest()
        {
            revision++;
            var previous = request; request = null;
            if (previous == null) return;
            previous.Cancel(); previous.Dispose();
        }
    }
}
