using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.Localization;
using UnityEngine.UI;

namespace Wulato.UIKit.Samples.Localization
{
    // Pass a LocalizedString configured with a table and entry to OpenAsync.
    // Keep one reference per label; Localization owns its loading handles.
    public class LocalizationView : UIView<LocalizedString>
    {
        [SerializeField] Text label;
        protected override void OnCreate()
        {
            if (label) return;
            var node = new GameObject("Localized label", typeof(RectTransform), typeof(Text));
            node.transform.SetParent(transform, false);
            label = node.GetComponent<Text>();
            label.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            label.raycastTarget = false;
        }

        protected override UniTask OnOpenAsync(LocalizedString reference, UISessionScope scope, CancellationToken ct)
        {
            if (reference == null) throw new ArgumentNullException(nameof(reference));
            ct.ThrowIfCancellationRequested();
            label.text = "";
            LocalizedString.ChangeHandler changed = value =>
            {
                // The captured opening, including queued callbacks, cannot write after it ends.
                if (scope.IsCurrent && label) label.text = value;
            };
            // Track BEFORE subscribing: the SDK can complete synchronously or throw during add.
            scope.Track(new Subscription(reference, changed));
            reference.StringChanged += changed;
            return UniTask.CompletedTask;
        }

        sealed class Subscription : IDisposable
        {
            LocalizedString reference;
            readonly LocalizedString.ChangeHandler handler;
            internal Subscription(LocalizedString reference, LocalizedString.ChangeHandler handler)
            { this.reference = reference; this.handler = handler; }
            public void Dispose()
            {
                var old = reference;
                reference = null;
                if (old != null) old.StringChanged -= handler;
            }
        }
    }
}
