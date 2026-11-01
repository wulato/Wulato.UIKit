using System;
using UnityEngine;
namespace Wulato.UIKit
{
    /// <summary>Authoring data only. Runtime bindings are serialized into the generated View partial.</summary>
    [DisallowMultipleComponent]
    public sealed class UIBindingMarker : MonoBehaviour
    {
        [Serializable] public sealed class Binding { public string fieldName; public Component target; }
        [SerializeField] public UnityEngine.Object viewScript;
        [SerializeField] public Binding[] bindings = Array.Empty<Binding>();
        [Tooltip("Select one registered prefab per View type; a base prefab can be excluded when registering its variant.")]
        [SerializeField] public bool includeInRegistry = true;
        [SerializeField] public string typeKey;
        [SerializeField] public UILayer layer = UILayer.Page;
        [SerializeField] public UIMultiplicity multiplicity = UIMultiplicity.Single;
        [SerializeField] public UIClosePolicy closePolicy = UIClosePolicy.Release;
        [SerializeField] public UIBackAction backAction = UIBackAction.RequestClose;
    }
}
