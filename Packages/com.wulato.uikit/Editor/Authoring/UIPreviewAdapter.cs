using UnityEngine;
namespace Wulato.UIKit.Editor.Authoring
{
    /// <summary>Cloned once per preview. Own shared dependencies until their late work finishes.</summary>
    public abstract class UIPreviewAdapter : ScriptableObject
    {
        public UIShutdownCompletion ShutdownResult { get; internal set; }
        public virtual void Configure(UIContextBuilder builder) { }
        public virtual UIArguments CreateArguments()=>UIArguments.None;
        /// <summary>Called once. When ShutdownResult has pending cleanup, defer disposal of dependencies still used by late work.</summary>
        public virtual void Release() { }
    }
}
