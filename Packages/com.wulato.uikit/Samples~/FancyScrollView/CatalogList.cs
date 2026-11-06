using System;
using System.Collections.Generic;
using FancyScrollView;
using UnityEngine;
using UnityEngine.UI;

namespace Wulato.UIKit.Samples.FancyList
{
    [RequireComponent(typeof(RectTransform), typeof(Image), typeof(Scroller))]
    public sealed class CatalogList : FancyScrollView<int, CatalogContext>
    {
        public const float RowHeight = 64;
        GameObject template;
        Scroller scroller;
        protected override GameObject CellPrefab => template;
        public Scroller Scroller => scroller;

        protected override void Initialize()
        {
            cellContainer = transform;
            scrollOffset = 0; loop = false;
            scroller = GetComponent<Scroller>(); scroller.Inertia = false;
            scroller.OnValueChanged(UpdatePosition);
            template = new GameObject("Cell template", typeof(RectTransform));
            template.SetActive(false); template.transform.SetParent(transform, false);
            ((RectTransform)template.transform).sizeDelta = new Vector2(360, RowHeight);
            template.AddComponent<CatalogCell>();
            base.Initialize();
        }

        public IDisposable Begin(CatalogArgs args, UISessionScope scope)
        {
            if (args == null) throw new ArgumentNullException(nameof(args));
            if (args.LoadCaption == null) throw new ArgumentException("LoadCaption is required", nameof(args));
            if (!initialized) { Initialize(); initialized = true; }
            Context.Args = args; Context.Session = scope;
            ResizeViewport();
            scroller.Position = 0;
            UpdateContents(args.ItemIds);
            scroller.SetTotalCount(args.ItemIds.Count);
            return new SessionBinding(this, scope);
        }

        public void ResizeViewport()
        {
            Context.Height = Mathf.Max(RowHeight, ((RectTransform)transform).rect.height);
            cellInterval = RowHeight / Context.Height;
            if (initialized) Relayout();
        }
        void OnRectTransformDimensionsChange() { if (initialized) ResizeViewport(); }

        sealed class SessionBinding : IDisposable
        {
            CatalogList owner;
            readonly UISessionScope scope;
            public SessionBinding(CatalogList owner, UISessionScope scope) { this.owner = owner; this.scope = scope; }
            public void Dispose()
            {
                var list = owner; owner = null;
                if (!list || list.Context.Session != scope) return;
                list.Context.Session = null; list.Context.Args = null;
                list.scroller.Draggable = false;
                list.UpdateContents(new List<int>());
                list.scroller.SetTotalCount(0);
            }
        }
    }
}
