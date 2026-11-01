using System;
using System.Collections.Generic;
using UnityEngine;

namespace Wulato.UIKit
{
    [DefaultExecutionOrder(-31900)]
    internal sealed class UIRuntimeHost : MonoBehaviour, IUIRuntimeHost
    {
        sealed class Attachment
        {
            internal UICompletionQueue Queue;
            internal UIOrphanRegistry Orphans;
            internal bool Released;
        }
        sealed class Lease : IDisposable
        {
            Action release;
            internal Lease(Action release) { this.release = release; }
            public void Dispose() { var action = release; release = null; action?.Invoke(); }
        }
        readonly List<Attachment> attachments = new List<Attachment>();
        int retainCount;
        bool destroying;
        public Transform IsolationRoot { get; private set; }
        internal static UIRuntimeHost Create()
        {
            var go = new GameObject("UIKit.RuntimeHost");
            if (Application.isPlaying) DontDestroyOnLoad(go);
            return go.AddComponent<UIRuntimeHost>();
        }
        void Awake()
        {
            var isolation = new GameObject("Isolation", typeof(RectTransform));
            isolation.SetActive(false); isolation.transform.SetParent(transform, false); IsolationRoot = isolation.transform;
        }
        internal IDisposable Retain()
        {
            retainCount++; return new Lease(() => retainCount--);
        }
        public IDisposable Attach(UICompletionQueue notifications, UIOrphanRegistry orphans)
        {
            var attachment = new Attachment { Queue = notifications, Orphans = orphans };
            attachments.Add(attachment); return new Lease(() => attachment.Released = true);
        }
        void OnApplicationQuit() => UIContextFacade.StopPlaySession();
        void LateUpdate()
        {
            for (int i = attachments.Count - 1; i >= 0; i--)
            {
                var item = attachments[i]; item.Queue.Drain();
                if (item.Released && item.Orphans.PendingCount == 0 && item.Queue.PendingCount == 0) attachments.RemoveAt(i);
            }
            if (!destroying && retainCount == 0 && attachments.Count == 0)
            {
                destroying = true; Destroy(gameObject);
            }
        }
    }
}
