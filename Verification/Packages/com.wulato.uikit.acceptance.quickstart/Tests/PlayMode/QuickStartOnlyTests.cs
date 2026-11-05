#if UNITY_EDITOR
using System;
using System.Collections;
using System.IO;
using System.Linq;
using Cysharp.Threading.Tasks;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;
namespace Wulato.UIKit.Acceptance.QuickStart.Tests
{
    [Category("quickstart_only")]
    public class QuickStartOnlyTests
    {
        [UnityTest] public IEnumerator VersionedQuickStartOnly_ButtonCloseAndReopen()=>UniTask.ToCoroutine(async()=>{
            IUIContext ui=null;
            try
            {
                foreach(var name in new[]{"Modals","Inventory"})Assert.That(AppDomain.CurrentDomain.GetAssemblies().Any(a=>a.GetName().Name=="Wulato.UIKit.Samples."+name),Is.False);
                var scenes=AssetDatabase.FindAssets("SettingsScene t:Scene",new[]{"Assets/Samples"}).Select(AssetDatabase.GUIDToAssetPath).ToArray();Assert.That(scenes.Length,Is.EqualTo(1));
                Assert.That(scenes[0].Split('/').Length,Is.EqualTo(6));
                EditorSceneManager.LoadSceneInPlayMode(scenes[0],new LoadSceneParameters(LoadSceneMode.Single));
                MonoBehaviour demo=null;
                for(int i=0;i<120;i++)
                {
                    await UniTask.NextFrame();
                    demo=UnityEngine.Object.FindObjectsOfType<MonoBehaviour>().FirstOrDefault(b=>b.GetType().FullName=="Wulato.UIKit.Samples.QuickStart.QuickStartDemo");
                    if(demo!=null)ui=(IUIContext)demo.GetType().GetProperty("Context").GetValue(demo);
                    if(ui!=null&&ui.GetDiagnostics().Sessions.Any(s=>s.State==UIState.Open))break;
                }
                Assert.That(ui,Is.Not.Null);Assert.That(ui.GetDiagnostics().Sessions.Single().State,Is.EqualTo(UIState.Open));
                var view=UnityEngine.Object.FindObjectsOfType<UIViewBase>().Single();var button=view.GetComponentInChildren<Button>();Assert.That(button,Is.Not.Null);
                var before=ui.GetDiagnostics().Sessions.Single().Id;
                button.onClick.Invoke();for(int i=0;i<120&&ui.GetDiagnostics().Sessions.Count!=0;i++)await UniTask.NextFrame();
                Assert.That(ui.GetDiagnostics().Sessions,Is.Empty);
                await (UniTask)demo.GetType().GetMethod("OpenAsync").Invoke(demo,null);
                Assert.That(ui.GetDiagnostics().Sessions.Single().Id,Is.Not.EqualTo(before));
                await ui.CloseAllAsync();var stopped=await ui.ShutdownAsync();Assert.That(stopped.PendingCleanupCount,Is.Zero);
                File.WriteAllText(Path.Combine(Environment.GetEnvironmentVariable("UIKIT_EVIDENCE_DIR"),"VersionedQuickStartOnly_ButtonCloseAndReopen.trace.json"),JsonUtility.ToJson(new Trace{scene=scenes[0],first=before.ToString(),finalState=ui.GetDiagnostics().State.ToString(),sampleAssemblies=AppDomain.CurrentDomain.GetAssemblies().Where(a=>a.GetName().Name.StartsWith("Wulato.UIKit.Samples.")).Select(a=>a.GetName().Name).ToArray()},true));
            }
            finally{if(ui!=null)await ui.ShutdownAsync();}
        });
        [Serializable] sealed class Trace{public string scene,first,finalState;public string[] sampleAssemblies;}
    }
}
#endif
