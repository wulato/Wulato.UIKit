using Unity.Profiling;
namespace Wulato.UIKit
{
    // These scopes end when the operation returns its UniTask, before any asynchronous wait.
    internal static class UIProfiler
    {
        internal static readonly ProfilerMarker Load=new ProfilerMarker("Wulato.UIKit.Load.Sync");
        internal static readonly ProfilerMarker Layout=new ProfilerMarker("Wulato.UIKit.Layout.Sync");
        internal static readonly ProfilerMarker Hooks=new ProfilerMarker("Wulato.UIKit.Hooks.Sync");
        internal static readonly ProfilerMarker Transition=new ProfilerMarker("Wulato.UIKit.Transition.Sync");
        internal static readonly ProfilerMarker Cache=new ProfilerMarker("Wulato.UIKit.Cache.Sync");
        internal static ProfilerMarker Stage(string stage)=>stage=="load"?Load:stage=="layout"?Layout:Hooks;
    }
}
