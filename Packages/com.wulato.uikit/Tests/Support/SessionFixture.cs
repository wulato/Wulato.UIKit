using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using Cysharp.Threading.Tasks;
using NUnit.Framework;
using UnityEngine;
namespace Wulato.UIKit.Tests.Support
{
    public class ProbeView : UIView
    {
        public static readonly List<string> Events=new List<string>();
        public static readonly List<string> Measurements=new List<string>();
        public static Func<UISessionScope,CancellationToken,UniTask> Open;
        public static Func<UICloseReason,CancellationToken,UniTask> Close;
        public static Action Create,Foreground,ClosedHook,Disposing,Disabled;
        public static bool AllowClose=true;
        public static Func<bool> CloseDecision;
        public static UISessionScope Scope; public static UIForegroundScope ForegroundScope; public static CancellationToken OpenToken;
        UISessionScope ownScope;
        protected override void OnCreate() { Events.Add("create");Create?.Invoke(); }
        void Awake() { Events.Add("awake"); }
        protected override UniTask OnOpenAsync(UISessionScope scope,CancellationToken ct) { Events.Add("open");Scope=scope;ownScope=scope;OpenToken=ct;return Open?.Invoke(scope,ct)??UniTask.CompletedTask; }
        protected override bool CanClose(UICloseReason reason) { Events.Add("can-close");return CloseDecision?.Invoke()??AllowClose; }
        protected override UniTask OnCloseAsync(UICloseReason reason,CancellationToken ct) { Events.Add("close");Assert.That(ownScope.Token.IsCancellationRequested,Is.True);Assert.That(ct.IsCancellationRequested,Is.False);return Close?.Invoke(reason,ct)??UniTask.CompletedTask; }
        protected override void OnForegroundEntered(UIForegroundScope scope) { Events.Add("foreground");ForegroundScope=scope;Foreground?.Invoke(); }
        protected override void OnForegroundExited() => Events.Add("background");
        protected override void OnClosed(UICloseReason reason) { Events.Add("closed");ClosedHook?.Invoke(); }
        void OnDisable() => Disabled?.Invoke();
        protected override void OnDispose() { Events.Add("dispose");Disposing?.Invoke(); }
        public void ClickClose() => RequestClose();
        public static void ResetProbeState() { Events.Clear();Measurements.Clear();Open=null;Close=null;Create=null;Foreground=null;ClosedHook=null;Disposing=null;Disabled=null;Scope=null;ForegroundScope=null;AllowClose=true;CloseDecision=null; }
    }
    public sealed class ProbeObserver : IUISessionLifecycleObserver
    {
        public Func<IDisposable> Preparing=()=>new ActionResource(()=>{});
        public int Prepares,Terminates,Terminated;
        public Action OnEnd;
        public IDisposable OnPreparing(UIViewBase view,UIArguments args,UISessionScope scope) { Prepares++;return Preparing(); }
        public void OnTerminating(UISessionScope scope,UICloseReason reason) { Terminates++; }
        public void OnTerminated(UICompletion completion) { Terminated++;OnEnd?.Invoke(); }
    }
    internal sealed class SessionFixture : IDisposable,IUILayoutReady,IUITransition,IUIErrorSink
    {
        public readonly ControlledLoader Loader=new ControlledLoader();
        public readonly FakeViewHostFactory Hosts=new FakeViewHostFactory();
        public readonly FakeInputArbiter Input=new FakeInputArbiter();
        public readonly ManualScheduler Scheduler=new ManualScheduler();
        public readonly List<int> ReportThreads=new List<int>();
        public readonly List<UIException> Errors=new List<UIException>();
        public UIContextEngine UI;
        public bool SinkThrows;
        public Func<CancellationToken,UniTask> Layout,Enter,Exit;
        public int LayoutCalls;
        public SessionFixture(Action<UIRegistryBuilder> configure=null,params IUISessionLifecycleObserver[] observers)
            : this(new UIContextSettings(),configure,observers) { }
        public SessionFixture(UIContextSettings settings,Action<UIRegistryBuilder> configure,params IUISessionLifecycleObserver[] observers)
        {
            ProbeView.ResetProbeState(); var b=new UIRegistryBuilder();
            if(configure==null)b.Add<ProbeView>(new UIViewTypeKey("probe"),UIViewAsset.Resources("probe"));else configure(b);
            UI=new UIContextEngine(new UIContextId(Guid.NewGuid()),new UIRuntimeDependencies(b.Build(),settings.CopyValidated(),Loader,Scheduler,Hosts,this,this,Input,this,observers,new Dictionary<Type,object>{{typeof(SessionFixture),this}}));
        }
        public UniTask WaitLayoutAsync(UIViewBase view,CancellationToken ct) { ct.ThrowIfCancellationRequested();LayoutCalls++;return Layout?.Invoke(ct)??UniTask.CompletedTask; }
        public UITransitionWrites Writes => UITransitionWrites.None;
        public UniTask EnterAsync(UITransitionContext c,CancellationToken ct) => Enter?.Invoke(ct)??UniTask.CompletedTask;
        public UniTask ExitAsync(UITransitionContext c,CancellationToken ct) => Exit?.Invoke(ct)??UniTask.CompletedTask;
        public void Stop(UITransitionContext c) { }
        public void Reset(UITransitionContext c) { }
        public void Report(UIException error) { ReportThreads.Add(Thread.CurrentThread.ManagedThreadId);Errors.Add(error);if(SinkThrows)throw new Exception("sink"); }
        public void Dispose() { ProbeView.Open=null;ProbeView.Close=null;UI.ShutdownAsync().GetAwaiter().GetResult(); }
    }
    public abstract class SessionTestBase
    {
        [SetUp] public void ResetTrace() => ProbeView.ResetProbeState();
        [TearDown] public void SaveTrace()
        {
            var dir=Environment.GetEnvironmentVariable("UIKIT_EVIDENCE_DIR");
            if(!string.IsNullOrEmpty(dir)) { Directory.CreateDirectory(dir);File.WriteAllText(Path.Combine(dir,TestContext.CurrentContext.Test.Name+".trace.json"),JsonUtility.ToJson(new Trace{events=ProbeView.Events.ToArray(),measurements=ProbeView.Measurements.ToArray()})); }
        }
        [Serializable] sealed class Trace { public string[] events,measurements; }
    }
}
