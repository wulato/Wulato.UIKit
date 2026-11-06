using System;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.EventSystems;
namespace Wulato.UIKit.Tests.EditMode
{
    [Category("integration")]
    public class DiagnosticsTests
    {
        [Test] public void OffDiagnostics_KeepBaseState_AndRejectWorkerReads()
        {
            var root=new GameObject("Root",typeof(RectTransform),typeof(UIRoot));root.GetComponent<Canvas>().renderMode=RenderMode.ScreenSpaceOverlay;
            var events=EditModeUIFixture.CreateEvents("Events");
            var registry=ScriptableObject.CreateInstance<UIRegistry>();IUIContext ui=null;
            try {
                ui=new UIContextBuilder(root.GetComponent<UIRoot>(),registry).WithSettings(new UIContextSettings{DiagnosticsLevel=UIDiagnosticsLevel.Off}).Build();
                var snapshot=ui.GetDiagnostics();Assert.That(snapshot.Level,Is.EqualTo(UIDiagnosticsLevel.Off));Assert.That(snapshot.State,Is.EqualTo(UIContextState.Running));
                Assert.That(snapshot.Sessions,Is.Empty);Assert.That(snapshot.CacheLru,Is.Empty);Assert.That(snapshot.History,Is.Empty);Assert.That(snapshot.Errors,Is.Empty);
                Exception failure=null;var worker=new System.Threading.Thread(()=>{try{ui.GetDiagnostics();}catch(Exception e){failure=e;}});worker.Start();worker.Join();Assert.That(failure,Is.TypeOf<UIException>());
            } finally {ui?.ShutdownAsync().GetAwaiter().GetResult();UnityEngine.Object.DestroyImmediate(root);EditModeUIFixture.DestroyEvents(events);UnityEngine.Object.DestroyImmediate(registry);foreach(var host in UnityEngine.Object.FindObjectsOfType<UIRuntimeHost>())UnityEngine.Object.DestroyImmediate(host.gameObject);}
        }
    }
}
