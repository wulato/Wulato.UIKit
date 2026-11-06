using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.UI;

namespace Wulato.UIKit.Samples.FancyList
{
    public sealed class CatalogArgs
    {
        public IList<int> ItemIds { get; }
        public Func<int, CancellationToken, UniTask<string>> LoadCaption { get; }
        public CatalogArgs(IList<int> ids, Func<int, CancellationToken, UniTask<string>> loadCaption)
        { ItemIds = ids ?? throw new ArgumentNullException(nameof(ids)); LoadCaption = loadCaption; }
    }

    public sealed class CatalogView : UIView<CatalogArgs>
    {
        [SerializeField] CatalogList list;
        protected override void OnCreate()
        {
            if (!list) throw new InvalidOperationException("Assign a CatalogList with its Scroller viewport in the prefab; use the included CatalogDemo prefab.");
        }
        protected override UniTask OnOpenAsync(CatalogArgs args, UISessionScope scope, CancellationToken ct)
        {
            ct.ThrowIfCancellationRequested();
            scope.Track(list.Begin(args, scope));
            list.Scroller.Draggable = true;
            return UniTask.CompletedTask;
        }
    }
}
