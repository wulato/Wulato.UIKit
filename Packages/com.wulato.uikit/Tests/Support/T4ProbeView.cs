using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;
namespace Wulato.UIKit.Tests.Support
{
    public sealed class T4ProbeView : UIView
    {
        public RectTransform SerializedChild;
        public static readonly List<string> Events = new List<string>();
        public static readonly List<string> Measurements = new List<string>();
        public static Func<T4ProbeView, UISessionScope, CancellationToken, UniTask> Opening;
        public static Action<T4ProbeView> Disposing, Enabled;
        public static int DestroyCount;
        public bool CreatedBeforeAwake { get; private set; }
        public bool Awoke { get; private set; }
        public UISessionScope Scope { get; private set; }
        protected override void OnCreate()
        {
            CreatedBeforeAwake = !Awoke;
            Measurements.Add("create:id=" + GetInstanceID() + ":awoke=" + Awoke + ":serialized=" + (SerializedChild != null));
            if (SerializedChild == null) throw new InvalidOperationException("Missing serialized reference");
            Events.Add("create");
        }
        void Awake() { Awoke = true; Events.Add("awake"); Measurements.Add("awake:id=" + GetInstanceID()); }
        void OnEnable() { Events.Add("enable"); Enabled?.Invoke(this); }
        protected override UniTask OnOpenAsync(UISessionScope scope, CancellationToken token)
        {
            Scope = scope;
            Events.Add("open");
            return Opening?.Invoke(this, scope, token) ?? UniTask.CompletedTask;
        }
        protected override void OnDispose() { Events.Add("dispose"); Disposing?.Invoke(this); }
        void OnDestroy() { DestroyCount++; Events.Add("destroy"); }
        public static void ClearProbe()
        {
            Events.Clear(); Measurements.Clear(); Opening = null; Disposing = null; Enabled = null; DestroyCount = 0;
        }
    }
}
