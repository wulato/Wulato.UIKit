using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using R3;
using UnityEngine;
using UnityEngine.UI;

namespace Wulato.UIKit.Samples.R3Binding
{
    public class CounterView : UIView<CounterModel>
    {
        [SerializeField] Text countLabel;
        [SerializeField] Button incrementButton;
        [SerializeField] Button closeButton;

        protected override void OnCreate()
        {
            // Assign authored widgets in a prefab, or use these minimal sample widgets.
            if (!countLabel) countLabel = Label(transform, "Count", "", 70);
            if (!incrementButton) incrementButton = MakeButton("Increment", 0);
            if (!closeButton) closeButton = MakeButton("Close", -70);
        }

        protected override UniTask OnOpenAsync(CounterModel model, UISessionScope scope, CancellationToken ct)
        {
            if (model == null) throw new ArgumentNullException(nameof(model));
            ct.ThrowIfCancellationRequested();
            // Capture this opening's scope, and reject queued notifications AFTER dispatching.
            scope.Track(model.Count.ObserveOnMainThread()
                .Where(_ => scope.IsCurrent).SubscribeToText(countLabel));
            scope.Track(incrementButton.OnClickAsObservable().Subscribe(_ =>
            {
                if (scope.IsCurrent) model.Increment();
            }));
            scope.Track(closeButton.OnClickAsObservable().Subscribe(_ =>
            {
                if (scope.IsCurrent) RequestClose();
            }));
            return UniTask.CompletedTask;
        }

        static GameObject Node(Transform parent, string name, float y)
        {
            var node = new GameObject(name, typeof(RectTransform));
            node.transform.SetParent(parent, false);
            var rect = (RectTransform)node.transform;
            rect.sizeDelta = new Vector2(360, 52);
            rect.anchoredPosition = new Vector2(0, y);
            return node;
        }

        static Text Label(Transform parent, string name, string value, float y)
        {
            var label = Node(parent, name, y).AddComponent<Text>();
            label.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            label.fontSize = 24;
            label.alignment = TextAnchor.MiddleCenter;
            label.color = Color.white;
            label.raycastTarget = false;
            label.text = value;
            return label;
        }

        Button MakeButton(string name, float y)
        {
            var node = Node(transform, name, y);
            var image = node.AddComponent<Image>();
            image.color = new Color(.16f, .35f, .50f);
            var button = node.AddComponent<Button>();
            button.targetGraphic = image;
            Label(node.transform, "Caption", name, 0);
            return button;
        }
    }
}
