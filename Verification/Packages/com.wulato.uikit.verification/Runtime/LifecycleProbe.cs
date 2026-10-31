using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
namespace Wulato.UIKit.Verification
{
    public class LifecycleProbe : MonoBehaviour
    {
        public GameObject SerializedDependency;
        public bool AwakeInitialized;
        public static readonly Dictionary<int,List<string>> Traces = new Dictionary<int,List<string>>();
        public List<string> Events { get { if (!Traces.ContainsKey(GetInstanceID())) Traces[GetInstanceID()] = new List<string>(); return Traces[GetInstanceID()]; } }
        void Awake() { AwakeInitialized=true; Events.Add("Awake"); }
        void OnEnable() { Events.Add("OnEnable"); }
        public void ProbeOnCreate() { Events.Add("OnCreate"); }
        public void Save(string name)
        {
            var dir=Environment.GetEnvironmentVariable("UIKIT_EVIDENCE_DIR");
            if (string.IsNullOrEmpty(dir)) return;
            Directory.CreateDirectory(dir);
            File.WriteAllText(Path.Combine(dir,name+".trace.json"),JsonUtility.ToJson(new Trace {instanceId=GetInstanceID(),events=Events.ToArray(),dependencyPresent=SerializedDependency!=null},true));
        }
        [Serializable] class Trace { public int instanceId; public string[] events; public bool dependencyPresent; }
    }
}
