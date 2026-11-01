using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
namespace Wulato.UIKit.Samples.Editor
{
    // This tool is imported with QuickStart; the other two samples are optional until setup is invoked.
    public static class SampleSetup
    {
        static readonly string[] Names={"QuickStart","Modals","Inventory"};
        static void ReportChecks(params string[] checks) { Debug.Log("UIKit samples: "+string.Join(", ",checks)); }
        public static void CheckAssemblies()
        {
            foreach(var name in Names)if(!AppDomain.CurrentDomain.GetAssemblies().Any(a=>a.GetName().Name=="Wulato.UIKit.Samples."+name))throw new InvalidOperationException("Import all three samples before setup: "+name);
        }
        [MenuItem("Tools/Wulato/UIKit/Samples/1 Generate imported samples")]
        public static void Prepare(){Author();Generate();}
        [MenuItem("Tools/Wulato/UIKit/Samples/2 Complete and wire scenes")]
        public static void Finish(){Complete();FinalizeSamples();}
        const string SampleRoot="Assets/Samples/Wulato UIKit";
        static string ViewName(string name)=>name=="QuickStart"?"SettingsView":name=="Modals"?"StackedModalView":"InventoryView";
        static MonoScript Script(string name,string file)=>AssetDatabase.LoadAssetAtPath<MonoScript>(SampleRoot+"/"+name+"/"+file+".cs");
        public static void Author()
        {
            CheckAssemblies();
            foreach(var name in Names)
            {
                var path=SampleRoot+"/"+name+"/"+ViewName(name)+".prefab";
                if(File.Exists(path))continue;
                var go=new GameObject(ViewName(name),typeof(RectTransform),typeof(UnityEngine.UI.Image));
                try
                {
                    var rect=(RectTransform)go.transform;rect.anchorMin=new Vector2(.15f,.15f);rect.anchorMax=new Vector2(.85f,.85f);rect.offsetMin=rect.offsetMax=Vector2.zero;
                    go.GetComponent<UnityEngine.UI.Image>().color=new Color(.07f,.12f,.2f,.98f);
                    var title=new GameObject("Title",typeof(RectTransform),typeof(UnityEngine.UI.Text));title.transform.SetParent(go.transform,false);
                    var text=title.GetComponent<UnityEngine.UI.Text>();text.font=Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");if(text.font==null)throw new InvalidOperationException("Missing builtin font");
                    text.text=name;text.color=Color.white;text.fontSize=28;text.alignment=TextAnchor.MiddleCenter;
                    var tr=(RectTransform)title.transform;tr.anchorMin=new Vector2(0,.7f);tr.anchorMax=Vector2.one;tr.offsetMin=tr.offsetMax=Vector2.zero;
                    var close=new GameObject("Close",typeof(RectTransform),typeof(UnityEngine.UI.Image),typeof(UnityEngine.UI.Button));close.transform.SetParent(go.transform,false);
                    var cr=(RectTransform)close.transform;cr.anchorMin=new Vector2(.35f,.05f);cr.anchorMax=new Vector2(.65f,.2f);cr.offsetMin=cr.offsetMax=Vector2.zero;
                    var closeLabel=new GameObject("Close label",typeof(RectTransform),typeof(UnityEngine.UI.Text));closeLabel.transform.SetParent(close.transform,false);
                    var ct=closeLabel.GetComponent<UnityEngine.UI.Text>();ct.font=text.font;ct.text="Close";ct.color=Color.black;ct.alignment=TextAnchor.MiddleCenter;
                    var ctr=(RectTransform)closeLabel.transform;ctr.anchorMin=Vector2.zero;ctr.anchorMax=Vector2.one;ctr.offsetMin=ctr.offsetMax=Vector2.zero;
                    close.GetComponent<UnityEngine.UI.Button>().targetGraphic=close.GetComponent<UnityEngine.UI.Image>();
                    if(name=="Inventory")
                    {
                        var scroll=new GameObject("Inventory scroll",typeof(RectTransform),typeof(UnityEngine.UI.Image),typeof(UnityEngine.UI.Mask),typeof(UnityEngine.UI.ScrollRect));scroll.transform.SetParent(go.transform,false);
                        var sr=(RectTransform)scroll.transform;sr.anchorMin=new Vector2(.1f,.25f);sr.anchorMax=new Vector2(.9f,.7f);sr.offsetMin=sr.offsetMax=Vector2.zero;
                        scroll.GetComponent<UnityEngine.UI.Mask>().showMaskGraphic=false;
                        var content=new GameObject("Content",typeof(RectTransform));content.transform.SetParent(scroll.transform,false);
                        var contentRect=(RectTransform)content.transform;contentRect.anchorMin=new Vector2(0,1);contentRect.anchorMax=Vector2.one;contentRect.pivot=new Vector2(.5f,1);contentRect.sizeDelta=new Vector2(0,600);
                        var scrollRect=scroll.GetComponent<UnityEngine.UI.ScrollRect>();scrollRect.viewport=sr;scrollRect.content=contentRect;scrollRect.horizontal=false;
                        for(int i=0;i<12;i++)
                        {
                            var row=new GameObject("Item "+i,typeof(RectTransform),typeof(UnityEngine.UI.Text));row.transform.SetParent(content.transform,false);
                            var rr=(RectTransform)row.transform;rr.anchorMin=new Vector2(0,1);rr.anchorMax=Vector2.one;rr.pivot=new Vector2(.5f,1);rr.sizeDelta=new Vector2(0,48);rr.anchoredPosition=new Vector2(0,-i*50);
                            var label=row.GetComponent<UnityEngine.UI.Text>();label.font=text.font;label.text="Slot "+(i+1);label.color=Color.white;label.fontSize=22;
                        }
                    }
                    var marker=go.AddComponent<UIBindingMarker>();marker.viewScript=Script(name,ViewName(name));marker.typeKey="sample-"+name;
                    marker.layer=name=="QuickStart"?UILayer.Page:UILayer.Modal;marker.multiplicity=name=="Modals"?UIMultiplicity.Multi:UIMultiplicity.Single;
                    marker.closePolicy=name=="Inventory"?UIClosePolicy.CacheOne:UIClosePolicy.Release;
                    marker.bindings=new[]{new UIBindingMarker.Binding{fieldName="titleLabel",target=text},new UIBindingMarker.Binding{fieldName="closeButton",target=close.GetComponent<UnityEngine.UI.Button>()}};
                    PrefabUtility.SaveAsPrefabAsset(go,path);
                }finally{UnityEngine.Object.DestroyImmediate(go);}
            }
            AssetDatabase.SaveAssets();ReportChecks("ThreeSampleAssembliesCompiled","AuthoredPrefabsAndBuiltinFonts");
        }
        public static void Generate(){Wulato.UIKit.Editor.UICodegenPipeline.Generate();ReportChecks("GenerationRequested");}
        public static void Complete(){Wulato.UIKit.Editor.UICodegenPipeline.Complete();Wulato.UIKit.Editor.UICodegenPipeline.Validate();ReportChecks("RoutedBindingsWritten");}
        public static void FinalizeSamples()
        {
            foreach(var name in Names)
            {
                var dir=SampleRoot+"/"+name;var usage=dir+"/"+ViewName(name)+".Usage.cs";
                if(!File.Exists(usage))File.WriteAllText(usage,File.ReadAllText(usage+".txt"));
                var scenePath=dir+"/"+(name=="QuickStart"?"Settings":name)+"Scene.unity";
                var exists=File.Exists(scenePath);
                var scene=exists?UnityEditor.SceneManagement.EditorSceneManager.OpenScene(scenePath):UnityEditor.SceneManagement.EditorSceneManager.NewScene(UnityEditor.SceneManagement.NewSceneSetup.EmptyScene,UnityEditor.SceneManagement.NewSceneMode.Single);
                var go=exists?scene.GetRootGameObjects().Single(g=>g.GetComponent<UIRoot>()!=null):new GameObject("UI Root",typeof(RectTransform),typeof(UIRoot));go.GetComponent<Canvas>().renderMode=RenderMode.ScreenSpaceOverlay;
                var demoType=Script(name,name+"Demo").GetClass();
                var demo=exists?scene.GetRootGameObjects().Select(g=>g.GetComponent(demoType)).Single(c=>c!=null):new GameObject(name+" demo").AddComponent(demoType);
                var so=new SerializedObject(demo);so.FindProperty("root").objectReferenceValue=go.GetComponent<UIRoot>();so.FindProperty("registry").objectReferenceValue=AssetDatabase.LoadAssetAtPath<UIRegistry>(Wulato.UIKit.Editor.UICodegenPipeline.RegistryPath);so.ApplyModifiedPropertiesWithoutUndo();
                UnityEditor.SceneManagement.EditorSceneManager.SaveScene(scene,scenePath);
            }
            AssetDatabase.Refresh();ReportChecks("UsagePartialsInstalled","ThreeScenesSaved");
        }
        public static void Validate()
        {
            CheckAssemblies();Wulato.UIKit.Editor.UICodegenPipeline.Validate();
            var mappings=AssetDatabase.LoadAssetAtPath<UIRegistry>(Wulato.UIKit.Editor.UICodegenPipeline.RegistryPath).CopyValidated();
            foreach(var name in Names)
            {
                var item=mappings.Entries.Single(e=>e.Key.Value=="sample-"+name);
                if(item.ViewType.Assembly.GetName().Name!="Wulato.UIKit.Samples."+name)throw new InvalidOperationException("Wrong sample binding route");
                var prefab=item.Asset.Prefab;
                foreach(var label in prefab.GetComponentsInChildren<UnityEngine.UI.Text>(true))if(label.font==null)throw new InvalidOperationException("Missing font: "+name);
                if(!File.Exists(SampleRoot+"/"+name+"/"+(name=="QuickStart"?"Settings":name)+"Scene.unity"))throw new InvalidOperationException("Missing scene");
                var view=prefab.GetComponent(item.ViewType);var so=new SerializedObject(view);
                foreach(var field in new[]{"titleLabel","closeButton"})if(so.FindProperty(field)?.objectReferenceValue==null)throw new InvalidOperationException("Missing generated binding: "+field);
            }
            ReportChecks("ThreeSampleAssembliesCompiled","SampleRoutesBindingsFontsAndScenesValid");
        }
    }
}
