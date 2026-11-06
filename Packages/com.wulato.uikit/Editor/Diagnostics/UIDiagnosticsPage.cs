using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
namespace Wulato.UIKit.Editor
{
    // Stored samples contain values only: no Context, Session, View, arguments, sink or Exception.
    internal sealed class UIDiagnosticsSample
    {
        public UIDiagnosticsSnapshot Snapshot { get; }
        public bool Live { get; }
        public string CapturedUtc { get; }
        internal UIDiagnosticsSample(UIDiagnosticsSnapshot snapshot,bool live,string capturedUtc)
        {Snapshot=snapshot;Live=live;CapturedUtc=capturedUtc;}
    }
    [InitializeOnLoad]
    internal static class UIDiagnosticsPage
    {
        static readonly List<UIDiagnosticsSample> samples=new List<UIDiagnosticsSample>();
        static readonly List<string> notices=new List<string>();
        static string selected;
        static bool paused;
        static double nextCapture;
        static UIDiagnosticsPage()
        {EditorApplication.playModeStateChanged+=state=>{if(state==PlayModeStateChange.ExitingEditMode)Clear();};}
        internal static void Clear(){samples.Clear();notices.Clear();selected=null;nextCapture=0;}
        internal static IReadOnlyList<UIDiagnosticsSample> Samples=>samples.AsReadOnly();
        internal static void Remember(UIDiagnosticsSnapshot snapshot,bool live)
        {
            if(snapshot==null)return;
            int index=samples.FindIndex(s=>s.Snapshot.ContextId.Equals(snapshot.ContextId));
            var previous=index<0?null:samples[index];
            var stamp=previous!=null&&previous.Snapshot.SampleTime==snapshot.SampleTime?previous.CapturedUtc:DateTime.UtcNow.ToString("O");
            var sample=new UIDiagnosticsSample(snapshot,live&&snapshot.State!=UIContextState.Stopped,stamp);
            if(index>=0)samples.RemoveAt(index);
            samples.Add(sample);if(samples.Count>32)samples.RemoveAt(0);
        }
        internal static void Capture()
        {
            for(int i=0;i<samples.Count;i++)
            {var old=samples[i];samples[i]=new UIDiagnosticsSample(old.Snapshot,false,old.CapturedUtc);}
            notices.Clear();
            if(Application.isPlaying)
            {
                foreach(var snapshot in UIContextFacade.CaptureDiagnostics())Remember(snapshot,true);
                foreach(var host in UnityEngine.Object.FindObjectsOfType<UIContextHost>(true))
                {
                    if(host.UI!=null)Remember(host.UI.GetDiagnostics(),true);
                    else if(host.LastError!=null&&notices.Count<16)notices.Add(host.name+": "+host.LastError.Message);
                }
            }
            Remember(Authoring.UIPreviewController.Diagnostics,Authoring.UIPreviewController.Running);
        }
        public static void Draw()
        {
            paused=EditorGUILayout.Toggle("暂停采样（仅冻结面板）",paused);
            if(GUILayout.Button("清除面板样本"))Clear();
            if(!paused&&EditorApplication.timeSinceStartup>=nextCapture){Capture();nextCapture=EditorApplication.timeSinceStartup+.25;}
            foreach(var notice in notices)EditorGUILayout.HelpBox(notice,MessageType.Error);
            if(samples.Count==0){EditorGUILayout.HelpBox("PlayMode 中采样已装配的 Context；不保留可写运行对象。",MessageType.Info);return;}
            var labels=samples.Select(s=>s.Snapshot.ContextId+" / "+s.Snapshot.State).ToArray();
            int current=samples.FindIndex(s=>s.Snapshot.ContextId.ToString()==selected);
            int next=EditorGUILayout.Popup("Context",Mathf.Max(0,current),labels);selected=samples[next].Snapshot.ContextId.ToString();
            DrawSample(samples[next]);
        }
        internal static void DrawSample(UIDiagnosticsSample sample)
        {
            var d=sample.Snapshot;
            EditorGUILayout.LabelField(sample.Live?"运行中采样":d.State==UIContextState.Stopped?"终态快照（非实时）":"已脱离运行集（最后采样）",EditorStyles.boldLabel);
            EditorGUILayout.LabelField("采样 UTC / 引擎时间",sample.CapturedUtc+" / "+d.SampleTime.ToString("F3"));
            EditorGUILayout.LabelField("Context / Diagnostics",d.ContextId+" / "+d.State+" / "+d.Level);
            EditorGUILayout.LabelField("租约 / 隔离 / PendingCleanup",d.ActiveLeaseCount+" / "+d.IsolatedLeaseCount+" / "+d.PendingCleanupCount);
            EditorGUILayout.LabelField("输入锁",d.InputLockCount.ToString());
            var owner=d.InputOwner;EditorGUILayout.LabelField("输入 Owner",owner.HasValue?owner.Value.ContextId+" / "+owner.Value.SessionId+" / generation "+owner.Value.Generation:"无");
            EditorGUILayout.LabelField("Session（"+d.Sessions.Count+"）",EditorStyles.boldLabel);
            foreach(var session in d.Sessions.Take(128))EditorGUILayout.LabelField(session.Id+" / "+session.Type+" / "+session.State);
            EditorGUILayout.LabelField("页面历史（键，不含业务参数）",EditorStyles.boldLabel);
            foreach(var page in d.History.Take(128))EditorGUILayout.LabelField(page.ToString());
            EditorGUILayout.LabelField("缓存 LRU",EditorStyles.boldLabel);
            foreach(var item in d.CacheLru.Take(128))EditorGUILayout.LabelField(item.ToString());
            EditorGUILayout.LabelField("已报告错误（最近 "+d.Errors.Count+" / 总计 "+d.TotalErrorCount+"）",EditorStyles.boldLabel);
            foreach(var error in d.Errors)EditorGUILayout.HelpBox(error.Code+" / "+error.Stage+" / "+error.SessionId+"\n"+error.Message,MessageType.Warning);
            if(d.Sessions.Count>128||d.History.Count>128||d.CacheLru.Count>128)EditorGUILayout.HelpBox("每组最多展示128行；快照总数显示在标题。",MessageType.Info);
            if(!sample.Live)EditorGUILayout.HelpBox("最后采样不会追踪之后的迟到清理；PendingCleanup 不代表当前物理资源已经释放。",MessageType.Info);
        }
    }
}
