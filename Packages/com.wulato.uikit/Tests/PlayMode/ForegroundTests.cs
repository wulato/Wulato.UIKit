using System;
using System.Collections;
using UnityEngine.TestTools;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using NUnit.Framework;
using UnityEngine;
using Wulato.UIKit.Tests.Support;
using Wulato.UIKit.Tests.Input;
namespace Wulato.UIKit.Tests.PlayMode
{
    public class ExitFailsProbeView : ProbeView
    {
        protected override void OnForegroundExited()=>throw new Exception("foreground exit");
    }
    [Category("input_runtime")]
    public class ForegroundTests : InputArbitrationFixture
    {
        sealed class Runtime : IDisposable,IUILayoutReady,IUIErrorSink
        {
            internal readonly ControlledLoader Loader=new ControlledLoader();
            internal readonly GameObject Root;
            internal readonly UIViewHostFactory Hosts;
            internal readonly UIContextEngine UI;
            internal Runtime(IUIInputArbiter input,bool cache=false,bool exitFailure=false)
            {
                Root=new GameObject("T7Root",typeof(RectTransform),typeof(UIRoot));
                Root.GetComponent<Canvas>().renderMode=RenderMode.ScreenSpaceOverlay;
                Hosts=new UIViewHostFactory(Root.GetComponent<UIRoot>());
                var builder=new UIRegistryBuilder();
                if(exitFailure)builder.Add<ExitFailsProbeView>(new UIViewTypeKey("exit-probe"),UIViewAsset.Resources("probe"));
                else builder.Add<ProbeView>(new UIViewTypeKey("probe"),UIViewAsset.Resources("probe"),closePolicy:cache?UIClosePolicy.CacheOne:UIClosePolicy.Release);
                UI=new UIContextEngine(new UIContextId(Guid.NewGuid()),new UIRuntimeDependencies(builder.Build(),new UIContextSettings().CopyValidated(),Loader,new ManualScheduler(),Hosts,this,new ImmediateUITransition(),input,this,null,null));
            }
            public UniTask WaitLayoutAsync(UIViewBase view,CancellationToken token)=>UniTask.CompletedTask;
            public void Report(UIException error) { }
            public void Dispose(){ProbeView.Foreground=null;UI.ShutdownAsync().GetAwaiter().GetResult();Hosts.Dispose();if(Root)UnityEngine.Object.DestroyImmediate(Root);}
        }
        [SetUp] public void RemoveSyntheticModal()=>Arbiter.Revoke(Scene.Gate.Contexts[0],Scene.Gate.Sessions[0]);
        void Transfer()=>Scene.Gate.TransferOwner(1);
        void Return()=>Arbiter.Revoke(Scene.Gate.Contexts[1],Scene.Gate.Sessions[1]);
        [UnityTest] public IEnumerator CachedView_ReopenedSameFrame_RetainsInputGateAfterDestroyBoundary()
        {
            using(var f=new Runtime(Arbiter,true))
            {
                var first=f.UI.OpenAsync<ProbeView>().GetAwaiter().GetResult();var view=f.Loader.LastView;
                first.CloseAsync().GetAwaiter().GetResult();var next=f.UI.OpenAsync<ProbeView>().GetAwaiter().GetResult();
                Assert.That(f.Loader.CreateCount,Is.EqualTo(1));Assert.That(next.State,Is.EqualTo(UIState.Open));
                yield return null;
                ((IUIInputDriver)Module).Gate.AllReleased=()=>true;Module.Process();
                Assert.That(view.GetComponent<CanvasGroup>(),Is.Not.Null);
                Assert.That(view.GetComponent<CanvasGroup>().interactable,Is.True);
            }
        }
        [Test] public void ForegroundExitFailure_ClosingCollectsErrorWithoutSecondTermination()
        {
            using(var f=new Runtime(Arbiter,false,true))
            {
                f.Loader.ViewType=typeof(ExitFailsProbeView);var session=f.UI.OpenAsync<ExitFailsProbeView>().GetAwaiter().GetResult();
                var result=session.CloseAsync().GetAwaiter().GetResult();
                Assert.That(session.State,Is.EqualTo(UIState.Closed));Assert.That(result.Errors.Count,Is.EqualTo(1));
                Assert.That(ProbeView.Events.FindAll(e=>e=="closed").Count,Is.EqualTo(1));Assert.That(f.Loader.ReleaseCount,Is.EqualTo(1));
            }
        }
        [Test] public void LostForeground_CancelsScope()
        {
            using(var f=new Runtime(Arbiter))
            {
                f.UI.OpenAsync<ProbeView>().GetAwaiter().GetResult();var scope=ProbeView.ForegroundScope;
                bool released=false;scope.Track(new ActionResource(()=>released=true));Transfer();
                Assert.That(scope.Token.IsCancellationRequested,Is.True);Assert.That(released,Is.True);Assert.That(ProbeView.Events,Does.Contain("background"));
            }
        }
        [Test] public void RegainedForeground_CreatesNewScope()
        {
            using(var f=new Runtime(Arbiter))
            {
                var session=f.UI.OpenAsync<ProbeView>().GetAwaiter().GetResult();var old=ProbeView.ForegroundScope;Transfer();Return();
                Assert.That(ProbeView.ForegroundScope,Is.Not.SameAs(old));Assert.That(ProbeView.ForegroundScope.Token.IsCancellationRequested,Is.False);
                Assert.That(f.UI.GetDiagnostics().InputOwner.Value.SessionId,Is.EqualTo(session.Id));
            }
        }
        [Test] public void ForegroundCallbackFails_UsesCorrectTerminalPath()
        {
            using(var f=new Runtime(Arbiter))
            {
                ProbeView.Foreground=()=>throw new Exception("first foreground");
                Assert.Throws<UIException>(()=>f.UI.OpenAsync<ProbeView>().GetAwaiter().GetResult());
                Assert.That(f.Loader.ReleaseCount,Is.EqualTo(1));Assert.That(ProbeView.Events,Does.Not.Contain("close"));
                ProbeView.Foreground=null;var session=f.UI.OpenAsync<ProbeView>().GetAwaiter().GetResult();Transfer();
                ProbeView.Foreground=()=>throw new Exception("restored foreground");Return();
                Assert.That(session.State,Is.EqualTo(UIState.Closed));Assert.That(session.Closed.GetAwaiter().GetResult().Errors.Count,Is.GreaterThan(0));
                Assert.That(f.Loader.ReleaseCount,Is.EqualTo(2));
            }
        }
        [Test] public void RootDestroyed_ReleasesRegistrationAndLocks()
        {
            using(var f=new Runtime(Arbiter))
            {
                f.UI.OpenAsync<ProbeView>().GetAwaiter().GetResult();UnityEngine.Object.DestroyImmediate(f.Root);
                Assert.That(f.UI.ShutdownAsync().Status,Is.EqualTo(UniTaskStatus.Succeeded));
                Scene.Gate.TransferOwner(1);((IUIInputDriver)Module).Gate.AllReleased=()=>true;Module.Process();
                Assert.That(Scene.Events.currentSelectedGameObject,Is.EqualTo(Scene.Spies[1].gameObject));
            }
        }
    }
}
