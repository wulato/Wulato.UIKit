using System;
using System.Collections.Generic;
using NUnit.Framework;
namespace Wulato.UIKit.Tests
{
    [Category("contracts")]
    public sealed class ContractTests
    {
        [Test] public void RuntimeAssembly_UnifiesModulesAndHidesTransactionInfrastructure()
        {
            var assembly=typeof(IUIContext).Assembly;
            Assert.That(assembly.GetName().Name,Is.EqualTo("Wulato.UIKit"));
            foreach(var type in new[]{typeof(UIViewBase),typeof(UIContextBuilder),typeof(ResourcesUIViewLoader),
                typeof(Navigation.UINavigator),typeof(Dialogs.UIDialogs),typeof(PopupQueue.UIPopupQueue),typeof(Presentation.UIPresenterObserver)})
                Assert.That(type.Assembly,Is.SameAs(assembly),type.FullName);
            foreach(var name in new[]{"IUITransactionAccess","IUITransactionSteps","IUIPageTransactionAccess","IUIPageTransactionSteps","IUITransactionParticipant","UITransitionOwner",
                "UIInputNode","UIInputPointerTracker","UIInputDragTracker","UIInputCancelTracker","UIModalMask","UIInputDomainRegistration"})
                Assert.That(assembly.GetType("Wulato.UIKit."+name,true).IsPublic,Is.False,name);
        }
        [Test] public void ViewBranches_AreDistinct()
        {
            Assert.That(typeof(UIView).BaseType, Is.EqualTo(typeof(UIViewBase)));
            Assert.That(typeof(UIView<int>).BaseType, Is.EqualTo(typeof(UIViewBase)));
            Assert.That(typeof(UIView).IsAssignableFrom(typeof(UIView<int>)), Is.False);
        }
        [Test] public void Arguments_NoneTypedNullAndValueRemainDistinct()
        {
            Assert.That(UIArguments.None.HasValue, Is.False);
            var empty=UIArguments.From<string>(null);
            Assert.That(empty.HasValue, Is.True);
            Assert.That(empty.ArgumentType, Is.EqualTo(typeof(string)));
            Assert.That(empty.Get<string>(), Is.Null);
            Assert.Throws<InvalidOperationException>(() => empty.Get<object>());
            Assert.Throws<InvalidOperationException>(() => UIArguments.None.Get<string>());
            Assert.That(UIArguments.From(42).Get<int>(), Is.EqualTo(42));
        }
        [Test] public void StableIdentity_UsesValueEquality()
        {
            var key=new UIViewTypeKey("inventory:v1");
            Assert.That(new Dictionary<UIViewTypeKey,int>{{key,1}}[new UIViewTypeKey("inventory:v1")], Is.EqualTo(1));
            Assert.That(new UIContextId(Guid.Parse("11111111-1111-1111-1111-111111111111")), Is.EqualTo(new UIContextId(Guid.Parse("11111111-1111-1111-1111-111111111111"))));
            Assert.That(new UISessionId(1), Is.Not.EqualTo(new UISessionId(2)));
            Assert.Throws<ArgumentException>(() => new UIViewTypeKey(" "));
        }
        [Test] public void Completion_CopiesCollectionsAndPreservesErrors()
        {
            var errors=new List<Exception>{new Exception("first")};
            var completion=new UICompletion(new UISessionId(1),UICloseReason.Owner,errors,true);
            var items=new List<UICompletion>{completion};
            var batch=new UIBatchCompletion(items,errors);
            var shutdown=new UIShutdownCompletion(items,errors,2);
            errors.Clear();items.Clear();
            Assert.That(completion.Errors.Count, Is.EqualTo(1));
            Assert.That(batch.Completions.Count, Is.EqualTo(1));
            Assert.That(shutdown.Completions.Count, Is.EqualTo(1));
            Assert.That(shutdown.PendingCleanupCount, Is.EqualTo(2));
            Assert.Throws<NotSupportedException>(() => ((IList<Exception>)completion.Errors).Clear());
        }
        [Test] public void Diagnostics_CopiesCollectionsWithoutLiveObjects()
        {
            var entries=new List<UISessionDiagnostics>{new UISessionDiagnostics(new UISessionId(1),new UIViewTypeKey("test"),UIState.Open)};
            var cache=new List<UIViewTypeKey>{new UIViewTypeKey("cached")};
            var snapshot=new UIDiagnosticsSnapshot(default,UIContextState.Running,UIDiagnosticsLevel.Basic,1.0,entries,1,2,cache,null,0,2);
            entries.Clear();cache.Clear();
            Assert.That(snapshot.Sessions.Count,Is.EqualTo(1));Assert.That(snapshot.CacheLru.Count,Is.EqualTo(1));
            Assert.Throws<NotSupportedException>(() => ((IList<UIViewTypeKey>)snapshot.CacheLru).Clear());
        }
        [Test] public void PublicHandlesAndScopes_DoNotExposeViewOrCommands()
        {
            foreach(var p in typeof(IUISession).GetProperties()) Assert.That(typeof(UnityEngine.Object).IsAssignableFrom(p.PropertyType),Is.False);
            Assert.That(typeof(UISessionScope).GetMethod("OpenAsync"),Is.Null);
            Assert.That(typeof(UISessionScope).GetMethod("CloseAsync"),Is.Null);
            Assert.That(typeof(UIResourceScope).IsAbstract,Is.True);
        }
    }
}
