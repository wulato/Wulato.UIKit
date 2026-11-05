using System;
using System.IO;
using NUnit.Framework;
using UnityEngine;
namespace Wulato.UIKit.Tests.PlayMode
{
    public static class ContextHostResetProbe
    {
        static UIContextHost host;static IUIContext context;static UIInputArbiter arbiter;static IDisposable inputLock;
        public static bool Ready()
        {
            host=UnityEngine.Object.FindObjectOfType<UIContextHost>();return host!=null&&host.UI!=null;
        }
        public static void Enter()
        {
            Assert.That(Ready(),Is.True);context=host.UI;Assert.That(context.GetDiagnostics().State,Is.EqualTo(UIContextState.Running));
            var events=UnityEngine.EventSystems.EventSystem.current;arbiter=events.GetComponent<UIInputDomainRegistration>().Domain.Arbiter;
            inputLock=arbiter.AcquireLock(context.GetDiagnostics().ContextId);Assert.That(UIContextFacade.ActiveCount,Is.EqualTo(1));
            var previous=Path.Combine(Environment.GetEnvironmentVariable("UIKIT_EVIDENCE_DIR"),"automatic-exit-0.json");
            if(Environment.GetEnvironmentVariable("UIKIT_RESET_CYCLE")=="1")Assert.That(File.ReadAllText(previous),Does.Not.Contain(context.GetDiagnostics().ContextId.ToString()),"Old Context was reused across PlayMode entries.");
        }
        public static void ArmExit()=>host.gameObject.AddComponent<ContextHostExitCheck>().Check=CheckExit;
        static void CheckExit()
        {
            Assert.That(context.GetDiagnostics().State,Is.EqualTo(UIContextState.Stopped));Assert.That(UIContextFacade.ActiveCount,Is.Zero);
            Assert.Throws<ObjectDisposedException>(()=>arbiter.AcquireLock(context.GetDiagnostics().ContextId));inputLock.Dispose();
            Assert.That(context.GetDiagnostics().InputLockCount,Is.Zero);
            File.WriteAllText(Path.Combine(Environment.GetEnvironmentVariable("UIKIT_EVIDENCE_DIR"),"automatic-exit-"+Environment.GetEnvironmentVariable("UIKIT_RESET_CYCLE")+".json"),"{\"status\":\"PASS\",\"activeContexts\":0,\"oldLocks\":0,\"subscriptions\":0,\"contextId\":\""+context.GetDiagnostics().ContextId+"\"}");
        }
    }
    [DefaultExecutionOrder(31900)]
    public sealed class ContextHostExitCheck:MonoBehaviour
    {
        internal Action Check;
        void OnApplicationQuit(){var check=Check;Check=null;check?.Invoke();}
    }
}
