using System;
using UnityEngine;
using UnityEngine.UI;

namespace Wulato.UIKit
{
    /// <summary>Canvas configuration and optional authored safe-area mount. Context assembly belongs to the builder.</summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler))]
    [RequireComponent(typeof(GraphicRaycaster))]
    public sealed class UIRoot : MonoBehaviour
    {
        [SerializeField] RectTransform safeArea;
        [SerializeField] RectTransform[] layers = new RectTransform[5];
        bool initialized;
        internal event Action Disabled, Enabled, Destroyed;
        void OnDisable() => Disabled?.Invoke();
        void OnEnable() => Enabled?.Invoke();
        void OnDestroy() => Destroyed?.Invoke();
        public Canvas Canvas => GetComponent<Canvas>();
        public RectTransform SafeArea
        {
            get => safeArea;
            set
            {
                if (initialized) throw new InvalidOperationException("Configure the safe-area mount before creating a Context.");
                safeArea = value;
            }
        }
        internal void ValidateAndInitialize()
        {
            var canvas = Canvas;
            if (canvas.renderMode == RenderMode.WorldSpace ||
                (canvas.renderMode == RenderMode.ScreenSpaceCamera && canvas.worldCamera == null))
                throw new InvalidOperationException("UIRoot supports Overlay or Screen Space Camera with an assigned camera.");
            if (safeArea != null && safeArea != transform && !safeArea.IsChildOf(transform))
                throw new InvalidOperationException("The safe-area mount must belong to this UIRoot.");
            if (initialized) return;
            if (layers == null || layers.Length != 5) throw new InvalidOperationException("Five logical layer slots are required.");
            Transform mount = safeArea != null ? safeArea : transform;
            for (int i = 0; i < layers.Length; i++)
            {
                if (layers[i] == null) layers[i] = CreateRect(((UILayer)i).ToString(), mount);
                if (layers[i].parent != mount) throw new InvalidOperationException("Logical layers must be direct children of the configured mount.");
                for (int j = 0; j < i; j++) if (layers[i] == layers[j]) throw new InvalidOperationException("Logical layers must be distinct.");
                var layerCanvas = layers[i].GetComponent<Canvas>();
                if (layerCanvas == null) layerCanvas = layers[i].gameObject.AddComponent<Canvas>();
                layerCanvas.overrideSorting = true; layerCanvas.sortingOrder = canvas.sortingOrder + i * 100;
                if (layers[i].GetComponent<GraphicRaycaster>() == null) layers[i].gameObject.AddComponent<GraphicRaycaster>();
            }
            initialized = true;
        }
        internal RectTransform GetLayer(UILayer layer)
        {
            if (!Enum.IsDefined(typeof(UILayer), layer)) throw new ArgumentOutOfRangeException(nameof(layer));
            ValidateAndInitialize(); return layers[(int)layer];
        }
        internal static RectTransform CreateRect(string name, Transform parent)
        {
            var rect = new GameObject(name, typeof(RectTransform)).GetComponent<RectTransform>();
            rect.SetParent(parent, false);
            rect.anchorMin = Vector2.zero; rect.anchorMax = Vector2.one;
            rect.offsetMin = rect.offsetMax = Vector2.zero; rect.pivot = new Vector2(.5f, .5f);
            rect.localScale = Vector3.one; rect.localRotation = Quaternion.identity;
            return rect;
        }
    }
}
