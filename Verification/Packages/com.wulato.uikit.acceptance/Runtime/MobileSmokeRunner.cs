using System;
using System.IO;
using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using UnityEngine;
namespace Wulato.UIKit.Acceptance
{
    /// <summary>Player-only smoke entry. TouchClose waits for a real UI input event.</summary>
    public sealed class MobileSmokeRunner : MonoBehaviour
    {
        public UnityEngine.UI.Text statusLabel;
        public UIRoot root;public UIRegistry directRegistry,resourceRegistry,flowRegistry;public string buildId;public string phase="phase1";
        [Serializable] sealed class Case {public string name,status="PASS";}
        [Serializable] sealed class Report {public string build_id,status="FAIL",error,editor,phase;public List<Case> cases=new List<Case>();public MobilePerformance performance;}
        public static UIArguments GeneratedArguments(string name)=>name=="ViewA"?UIArguments.From(73):UIArguments.None;
        async void Start()
        {
            var report=new Report{build_id=buildId,editor=Application.unityVersion,phase=phase};IUIContext ui=null;
            if(statusLabel)statusLabel.text="Running smoke checks. Tap the green Close button when shown.";
            try
            {
                ui=new UIContextBuilder(root,directRegistry).Build();
                var direct=await ui.OpenAsync<MobileSmokeView,MobileSmokeArgs>(new MobileSmokeArgs(17));
                Require(UnityEngine.Object.FindObjectOfType<MobileSmokeView>().Value==17,"direct args");report.cases.Add(new Case{name="DirectReference"});
                var done=await direct.CloseAsync();Require(done.Errors.Count==0,"close");
                foreach(var name in new[]{"ViewA","ViewB","DefaultView"})
                {
                    // No runtime type names or generic references retain these business types.
                    // The generated serialized providers must survive high managed stripping.
                    var mapped=await ui.OpenAsync(new UIViewTypeKey("generated-"+name),GeneratedArguments(name));
                    var mappedCompletion=await mapped.CloseAsync();Require(mappedCompletion.Errors.Count==0,"stripped mapping "+name);
                }
                report.cases.Add(new Case{name="StrippedGeneratedMapping"});
                await ui.ShutdownAsync();await UniTask.NextFrame();ui=null;
                ui=new UIContextBuilder(root,resourceRegistry).Build();
                var resource=await ui.OpenAsync<MobileSmokeView,MobileSmokeArgs>(new MobileSmokeArgs(23));
                Require(UnityEngine.Object.FindObjectOfType<MobileSmokeView>().Value==23,"Resources args");report.cases.Add(new Case{name="Resources"});
                var first=UnityEngine.Object.FindObjectOfType<MobileSmokeView>().GetInstanceID();await resource.CloseAsync();
                var erased=await ui.OpenAsync(new UIViewTypeKey("mobile-resource"),UIArguments.From(new MobileSmokeArgs(41)));
                var view=UnityEngine.Object.FindObjectOfType<MobileSmokeView>();Require(view.Value==41,"erased args");report.cases.Add(new Case{name="TypeErased"});
                Require(view.GetInstanceID()==first,"cache reuse");report.cases.Add(new Case{name="CacheReopen"});
                // Never invoke onClick or ExecuteEvents: the physical touch must traverse the selected driver.
                var completion=await erased.Closed;Require(completion.Reason==UICloseReason.UserRequest&&completion.Errors.Count==0,"touch close");report.cases.Add(new Case{name="TouchClose"});
                if(statusLabel)statusLabel.text="Automated checks running. Please wait without touching.";
                var shutdown=await ui.ShutdownAsync();Require(shutdown.Errors.Count==0&&shutdown.PendingCleanupCount==0,"shutdown");
                Require(ui.GetDiagnostics().ActiveLeaseCount==0,"leases after shutdown");report.cases.Add(new Case{name="Shutdown"});
                if(phase=="phase2"||phase=="uikit05")
                {
#if UIKIT_PHASE2_SMOKE || UIKIT_05_SMOKE
                    await PhaseTwoSmoke.RunAsync(root,flowRegistry,name=>report.cases.Add(new Case{name=name}));
#else
                    throw new InvalidOperationException("Phase2 requires all extension assemblies.");
#endif
                }
                if(phase=="uikit05")
                {
#if UIKIT_05_SMOKE
                    await Mobile05Smoke.RunAsync(root,flowRegistry,directRegistry,name=>report.cases.Add(new Case{name=name}));
                    report.performance=await Mobile05Smoke.MeasureAsync(root,resourceRegistry);
                    report.cases.Add(new Case{name="PerformanceCacheCycles"});
#else
                    throw new InvalidOperationException("Missing UIKIT_05_SMOKE contract assembly.");
#endif
                }
                report.status="PASS";
            }
            catch(Exception e){report.error=e.ToString();Debug.LogException(e);}
            finally
            {
                if(ui!=null)try{await ui.ShutdownAsync();}catch(Exception e){Debug.LogException(e);}
                if(statusLabel)statusLabel.text=report.status=="PASS"?"PASS: "+report.cases.Count+" checks completed. Return Home to exit.":"FAIL: "+(report.error??"Unknown error").Split('\n')[0];
                File.WriteAllText(Path.Combine(Application.persistentDataPath,"uikit-smoke.json"),JsonUtility.ToJson(report,true));
                Debug.Log("UIKIT_MOBILE_SMOKE "+JsonUtility.ToJson(report));
            }
        }
        static void Require(bool condition,string message){if(!condition)throw new InvalidOperationException(message);}
    }
}
