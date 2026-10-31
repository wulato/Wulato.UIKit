using System;
using UnityEngine;
namespace Wulato.UIKit.Tests.Support
{
    internal sealed class FakeViewHostFactory : IUIViewHostFactory
    {
        public int CreateCount;
        public IUIViewHost Create(UILayer layer) { CreateCount++; return new FakeViewHost(); }
    }
    internal sealed class FakeViewHost : IUIViewHost
    {
        readonly GameObject root=new GameObject("fake-host",typeof(RectTransform),typeof(CanvasGroup));
        public FakeViewHost() { root.SetActive(false); }
        public Transform InactiveParent => root.transform;
        public RectTransform AnimationRoot => (RectTransform)root.transform;
        public CanvasGroup AnimationOpacity => root.GetComponent<CanvasGroup>();
        public void AttachHidden(UIViewBase view) { view.transform.SetParent(root.transform,false);root.SetActive(true); }
        public void SetVisible(bool visible) { AnimationOpacity.alpha=visible?1:0; }
        public void SetInteractive(bool value) { AnimationOpacity.interactable=value; }
        public void MoveToCache() => root.SetActive(false);
        public void DetachView() { }
        public void Dispose() { UnityEngine.Object.DestroyImmediate(root); }
    }
}
