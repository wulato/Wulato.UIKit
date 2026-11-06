using UnityEngine;
namespace Wulato.UIKit
{
    /// <summary>Authored page-pair factory without an optional SDK dependency.</summary>
    public abstract class UINavigationTransitionPreset:ScriptableObject
    {
        public abstract INavigationTransition CreateTransition();
    }
}
