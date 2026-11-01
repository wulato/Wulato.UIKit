using System;
using UnityEngine;
using UnityEngine.EventSystems;
namespace Wulato.UIKit
{
    /// <summary>Explicit module adapter. One driver and arbiter share one EventSystem.</summary>
    public interface IUIInputDriver
    {
        UIInputGate Gate { get; set; }
        void ResetForGate();
        bool AllReleased();
        void InvalidatePointers();
    }
    [DisallowMultipleComponent]
    public sealed class UIInputDomainRegistration : MonoBehaviour
    {
        internal UIInputDomain Domain;
        void OnDestroy() { Domain?.Dispose(); }
    }
    public sealed class UIInputDomain : IDisposable
    {
        readonly UIInputDomainRegistration marker;
        internal readonly UIInputGate Gate;
        readonly IUIInputDriver driver;
        internal UIInputArbiter Arbiter;
        bool disposed;
        public EventSystem EventSystem { get; }
        public UIInputDomain(EventSystem eventSystem, BaseInputModule module)
        {
            if (!eventSystem || !module || module.gameObject != eventSystem.gameObject || !(module is IUIInputDriver adapter))
                throw new ArgumentException("An explicitly supplied UIKit driver on the same EventSystem is required.");
            foreach (var other in eventSystem.GetComponents<BaseInputModule>())
                if (other != module && other.enabled) throw new InvalidOperationException("Only one UI input module may be active.");
            marker=eventSystem.GetComponent<UIInputDomainRegistration>();
            if (marker && marker.Domain!=null) throw new InvalidOperationException("This EventSystem already has an input domain.");
            if (!marker) marker=eventSystem.gameObject.AddComponent<UIInputDomainRegistration>();
            EventSystem=eventSystem;driver=adapter;
            Gate=eventSystem.gameObject.AddComponent<UIInputGate>();
            Gate.Initialize(eventSystem,adapter);adapter.Gate=Gate;marker.Domain=this;
        }
        internal void Register(UIInputArbiter arbiter)
        {
            if (disposed) throw new ObjectDisposedException(nameof(UIInputDomain));
            if (Arbiter!=null) throw new InvalidOperationException("An input domain permits only one arbiter.");
            Arbiter=arbiter;
        }
        public void Dispose()
        {
            if (disposed) return;disposed=true;
            Arbiter?.Dispose();driver.Gate=null;
            if (Gate) { Gate.Release();UnityEngine.Object.Destroy(Gate); }
            if (marker) marker.Domain=null;
        }
    }
}
