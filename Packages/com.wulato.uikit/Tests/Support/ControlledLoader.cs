using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;
namespace Wulato.UIKit.Tests.Support
{
    public sealed class ControlledLoader : IUIViewLoader
    {
        public Type ViewType=typeof(ProbeView);
        public int CreateCount,ReleaseCount;
        public UIViewBase LastView;
        public Func<Transform,UniTask<IUIViewLease>> Create;
        public UniTask<IUIViewLease> CreateAsync(UIViewAsset asset,Transform parent,CancellationToken ct)
        {
            CreateCount++; if(Create!=null)return Create(parent);
            return UniTask.FromResult(NewLease(parent));
        }
        public IUIViewLease NewLease(Transform parent)
        {
            var go=new GameObject("probe",typeof(RectTransform));go.SetActive(false);go.transform.SetParent(parent,false);
            LastView=(UIViewBase)go.AddComponent(ViewType); go.SetActive(true);return new Lease(this,LastView);
        }
        sealed class Lease : IUIViewLease
        {
            ControlledLoader owner; public UIViewBase View { get; }
            public Lease(ControlledLoader owner,UIViewBase view) { this.owner=owner;View=view; }
            public void Dispose() { if(owner==null)return;owner.ReleaseCount++;owner=null;UnityEngine.Object.DestroyImmediate(View.gameObject); }
        }
    }
}
