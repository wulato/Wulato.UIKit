using System;
using System.IO;
using System.Linq;
using NUnit.Framework;
using UnityEditor.PackageManager;
using UnityEngine;
namespace Wulato.UIKit.InputSystem.Tests
{
    public class AdapterPackageTests
    {
        [Serializable] class Dependency { public string name,version; }
        [Serializable] class Manifest { public string name,version; public Dependencies dependencies; }
        [Serializable] class Dependencies { }
        [Serializable] class Assembly { public string[] references; }
        string PackagePath => PackageInfo.FindForAssembly(typeof(InputSystemUIInputDriver).Assembly).resolvedPath;
        [Test,Category("input_package")] public void AdapterManifest_PinsCoreAndInputVersions()
        {
            var path=Path.Combine(PackagePath,"package.json");
            var text=File.ReadAllText(path);var manifest=JsonUtility.FromJson<Manifest>(text);
            Assert.That(manifest.name,Is.EqualTo("com.wulato.uikit.input-system"));Assert.That(manifest.version,Is.EqualTo("0.2.0"));
            StringAssert.IsMatch("\"com.wulato.uikit\"\\s*:\\s*\"0.2.0\"",text);
            StringAssert.IsMatch("\"com.unity.inputsystem\"\\s*:\\s*\"1.17.0\"",text);
            Assert.That(PackageInfo.FindForAssembly(typeof(UIViewBase).Assembly).version,Is.EqualTo("0.2.0"));
            Assert.That(PackageInfo.FindForAssembly(typeof(UnityEngine.InputSystem.InputAction).Assembly).version,Is.EqualTo("1.17.0"));
            Save();
        }
        [Test,Category("input_package")] public void AdapterAssemblyReferences_OnlyPointToAllowedRuntimeAssemblies()
        {
            var refs=JsonUtility.FromJson<Assembly>(File.ReadAllText(Path.Combine(PackagePath,"Runtime/Wulato.UIKit.InputSystem.asmdef"))).references;
            CollectionAssert.AreEquivalent(new[]{"Wulato.UIKit.Core","Wulato.UIKit.UGUI","UnityEngine.UI","Unity.InputSystem"},refs);
            foreach(var assembly in new[]{typeof(UIViewBase).Assembly,typeof(UIInputArbiter).Assembly})
                Assert.That(assembly.GetReferencedAssemblies().Any(a=>a.Name=="Unity.InputSystem"||a.Name=="Wulato.UIKit.InputSystem"),Is.False);
            Save();
        }
        void Save()
        {
            var dir=Environment.GetEnvironmentVariable("UIKIT_EVIDENCE_DIR");if(!string.IsNullOrEmpty(dir))File.WriteAllText(Path.Combine(dir,TestContext.CurrentContext.Test.Name+".trace.json"),"{\"package\":\"com.wulato.uikit.input-system\"}");
        }
    }
}
