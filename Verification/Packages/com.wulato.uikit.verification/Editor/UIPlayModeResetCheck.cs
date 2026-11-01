using System;
using System.IO;
using System.Reflection;
using UnityEditor;
using UnityEngine;
namespace Wulato.UIKit.Verification.Editor
{
    [InitializeOnLoad]
    public static class UIPlayModeResetCheck
    {
        const string Key="UIKit.T8.Reset.";
        [Serializable] sealed class Evidence {public int schema=1;public int completed;public bool domainReload,sceneReload;public int activeContexts,oldLocks,subscriptions;public string status;}
        static UIPlayModeResetCheck()
        {
            EditorApplication.playModeStateChanged+=Changed;
            EditorApplication.update+=Tick;
        }
        public static void Run()
        {
            SessionState.SetBool(Key+"active",true);SessionState.SetInt(Key+"step",0);
            SessionState.SetString(Key+"out",Environment.GetEnvironmentVariable("UIKIT_EVIDENCE_DIR"));
            SessionState.SetString(Key+"option",Environment.GetEnvironmentVariable("UIKIT_RESET_OPTIONS"));
            SessionState.SetBool(Key+"originalEnabled",EditorSettings.enterPlayModeOptionsEnabled);
            SessionState.SetInt(Key+"originalOptions",(int)EditorSettings.enterPlayModeOptions);
            var option=int.Parse(SessionState.GetString(Key+"option","0"));
            EditorSettings.enterPlayModeOptionsEnabled=true;EditorSettings.enterPlayModeOptions=(EnterPlayModeOptions)option;
            EditorApplication.EnterPlaymode();
        }
        static void Invoke(string method)
        {
            var type=Type.GetType("Wulato.UIKit.Tests.PlayMode.IntegrationResetProbe, Wulato.UIKit.Tests.PlayMode",true);
            type.GetMethod(method,BindingFlags.Public|BindingFlags.Static).Invoke(null,null);
        }
        static void Tick()
        {
            if(!SessionState.GetBool(Key+"active",false))return;
            var step=SessionState.GetInt(Key+"step",0);
            try {
                if(EditorApplication.isPlaying&&!EditorApplication.isPlayingOrWillChangePlaymode)return;
                if(EditorApplication.isPlaying && step%2==0){Environment.SetEnvironmentVariable("UIKIT_RESET_CYCLE",(step/2).ToString());Invoke("Enter");Invoke("ArmExit");SessionState.SetInt(Key+"step",step+1);EditorApplication.ExitPlaymode();}
            }catch(Exception e){Fail(e);}
        }
        static void Changed(PlayModeStateChange state)
        {
            if(!SessionState.GetBool(Key+"active",false))return;
            try {

                if(state==PlayModeStateChange.EnteredEditMode)
                {
                    var step=SessionState.GetInt(Key+"step",0);SessionState.SetInt(Key+"step",step+1);
                    if(step==1)EditorApplication.delayCall+=()=>EditorApplication.EnterPlaymode();
                    else if(step==3)Finish();else throw new Exception("Unexpected play reset step: "+step);
                }
            }catch(Exception e){Fail(e);}
        }
        static void Restore()
        {
            SessionState.SetBool(Key+"active",false);
            EditorSettings.enterPlayModeOptionsEnabled=SessionState.GetBool(Key+"originalEnabled",false);
            EditorSettings.enterPlayModeOptions=(EnterPlayModeOptions)SessionState.GetInt(Key+"originalOptions",0);
        }
        [Serializable] sealed class ExitEvidence {public string status;public int activeContexts,oldLocks,subscriptions;}
        static void Finish()
        {
            int option=int.Parse(SessionState.GetString(Key+"option","0"));
            for(int i=0;i<2;i++)
            {
                var path=Path.Combine(SessionState.GetString(Key+"out",""),"automatic-exit-"+i+".json");
                var exit=JsonUtility.FromJson<ExitEvidence>(File.ReadAllText(path));
                if(exit.status!="PASS"||exit.activeContexts!=0||exit.oldLocks!=0||exit.subscriptions!=0)throw new Exception("Automatic exit evidence failed: "+path);
            }
            var evidence=new Evidence{completed=2,domainReload=(option&1)==0,sceneReload=(option&2)==0,status="PASS"};
            File.WriteAllText(Path.Combine(SessionState.GetString(Key+"out",""),"play-reset.json"),JsonUtility.ToJson(evidence,true));Restore();EditorApplication.Exit(0);
        }
        static void Fail(Exception e){Debug.LogException(e);Restore();EditorApplication.Exit(1);}
    }
}
