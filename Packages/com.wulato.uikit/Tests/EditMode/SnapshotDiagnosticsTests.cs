using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
namespace Wulato.UIKit.Tests.EditMode
{
    [Category("diagnostics")]
    public sealed class SnapshotDiagnosticsTests
    {
        sealed class Sink:IUIErrorSink,IDisposable
        {internal readonly List<UIException> Received=new List<UIException>();internal int Disposes;public void Report(UIException error)=>Received.Add(error);public void Dispose()=>Disposes++;}
        sealed class HostileException:Exception{public override string Message=>throw new InvalidOperationException("message getter failed");}
        static UIDiagnosticsSnapshot Snapshot(UIContextId id,UIContextState state=UIContextState.Running,int pending=0)
            =>new UIDiagnosticsSnapshot(id,state,UIDiagnosticsLevel.Basic,1,null,0,0,null,null,0,pending);
        [Test] public void OffDiagnostics_KeepBaseState_AndRejectWorkerReads()
            =>new DiagnosticsTests().OffDiagnostics_KeepBaseState_AndRejectWorkerReads();
        [Test] public void Snapshot_CollectionsAreFrozenAndExcludeBusinessArguments()
        {
            var id=new UIContextId(Guid.NewGuid());var keys=new List<UIViewTypeKey>{new UIViewTypeKey("page")};
            var errors=new List<UIErrorDiagnostics>{new UIErrorDiagnostics(new UIException(UIErrorCode.LoadFailed,"load",id))};
            var snapshot=new UIDiagnosticsSnapshot(id,UIContextState.Running,UIDiagnosticsLevel.Basic,1,null,0,0,null,null,0,0,keys,errors,1);
            keys.Clear();errors.Clear();Assert.That(snapshot.History.Single().Value,Is.EqualTo("page"));Assert.That(snapshot.Errors.Count,Is.EqualTo(1));
            Assert.Throws<NotSupportedException>(()=>((IList<UIViewTypeKey>)snapshot.History).Clear());
            Assert.Throws<NotSupportedException>(()=>((IList<UIErrorDiagnostics>)snapshot.Errors).Clear());
            Assert.That(typeof(UIErrorDiagnostics).GetProperties().Any(p=>typeof(Exception).IsAssignableFrom(p.PropertyType)||typeof(UnityEngine.Object).IsAssignableFrom(p.PropertyType)),Is.False);
            Assert.That(typeof(UIDiagnosticsSnapshot).GetProperties().Any(p=>p.PropertyType==typeof(UIArguments)||p.PropertyType==typeof(IUIContext)||p.PropertyType==typeof(IUISession)),Is.False);
        }
        [Test] public void ErrorRecording_IsBoundedAndForwardsOriginalWithoutOwningSink()
        {
            var sink=new Sink();var recorder=new UIDiagnosticErrorSink(sink,true);var id=new UIContextId(Guid.NewGuid());UIException last=null;
            for(int i=0;i<80;i++){last=new UIException(UIErrorCode.LifecycleFailed,"step-"+i,id,null,new Exception(new string('x',2000)));recorder.Report(last);}
            Assert.That(recorder.Total,Is.EqualTo(80));Assert.That(recorder.Recent.Count(),Is.EqualTo(32));Assert.That(recorder.Recent.First().Stage,Is.EqualTo("step-48"));Assert.That(recorder.Recent.Last().Message.Length,Is.EqualTo(1024));
            Assert.That(sink.Received.Count,Is.EqualTo(80));Assert.That(sink.Received.Last(),Is.SameAs(last));Assert.That(sink.Disposes,Is.Zero);
            recorder.Report(new UIException(UIErrorCode.LoadFailed,"hostile-message",id,null,new HostileException()));Assert.That(recorder.Recent.Last().Message,Is.EqualTo("Error message unavailable"));
            var off=new UIDiagnosticErrorSink(sink,false);off.Report(last);Assert.That(off.Total,Is.Zero);Assert.That(off.Recent,Is.Empty);Assert.That(sink.Received.Last(),Is.SameAs(last));
        }
        [Test] public void TerminalRecord_RoundTripsOnlyValuesAndRejectsInvalidIdentity()
        {
            var type=AppDomain.CurrentDomain.GetAssemblies().Select(a=>a.GetType("Wulato.UIKit.Editor.UIDiagnosticsRecord")).First(t=>t!=null);
            var source=Snapshot(new UIContextId(Guid.NewGuid()),UIContextState.Stopped,3);
            var record=type.GetMethod("Capture",BindingFlags.Static|BindingFlags.NonPublic).Invoke(null,new object[]{source,"2026-11-06T00:00:00Z"});
            var json=UnityEngine.JsonUtility.ToJson(record);var restored=UnityEngine.JsonUtility.FromJson(json,type);
            var snapshot=(UIDiagnosticsSnapshot)type.GetMethod("Restore",BindingFlags.Instance|BindingFlags.NonPublic).Invoke(restored,null);
            Assert.That(snapshot.ContextId,Is.EqualTo(source.ContextId));Assert.That(snapshot.State,Is.EqualTo(UIContextState.Stopped));Assert.That(snapshot.SampleTime,Is.EqualTo(source.SampleTime));Assert.That(snapshot.PendingCleanupCount,Is.EqualTo(3));
            type.GetField("context").SetValue(restored,"invalid");Assert.That(type.GetMethod("Restore",BindingFlags.Instance|BindingFlags.NonPublic).Invoke(restored,null),Is.Null);
        }
        [Test] public void WorkbenchSamples_AreBoundedValueOnlyAndKeepTerminalTime()
        {
            var type=AppDomain.CurrentDomain.GetAssemblies().Select(a=>a.GetType("Wulato.UIKit.Editor.UIDiagnosticsPage")).First(t=>t!=null);
            object Call(string name,params object[] args)=>type.GetMethod(name,BindingFlags.Static|BindingFlags.NonPublic).Invoke(null,args);
            Call("Clear");try
            {
                for(int i=0;i<40;i++)Call("Remember",Snapshot(new UIContextId(Guid.NewGuid())),true);
                var samples=((IEnumerable)type.GetProperty("Samples",BindingFlags.Static|BindingFlags.NonPublic).GetValue(null)).Cast<object>().ToArray();Assert.That(samples.Length,Is.EqualTo(32));
                var id=new UIContextId(Guid.NewGuid());var terminal=Snapshot(id,UIContextState.Stopped,2);Call("Remember",terminal,true);
                object Last()=>((IEnumerable)type.GetProperty("Samples",BindingFlags.Static|BindingFlags.NonPublic).GetValue(null)).Cast<object>().Last();
                var first=Last();var sampleType=first.GetType();Assert.That(sampleType.GetProperty("Live").GetValue(first),Is.EqualTo(false));
                var time=sampleType.GetProperty("CapturedUtc").GetValue(first);Call("Remember",terminal,false);Assert.That(sampleType.GetProperty("CapturedUtc").GetValue(Last()),Is.EqualTo(time));
                Assert.That(sampleType.GetProperties().Select(p=>p.PropertyType),Is.EquivalentTo(new[]{typeof(UIDiagnosticsSnapshot),typeof(bool),typeof(string)}));
            }finally{Call("Clear");}
        }
    }
}
