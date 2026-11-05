using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
namespace Wulato.UIKit.Navigation
{
    /// <summary>In-process navigation for one Context; history stores descriptors, not View instances.</summary>
    public interface IUINavigator
    {
        /// <summary>Push a registered parameterless page.</summary>
        UniTask<IUISession> PushAsync<TView>(CancellationToken cancellationToken=default) where TView:UIView;
        /// <summary>Push a typed page with immutable restoration arguments.</summary>
        UniTask<IUISession> PushAsync<TView,TArgs>(TArgs arguments,CancellationToken cancellationToken=default) where TView:UIView<TArgs>;
        /// <summary>Immutable descriptor snapshot including the current page; contains no View instances.</summary>
        IReadOnlyList<UIPageDescriptor> History { get; }
        /// <summary>Push a page through the type-erased registration bridge.</summary>
        UniTask<IUISession> PushAsync(UIViewTypeKey type,UIArguments arguments,CancellationToken cancellationToken=default);
        /// <summary>Replace only the current descriptor, or create the first page.</summary>
        UniTask<IUISession> ReplaceAsync<TView>(CancellationToken cancellationToken=default) where TView:UIView;
        UniTask<IUISession> ReplaceAsync<TView,TArgs>(TArgs arguments,CancellationToken cancellationToken=default) where TView:UIView<TArgs>;
        UniTask<IUISession> ReplaceAsync(UIViewTypeKey type,UIArguments arguments,CancellationToken cancellationToken=default);
        /// <summary>Restore the nearest matching descriptor and its arguments; missing/current returns false.</summary>
        UniTask<bool> PopToAsync<TView>(CancellationToken cancellationToken=default) where TView:UIViewBase;
        UniTask<bool> PopToAsync(UIViewTypeKey type,CancellationToken cancellationToken=default);
        /// <summary>Reconstruct the previous descriptor, or return false at the root.</summary>
        UniTask<bool> BackAsync(CancellationToken cancellationToken=default);
    }
    /// <summary>Arguments must be immutable caller-owned values suitable for reconstructing a page.</summary>
    public sealed class UIPageDescriptor
    {
        /// <summary>Stable registered View key.</summary>
        public UIViewTypeKey Type { get; }
        /// <summary>Caller-owned immutable arguments; no deep copy is performed.</summary>
        public UIArguments Arguments { get; }
        /// <summary>Create an in-process restoration descriptor.</summary>
        public UIPageDescriptor(UIViewTypeKey type,UIArguments arguments){Type=type;Arguments=arguments;}
    }
    /// <summary>Copied by the Navigator at construction; changes only apply to a new Navigator.</summary>
    public sealed class NavigationSettings
    {
        /// <summary>Positive, unscaled per-pair upper bound; defaults to two seconds.</summary>
        public TimeSpan PairTransitionTimeout { get; set; }=TimeSpan.FromSeconds(2);
    }
}
