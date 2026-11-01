using System.Linq;
using NUnit.Framework;
using UnityEditor.PackageManager;
using UnityEngine;
namespace Wulato.UIKit.Acceptance.Tests
{
    [Category("package_install")]
    public sealed class PackageInstallTests
    {
        [Test] public void ResolvedDependencies_MatchPinnedVersions()
        {
            var packages=PackageInfo.GetAllRegisteredPackages();
            Assert.That(packages.Single(p=>p.name=="com.cysharp.unitask").version,Is.EqualTo("2.5.11"));
            Assert.That(packages.Single(p=>p.name=="com.unity.ugui").version,Is.EqualTo(Application.unityVersion.StartsWith("6000.")?"2.0.0":"1.0.0"));
            Assert.That(packages.Single(p=>p.name=="com.wulato.uikit").version,Is.EqualTo("0.1.0"));
        }
        [Test] public void CoreAssembly_DoesNotReferenceOptionalOrEditorAssemblies()
        {
            var names=typeof(IUIContext).Assembly.GetReferencedAssemblies().Select(a=>a.Name).ToArray();
            Assert.That(names,Does.Not.Contain("Unity.InputSystem"));Assert.That(names,Does.Not.Contain("Unity.TextMeshPro"));
            Assert.That(names.Any(n=>n.StartsWith("UnityEditor")||n.Contains("Verification")||n.Contains("Acceptance")||n.Contains("Test")),Is.False);
        }
    }
}
