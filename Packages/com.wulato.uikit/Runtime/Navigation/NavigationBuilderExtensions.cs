using System;
using System.Threading;
using Cysharp.Threading.Tasks;
namespace Wulato.UIKit.Navigation
{
    /// <summary>Explicit navigation composition and type-safe convenience entry points.</summary>
    public static class NavigationBuilderExtensions
    {
        /// <inheritdoc/>
        public static UIContextBuilder UseNavigation(this UIContextBuilder builder,INavigationTransition transition=null,NavigationSettings settings=null)
            =>builder.WithTransactions().WithServiceFactory<IUINavigator>(ui=>new UINavigator(ui,transition,settings));
    }
}
