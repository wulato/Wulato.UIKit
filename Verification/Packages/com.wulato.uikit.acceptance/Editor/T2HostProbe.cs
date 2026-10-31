using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.PackageManager;
using UnityEngine;
namespace Wulato.UIKit.Acceptance.Editor
{
    // Verification-only; never a runtime or main-package dependency.
    public static class T2HostProbe
    {
        [Serializable] private sealed class PackageData { public string name; public string version; public string resolvedPath; public string source; }
        [Serializable] private sealed class Report { public string editor; public PackageData[] packages; }
        public static void Prepare()
        {
            var packages=UnityEditor.PackageManager.PackageInfo.GetAllRegisteredPackages().Where(p=>p.name=="com.wulato.uikit" || p.name=="com.cysharp.unitask" || p.name=="com.unity.ugui")
                .Select(p=>new PackageData{name=p.name,version=p.version,resolvedPath=p.resolvedPath,source=p.source.ToString()}).ToArray();
            var report=new Report{editor=Application.unityVersion,packages=packages};
            var folder=Environment.GetEnvironmentVariable("UIKIT_EVIDENCE_DIR");
            if(string.IsNullOrEmpty(folder))throw new InvalidOperationException("Missing evidence directory.");
            File.WriteAllText(Path.Combine(folder,"dependency-snapshot.json"),JsonUtility.ToJson(report,true));
            var ugui=Application.unityVersion.StartsWith("6000.") ? "2.0.0" : "1.0.0";
            foreach(var requirement in new[]{("com.wulato.uikit","0.1.0"),("com.cysharp.unitask","2.5.11"),("com.unity.ugui",ugui)})
                if(!packages.Any(p=>p.name==requirement.Item1 && p.version==requirement.Item2))throw new InvalidOperationException("Resolved package mismatch: "+requirement.Item1);
            Wulato.UIKit.Verification.Editor.FixtureBuilder.Create();
            Debug.Log("UIKIT_T2_DEPENDENCIES_OK");
        }
    }
}
