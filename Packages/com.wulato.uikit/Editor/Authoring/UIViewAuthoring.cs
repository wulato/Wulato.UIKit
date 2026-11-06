using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Compilation;
using UnityEngine;
namespace Wulato.UIKit.Editor.Authoring
{
    internal static class UIViewAuthoring
    {
        public static void ValidateNames(string ns,string name)
        {
            if(!UICodegenPipeline.Identifier(name)||(!string.IsNullOrEmpty(ns)&&ns.Split('.').Any(s=>!UICodegenPipeline.Identifier(s))))throw new ArgumentException("类名和命名空间必须是合法 C# 标识符。");
        }
        public static void WriteSource(string path,string ns,string name,Type args)
        {
            ValidateNames(ns,name);
            if(args!=null&&(args.ContainsGenericParameters||args.IsPointer||args.IsByRef||args==typeof(void)||!args.IsVisible))throw new ArgumentException("参数类型必须已编译、公开且不包含开放泛型。");
            if(!path.StartsWith("Assets/",StringComparison.Ordinal)||path.Split('/').Any(p=>p==".."))throw new ArgumentException("脚本必须位于 Assets 下。");
            if(args!=null&&(args.Assembly.GetName().Name.StartsWith("UnityEditor",StringComparison.Ordinal)||CompilationPipeline.GetAssemblies(AssembliesType.Editor).Any(a=>a.name==args.Assembly.GetName().Name&&(a.flags&AssemblyFlags.EditorAssembly)!=0)))throw new ArgumentException("参数类型不能来自 Editor-only 程序集。");
            if(File.Exists(path))throw new IOException("拒绝覆盖已有脚本："+path);
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            File.WriteAllText(path,"// User-owned View. Generated bindings live separately.\n"+(string.IsNullOrEmpty(ns)?"":"namespace "+ns+" {\n")+"public partial class "+name+" : global::Wulato.UIKit.UIView"+(args==null?"":"<"+UICodegenPipeline.TypeName(args)+">")+" { }\n"+(string.IsNullOrEmpty(ns)?"":"}\n"));
        }
        public static void WriteTemplate(string path,string ns,string name,Type args,int template,bool presenter)
        {
            if(template<0||template>2)throw new ArgumentOutOfRangeException(nameof(template));
            if(template==2&&args!=null)throw new ArgumentException("Dialog 模板自带 Request/bool 结果；请在创建后编辑业务类型。");
            var companion=Path.Combine(Path.GetDirectoryName(path),name+"Presenter.cs").Replace('\\','/');
            if(presenter&&File.Exists(companion))throw new IOException("拒绝覆盖已有 Presenter："+companion);
            WriteSource(path,ns,name,args);
            var prefix=string.IsNullOrEmpty(ns)?"":"namespace "+ns+" {\n";
            var suffix=string.IsNullOrEmpty(ns)?"":"}\n";
            if(template==2)
                File.WriteAllText(path,"// User-owned Dialog; generated bindings remain separate.\n"+prefix+
                    "public sealed class "+name+"Request : global::Wulato.UIKit.Dialogs.IDialogRequest<bool> { }\n"+
                    "public partial class "+name+" : global::Wulato.UIKit.UIView<global::Wulato.UIKit.Dialogs.DialogArguments<"+name+"Request,bool>> {\n"+
                    "global::Wulato.UIKit.Dialogs.IDialogResponder<bool> responder; global::Wulato.UIKit.UISessionScope scope;\n"+
                    "protected override global::Cysharp.Threading.Tasks.UniTask OnOpenAsync(global::Wulato.UIKit.Dialogs.DialogArguments<"+name+"Request,bool> args, global::Wulato.UIKit.UISessionScope session, global::System.Threading.CancellationToken ct) { ct.ThrowIfCancellationRequested(); responder=args.Responder; scope=session; return global::Cysharp.Threading.Tasks.UniTask.CompletedTask; }\n"+
                    "public void Submit(bool result) { if(scope!=null && scope.IsCurrent) responder.TrySubmit(result); }\n"+
                    "public void Cancel() { if(scope!=null && scope.IsCurrent) responder.TryCancel(); }\n"+
                    "protected override void OnClosed(global::Wulato.UIKit.UICloseReason reason) { scope=null; responder=null; }\n}\n"+suffix);
            if(presenter)
                File.WriteAllText(companion,"// Optional, user-owned Presenter. Opt in via builder.UsePresentation(new "+name+"PresenterFactory()).\n"+prefix+
                    "public sealed class "+name+"Presenter : global::Wulato.UIKit.Presentation.IUIPresenter {\n"+
                    name+" view; global::Wulato.UIKit.UISessionScope scope;\n"+
                    "public "+name+"Presenter("+name+" view, global::Wulato.UIKit.UISessionScope scope) { this.view=view; this.scope=scope; }\n"+
                    "public void Bind() { if(!scope.IsCurrent) return; /* Bind events here; keep Models caller-owned. */ }\n"+
                    "public void Dispose() { /* Unbind your events here. */ view=null; scope=null; }\n}\n"+
                    "public sealed class "+name+"PresenterFactory : global::Wulato.UIKit.Presentation.IPresenterFactory {\n"+
                    "public global::Wulato.UIKit.Presentation.IUIPresenter Create(global::Wulato.UIKit.UIViewBase view, global::Wulato.UIKit.UIArguments args, global::Wulato.UIKit.UISessionScope scope) { return view is "+name+" typed ? new "+name+"Presenter(typed,scope) : null; }\n}\n"+suffix);
        }
        public static void ValidatePrefab(string path)
        {
            var asset=AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if(asset==null||!(asset.transform is RectTransform)||!asset.activeSelf)throw new ArgumentException("请选择根节点启用的 RectTransform Prefab："+path);
            if(PrefabUtility.GetPrefabAssetType(asset)!=PrefabAssetType.Regular)throw new ArgumentException("首版仅支持普通 Prefab；Variant 请使用现有手工流程。");
            if(asset.GetComponentsInChildren<UIBindingMarker>(true).Any(m=>m.gameObject!=asset))throw new ArgumentException("绑定 Marker 必须唯一并位于根节点。");
        }
        public static void Configure(string path,MonoScript script)=>ConfigureCore(path,script,null);
        public static void ConfigureNew(string path,MonoScript script,int template)=>ConfigureCore(path,script,template);
        static void ConfigureCore(string path,MonoScript script,int? template)
        {
            ValidatePrefab(path);var type=script==null?null:script.GetClass();
            if(type==null||!type.IsPublic||type.IsNested||type.IsAbstract||type.ContainsGenericParameters||!typeof(UIViewBase).IsAssignableFrom(type))throw new ArgumentException("请选择已编译的公开顶层 UIView partial 脚本。");
            var source=File.ReadAllText(AssetDatabase.GetAssetPath(script));
            if(!System.Text.RegularExpressions.Regex.IsMatch(source,@"\bpartial\s+class\s+"+type.Name+@"\b"))throw new ArgumentException("View 必须声明为 partial；不修改已有脚本。");
            var asset=AssetDatabase.LoadAssetAtPath<GameObject>(path);var views=asset.GetComponents<UIViewBase>();var old=asset.GetComponent<UIBindingMarker>();
            if(views.Length>1||views.Any(v=>v.GetType()!=type||!v.enabled)||(old!=null&&old.viewScript!=null&&old.viewScript!=script))throw new InvalidOperationException("Prefab 已绑定其他 View，拒绝覆盖："+path);
            var go=PrefabUtility.LoadPrefabContents(path);
            try
            {
                var marker=go.GetComponent<UIBindingMarker>();bool created=marker==null;if(created)marker=go.AddComponent<UIBindingMarker>();
                marker.viewScript=script;if(string.IsNullOrEmpty(marker.typeKey))marker.typeKey=AssetDatabase.AssetPathToGUID(AssetDatabase.GetAssetPath(script));
                if(created)
                {
                    marker.backAction=template.HasValue?UIBackAction.RequestClose:UIBackAction.None;
                    if(template.HasValue)
                    {
                        if(template<0||template>2)throw new ArgumentOutOfRangeException(nameof(template));
                        marker.layer=template==0?UILayer.Page:UILayer.Modal;
                        marker.multiplicity=template==0?UIMultiplicity.Single:UIMultiplicity.Multi;
                        marker.closePolicy=UIClosePolicy.Release;
                    }
                }
                if(go.GetComponent(type)==null)go.AddComponent(type);
                // Validate complete bindings and route before committing any Prefab content.
                if(marker.bindings==null)throw new ArgumentException("Bindings 不能为 null。");
                if(marker.bindings.Where(b=>b!=null).GroupBy(b=>b.fieldName,StringComparer.Ordinal).Any(g=>g.Count()>1))throw new ArgumentException("存在重复绑定字段名。");
                foreach(var b in marker.bindings)
                    if(b==null||!UICodegenPipeline.Identifier(b.fieldName)||b.target==null||(!b.target.transform.IsChildOf(go.transform)&&b.target.gameObject!=go))throw new ArgumentException("绑定名无效或组件不属于当前 Prefab。");
                var route=UIBindingAssemblyRouter.Resolve(AssetDatabase.GetAssetPath(script));UIBindingAssemblyRouter.ValidateLayout(route);
                PrefabUtility.SaveAsPrefabAsset(go,path);
            }
            finally{PrefabUtility.UnloadPrefabContents(go);}
        }
        public static void CreatePrefab(string path)
        {
            if(File.Exists(path))throw new IOException("拒绝覆盖已有 Prefab："+path);
            var go=new GameObject(Path.GetFileNameWithoutExtension(path),typeof(RectTransform));
            try{PrefabUtility.SaveAsPrefabAsset(go,path);}finally{UnityEngine.Object.DestroyImmediate(go);}
        }
    }
}
