using System;
using Cysharp.Threading.Tasks;
using UnityEditor;
using UnityEngine;
namespace Wulato.UIKit.Editor.Authoring
{
    internal static class UIPreviewPage
    {
        public static void Draw(ref UIPreviewProfile profile)
        {
            profile=(UIPreviewProfile)EditorGUILayout.ObjectField("预览配置",profile,typeof(UIPreviewProfile),false);
            EditorGUILayout.HelpBox("使用真实 PlayMode 和独立临时场景。带参或依赖业务服务的 View 需要显式 Adapter；不会模拟完整业务环境。",MessageType.Info);
            if(!UIPreviewController.Active)
            {
                using(new EditorGUI.DisabledScope(EditorApplication.isPlayingOrWillChangePlaymode||EditorApplication.isCompiling))
                {
                    if(GUILayout.Button("创建预览配置"))
                    {
                        var path=EditorUtility.SaveFilePanelInProject("保存预览配置","UIPreviewProfile","asset","选择新资产路径");
                        if(!string.IsNullOrEmpty(path))
                        {
                            if(System.IO.File.Exists(path))throw new InvalidOperationException("Profile path already exists.");
                            profile=ScriptableObject.CreateInstance<UIPreviewProfile>();profile.registry=AssetDatabase.LoadAssetAtPath<UIRegistry>(UICodegenPipeline.RegistryPath);AssetDatabase.CreateAsset(profile,path);Selection.activeObject=profile;
                        }
                    }
                    if(profile&&GUILayout.Button("启动独立预览"))UIPreviewController.Start(profile);
                }
            }
            else
            {
                if(UIPreviewController.Running)
                {
                    var session=UIPreviewController.Session;EditorGUILayout.LabelField("Session",session==null?"正在打开":session.Id+" / "+session.State);
                    var d=UIPreviewController.Diagnostics;if(d!=null)EditorGUILayout.LabelField("Context",d.ContextId+" / "+d.State+"; leases "+d.ActiveLeaseCount+"; pending "+d.PendingCleanupCount);
                    using(new EditorGUI.DisabledScope(!UIPreviewController.CanCommand||session==null||session.State!=UIState.Open))
                    if(GUILayout.Button("关闭界面"))UIPreviewController.CloseAsync().Forget(UIPreviewController.Report);
                    using(new EditorGUI.DisabledScope(!UIPreviewController.CanCommand||(session!=null&&session.State!=UIState.Closed)))
                    if(GUILayout.Button("再次打开"))UIPreviewController.ReopenAsync().Forget(UIPreviewController.Report);
                }
                if(EditorApplication.isPlayingOrWillChangePlaymode)
                {if(GUILayout.Button("停止预览并恢复"))UIPreviewController.StopAsync().Forget(UIPreviewController.Report);}
                else
                {
                    if(GUILayout.Button("恢复原场景"))UIPreviewController.Restore();
                    if(GUILayout.Button("保留现场"))UIPreviewController.Keep();
                }
            }
            if(!string.IsNullOrEmpty(UIPreviewController.LastMessage))EditorGUILayout.HelpBox(UIPreviewController.LastMessage,MessageType.Info);
            if(!string.IsNullOrEmpty(UIPreviewController.RecoveryError))EditorGUILayout.HelpBox(UIPreviewController.RecoveryError,MessageType.Error);
        }
    }
}
