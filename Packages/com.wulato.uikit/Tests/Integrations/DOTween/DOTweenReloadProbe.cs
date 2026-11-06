using System;
using System.IO;
using Cysharp.Threading.Tasks;
using NUnit.Framework;
using UnityEngine;
using Wulato.UIKit.Integrations.DOTween;
namespace Wulato.UIKit.DOTween.Tests
{
    // Invoked by the existing two-entry Host reload executor when this optional assembly exists.
    public static class DOTweenReloadProbe
    {
        static Surface surface;static DOTweenUITransition transition;static UniTask operation;
        public static void Enter()
        {
            Assert.That(surface,Is.Null,"A playback owner survived the previous Play session.");
            surface=new Surface();transition=new DOTweenUITransition(new DOTweenTransitionSettings{Duration=30,Effects=DOTweenUIEffects.Fade|DOTweenUIEffects.Scale|DOTweenUIEffects.Slide});
            operation=transition.EnterAsync(surface.Context,System.Threading.CancellationToken.None);Assert.That(operation.Status,Is.EqualTo(UniTaskStatus.Pending));
        }
        public static void ArmExit()=>surface.Fixture.LastView.gameObject.AddComponent<DOTweenExitProbe>().Check=Exit;
        static void Exit()
        {
            transition.Stop(surface.Context);Assert.That(operation.Status,Is.EqualTo(UniTaskStatus.Canceled));try{operation.GetAwaiter().GetResult();}catch(OperationCanceledException){}
            surface.Dispose();Assert.That(surface.Fixture.UI.GetDiagnostics().ActiveLeaseCount,Is.Zero);Assert.That(surface.Fixture.UI.GetDiagnostics().InputLockCount,Is.Zero);
            surface=null;transition=null;operation=default;
            File.WriteAllText(Path.Combine(Environment.GetEnvironmentVariable("UIKIT_EVIDENCE_DIR"),"dotween-exit-"+Environment.GetEnvironmentVariable("UIKIT_RESET_CYCLE")+".json"),"{\"status\":\"PASS\",\"activePlaybacks\":0,\"version\":\""+DG.Tweening.DOTween.Version+"\"}");
        }
    }
    [DefaultExecutionOrder(31800)]
    public sealed class DOTweenExitProbe:MonoBehaviour
    {
        internal Action Check;
        void OnApplicationQuit(){var action=Check;Check=null;action?.Invoke();}
    }
}
