using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Cysharp.Threading.Tasks;
using NUnit.Framework;
using UnityEngine;
using Wulato.UIKit.Dialogs;
using Wulato.UIKit.Navigation;
using Wulato.UIKit.PopupQueue;
using Wulato.UIKit.Tests.Support;
namespace Wulato.UIKit.Tests.EditMode
{
    public sealed class ApiQuestion : IDialogRequest<bool> { }
    public sealed class ApiDialog : UIView<DialogArguments<ApiQuestion,bool>> { }
    [Category("contracts")]
    public sealed class ContextApiContractTests
    {
        [Test] public void Facade_IsTheOnlyProductionContextImplementation()
        {
            var types=typeof(IUIContext).Assembly.GetTypes().Where(t=>!t.IsInterface&&typeof(IUIContext).IsAssignableFrom(t)).ToArray();
            Assert.That(types.Select(t=>t.Name),Is.EqualTo(new[]{"UIContextFacade"}));
            Assert.That(typeof(UIContextHost).GetProperty("UI"),Is.Not.Null);Assert.That(typeof(UIContextHost).GetProperty("Context"),Is.Null);
            Assert.That(typeof(UIContextBuilder).GetMethod("WithTransactions"),Is.Null);
        }
        [Test] public void DialogMapping_IsRegisteredAndValidatedWithTheView()
        {
            var b=new UIRegistryBuilder();b.AddDialog<ApiDialog,ApiQuestion,bool>(new UIViewTypeKey("dialog"),UIViewAsset.Resources("dialog"));
            Assert.Throws<ArgumentException>(()=>b.AddDialog<ApiDialog,ApiQuestion,bool>(new UIViewTypeKey("other"),UIViewAsset.Resources("other")));
            using(var f=new UIFlowTestHarness(b.Build()))Assert.DoesNotThrow(()=>new UIDialogs(f.UI));
        }
    }
    [Category("integration")]
    public sealed class ContextApiTests
    {
        [Test] public void DefaultServices_AreAvailableBeforeUserFactoriesAndCannotBeOverridden()
        {
            var root=new GameObject("api-root",typeof(RectTransform),typeof(UIRoot));root.GetComponent<Canvas>().renderMode=RenderMode.ScreenSpaceOverlay;
            var events=EditModeUIFixture.CreateEvents("api-events");var registry=ScriptableObject.CreateInstance<UIRegistry>();IUIContext ui=null;
            try
            {
                var b=new UIContextBuilder(root.GetComponent<UIRoot>(),registry);
                Assert.Throws<InvalidOperationException>(()=>b.WithServiceFactory<IUINavigator>(_=>null));
                Assert.Throws<InvalidOperationException>(()=>b.WithServiceFactory<IUIDialogs>(_=>null));
                Assert.Throws<InvalidOperationException>(()=>b.WithServiceFactory<IUIPopupQueue>(_=>null));
                ui=b.WithServiceFactory(context=>
                {Assert.That(context.ContextId,Is.Not.EqualTo(default(UIContextId)));Assert.That(context.GetService<IUINavigator>(),Is.Not.Null);Assert.That(context.GetService<IUIDialogs>(),Is.Not.Null);Assert.That(context.GetService<IUIPopupQueue>(),Is.Not.Null);return new object();}).Build();
                Assert.That(ui.ContextId,Is.EqualTo(ui.GetDiagnostics().ContextId));
            }
            finally{ui?.ShutdownAsync().GetAwaiter().GetResult();UnityEngine.Object.DestroyImmediate(root);EditModeUIFixture.DestroyEvents(events);UnityEngine.Object.DestroyImmediate(registry);}
        }
        sealed class Resource : IDisposable
        {readonly Action action;internal Resource(Action action){this.action=action;}public void Dispose()=>action();}
        [Test] public void FailedBuild_StopsDefaultsAndReleasesUserServicesInReverseOrder()
        {
            var root=new GameObject("api-rollback",typeof(RectTransform),typeof(UIRoot));root.GetComponent<Canvas>().renderMode=RenderMode.ScreenSpaceOverlay;
            var events=EditModeUIFixture.CreateEvents("api-rollback-events");var registry=ScriptableObject.CreateInstance<UIRegistry>();IUIContext ui=null;
            IUINavigator navigator=null;IUIPopupQueue queue=null;var order=new List<int>();
            try
            {
                var b=new UIContextBuilder(root.GetComponent<UIRoot>(),registry);
                Assert.Throws<InvalidOperationException>(()=>b.WithServiceFactory(context=>
                {navigator=context.GetService<IUINavigator>();queue=context.GetService<IUIPopupQueue>();return new Resource(()=>order.Add(1));})
                  .WithServiceFactory<IDisposable>(_=>new Resource(()=>order.Add(2))).WithServiceFactory<string>(_=>throw new InvalidOperationException("factory")).Build());
                Assert.That(order,Is.EqualTo(new[]{2,1}));Assert.That(root.transform.childCount,Is.Zero);
                Assert.That(queue.Enqueue(null).GetAwaiter().GetResult().Status,Is.EqualTo(PopupQueueStatus.ContextStopped));
                Assert.Throws<UIException>(()=>navigator.PushAsync<ProbeView>().GetAwaiter().GetResult());
                ui=new UIContextBuilder(root.GetComponent<UIRoot>(),registry).Build();Assert.That(ui.GetService<IUIPopupQueue>(),Is.Not.SameAs(queue));
            }
            finally{ui?.ShutdownAsync().GetAwaiter().GetResult();UnityEngine.Object.DestroyImmediate(root);EditModeUIFixture.DestroyEvents(events);UnityEngine.Object.DestroyImmediate(registry);}
        }
        static UIFlowTestHarness Fixture()
        {var b=new UIRegistryBuilder();b.Add<ProbeView>(new UIViewTypeKey("probe"),UIViewAsset.Resources("probe"),closePolicy:UIClosePolicy.CacheOne);return new UIFlowTestHarness(b.Build());}
        [Test] public void ContextClose_RejectsForeignAndForgedSessionsWithoutClosingEitherContext()
        {
            ProbeView.ResetProbeState();using(var a=Fixture())using(var b=Fixture())
            {
                var one=a.UI.OpenAsync<ProbeView>().GetAwaiter().GetResult();var two=b.UI.OpenAsync<ProbeView>().GetAwaiter().GetResult();
                Assert.That(one.Id,Is.EqualTo(two.Id));Assert.That(one.ContextId,Is.Not.EqualTo(two.ContextId));
                Assert.Throws<UIException>(()=>a.UI.CloseAsync(two).GetAwaiter().GetResult());
                var forged=new Forged(one.ContextId,one.Id);Assert.Throws<UIException>(()=>a.UI.CloseAsync(forged).GetAwaiter().GetResult());
                Assert.That(forged.Called,Is.False);Assert.That(one.State,Is.EqualTo(UIState.Open));Assert.That(two.State,Is.EqualTo(UIState.Open));
                a.UI.CloseAsync(one).GetAwaiter().GetResult();Assert.That(two.State,Is.EqualTo(UIState.Open));
            }
        }
        [Test] public void CachedReopen_ExposesNewSessionAndOldContextCloseCannotCloseIt()
        {
            ProbeView.ResetProbeState();using(var f=Fixture())
            {
                var a=f.UI.OpenAsync<ProbeView>().GetAwaiter().GetResult();var closed=f.UI.CloseAsync(a).GetAwaiter().GetResult();
                var b=f.UI.OpenAsync<ProbeView>().GetAwaiter().GetResult();Assert.That(a.ContextId,Is.EqualTo(f.UI.ContextId));Assert.That(b.ContextId,Is.EqualTo(a.ContextId));
                Assert.That(f.UI.CloseAsync(a).GetAwaiter().GetResult(),Is.SameAs(closed));Assert.That(b.State,Is.EqualTo(UIState.Open));Assert.That(b.Id,Is.Not.EqualTo(a.Id));
            }
        }
        sealed class Forged : IUISession
        {
            public bool Called;public UIContextId ContextId{get;}public UISessionId Id{get;}public UIState State=>UIState.Open;
            public Forged(UIContextId context,UISessionId id){ContextId=context;Id=id;}
            public UniTask<UICompletion> Closed=>UniTask.FromResult<UICompletion>(null);
            public UniTask<UICompletion> CloseAsync(UICloseReason reason=UICloseReason.Owner){Called=true;return Closed;}
        }
    }
}
