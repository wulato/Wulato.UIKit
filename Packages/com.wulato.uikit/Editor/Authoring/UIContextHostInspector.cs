using UnityEditor;
using UnityEngine;
namespace Wulato.UIKit.Editor.Authoring
{
    [CustomEditor(typeof(UIContextHost))]
    internal sealed class UIContextHostInspector : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            using(new EditorGUI.DisabledScope(EditorApplication.isPlaying))DrawDefaultInspector();
            var host=(UIContextHost)target;
            EditorGUILayout.HelpBox("同一 Root 只能有一个装配者；已有业务启动脚本时不要同时自动初始化。",MessageType.Info);
            if(host.LastError!=null)EditorGUILayout.HelpBox(host.LastError.ToString(),MessageType.Error);
            if(host.Context!=null)
            {
                var d=host.Context.GetDiagnostics();EditorGUILayout.LabelField("Context",d.ContextId+" / "+d.State);
                EditorGUILayout.LabelField("Sessions / Pending cleanup",d.Sessions.Count+" / "+d.PendingCleanupCount);
            }
            if(host.ShutdownResult!=null)EditorGUILayout.HelpBox("Shutdown errors: "+host.ShutdownResult.Errors.Count+"; pending: "+host.ShutdownResult.PendingCleanupCount,MessageType.Info);
        }
    }
}
