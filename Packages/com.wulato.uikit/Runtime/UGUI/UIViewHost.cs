using System;
using System.Collections.Generic;
using UnityEngine.UI;
using UnityEngine;

namespace Wulato.UIKit
{
    internal sealed class UIViewHost : IUIViewHost, IUIIsolatableHost
    {
        RectTransform layer;
        readonly RectTransform stagingRoot, cacheRoot, shell;
        readonly CanvasGroup visibility;
        readonly List<Canvas> canvases = new List<Canvas>();
        readonly List<CanvasGroup> groups = new List<CanvasGroup>();
        UIViewBase attachedView;
        Exception gateFailure;
        Action<UIViewHost> released;
        Transform evacuatedParent;
        bool evacuated, evacuatedActive;
        bool visible, interactive, disposed;
        public bool IsHealthy { get { MonitorGate(); return !disposed && shell != null && attachedView != null && gateFailure == null; } }
        public Transform InactiveParent => AnimationRoot;
        public RectTransform AnimationRoot { get; }
        public CanvasGroup AnimationOpacity { get; }
        internal UIViewHost(RectTransform layer, RectTransform stagingRoot, RectTransform cacheRoot, Action<UIViewHost> released = null)
        {
            this.layer = layer; this.stagingRoot = stagingRoot; this.cacheRoot = cacheRoot; this.released = released;
            shell = UIRoot.CreateRect("Shell", stagingRoot);
            shell.gameObject.SetActive(false);
            shell.gameObject.AddComponent<LayoutElement>().ignoreLayout = true;
            visibility = shell.gameObject.AddComponent<CanvasGroup>();
            shell.gameObject.AddComponent<UIViewGateGuard>().Initialize(MonitorGate, ValidateConfiguration);
            AnimationRoot = UIRoot.CreateRect("AnimationRoot", shell);
            AnimationRoot.gameObject.AddComponent<LayoutElement>().ignoreLayout = true;
            AnimationOpacity = AnimationRoot.gameObject.AddComponent<CanvasGroup>();
            ApplyGate();
        }
        void EnsureAlive()
        {
            if (disposed || shell == null) throw new ObjectDisposedException(nameof(UIViewHost));
        }
        public void AttachHidden(UIViewBase view)
        {
            EnsureAlive();
            if (view == null || view.transform.parent != AnimationRoot || !(view.transform is RectTransform) || !view.gameObject.activeSelf || !view.enabled)
                throw new InvalidOperationException("The loader must supply an enabled UIView root directly under AnimationRoot.");
            evacuated = false;
            attachedView = view;
            ValidateConfiguration();
            shell.gameObject.SetActive(false);
            visible = interactive = false; ApplyGate();
            shell.SetParent(layer, false);
            shell.gameObject.SetActive(true);
            ValidateConfiguration();
        }
        void ValidateGate()
        {
            // Reuse buffers: frame checks allocate only when dynamic content grows capacity.
            attachedView.GetComponentsInChildren(true, canvases);
            foreach (var canvas in canvases)
                if (canvas.overrideSorting) throw new InvalidOperationException("Unregistered overrideSorting Canvas bypasses the Shell.");
            attachedView.GetComponentsInChildren(true, groups);
            foreach (var group in groups)
                if (group.ignoreParentGroups) throw new InvalidOperationException("ignoreParentGroups bypasses the Shell.");
        }
        void MonitorGate()
        {
            if (disposed || attachedView == null || shell == null || gateFailure != null) return;
            try { ValidateGate(); }
            catch (Exception e)
            {
                gateFailure = e;
                visible = interactive = false; ApplyGate();
                shell.gameObject.SetActive(false);
            }
        }
        void ValidateConfiguration()
        {
            MonitorGate();
            if (gateFailure != null) throw new InvalidOperationException("View configuration invalidates the Shell gate.", gateFailure);
        }
        void ApplyGate()
        {
            visibility.alpha = visible ? 1 : 0;
            visibility.interactable = visibility.blocksRaycasts = visible && interactive;
        }
        public void SetVisible(bool value) { EnsureAlive(); visible = value; ApplyGate(); ValidateConfiguration(); }
        public void SetInteractive(bool value) { EnsureAlive(); interactive = value; ApplyGate(); ValidateConfiguration(); }
        public void MoveToCache()
        {
            evacuated = false;
            EnsureAlive(); visible = interactive = false; ApplyGate();
            shell.gameObject.SetActive(false); shell.SetParent(cacheRoot, false);
        }
        public void MoveToIsolation(Transform parent)
        {
            EnsureAlive(); evacuated = false; visible = interactive = false; ApplyGate();
            shell.gameObject.SetActive(false); shell.SetParent(parent, false);
            released?.Invoke(this); released = null; layer = null; evacuatedParent = null;
        }
        internal void Evacuate(Transform root, Transform isolation)
        {
            if (disposed || shell == null || !shell.IsChildOf(root)) return;
            evacuatedParent = shell.parent; evacuatedActive = shell.gameObject.activeSelf; evacuated = true;
            shell.gameObject.SetActive(false); shell.SetParent(isolation, false);
        }
        internal void RestoreEvacuated()
        {
            if (!evacuated || disposed || shell == null || evacuatedParent == null) return;
            evacuated = false; shell.SetParent(evacuatedParent, false); shell.gameObject.SetActive(evacuatedActive);
        }
        public void DetachView()
        {
            evacuated = false;
            attachedView = null;
            canvases.Clear(); groups.Clear();
            if (AnimationRoot == null) return;
            while (AnimationRoot.childCount != 0)
            {
                var child = AnimationRoot.GetChild(0);
                if (stagingRoot == null) child.gameObject.SetActive(false);
                child.SetParent(stagingRoot, false);
            }
        }
        public void Dispose()
        {
            if (disposed) return;
            disposed = true;
            released?.Invoke(this);
            if (shell == null) return;
            shell.gameObject.SetActive(false);
            DetachView();
            if (Application.isPlaying) UnityEngine.Object.Destroy(shell.gameObject);
            else UnityEngine.Object.DestroyImmediate(shell.gameObject);
        }
    }
}
