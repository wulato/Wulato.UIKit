using UnityEngine;
namespace Wulato.UIKit
{
    /// <summary>Authored factory; each Context receives its own playback owner. Null Host presets use Immediate.</summary>
    public abstract class UITransitionPreset:ScriptableObject
    {
        public abstract IUITransition CreateTransition();
    }
}
