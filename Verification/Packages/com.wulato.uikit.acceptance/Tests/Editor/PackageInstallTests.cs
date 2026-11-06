using System.Linq;
using NUnit.Framework;
using UnityEditor.PackageManager;
using UnityEditor.Compilation;
using UnityEngine;
namespace Wulato.UIKit.Acceptance.Tests
{
    [Category("package_install")]
    public sealed class PackageInstallTests
    {
        [Test] public void LegacyAdapterPackages_AreRejectedWithMigrationMessage()
        {
            var type=System.Reflection.Assembly.Load("Wulato.UIKit.Editor").GetType("Wulato.UIKit.Editor.UICodegenPipeline",true);
            var method=type.GetMethod("ValidateAdapterPackages",System.Reflection.BindingFlags.Static|System.Reflection.BindingFlags.NonPublic);
            Assert.That(method,Is.Not.Null);
            foreach(var name in new[]{"com.wulato.uikit.addressables","com.wulato.uikit.input-system"})
            {
                var error=Assert.Throws<System.Reflection.TargetInvocationException>(()=>method.Invoke(null,new object[]{new[]{name}}));
                Assert.That(error.InnerException,Is.TypeOf<System.InvalidOperationException>());
                Assert.That(error.InnerException.Message,Does.Contain("Package migration: remove "+name));
            }
        }
        [Test] public void OptionalIntegrations_CompileOnlyWithInstalledSdks()
        {
            var packages=PackageInfo.GetAllRegisteredPackages();
            Assert.That(packages.Any(p=>p.name=="com.wulato.uikit.addressables"||p.name=="com.wulato.uikit.input-system"),Is.False,"Remove retired UIKit adapter packages; keep Unity SDK packages.");
            var runtime=CompilationPipeline.GetAssemblies(AssembliesType.Player).Select(a=>a.name).ToArray();
            var editor=CompilationPipeline.GetAssemblies(AssembliesType.Editor).Select(a=>a.name).ToArray();
            var addressables=packages.Any(p=>p.name=="com.unity.addressables");
            bool tween=UnityEditor.PlayerSettings.GetScriptingDefineSymbolsForGroup(UnityEditor.BuildTargetGroup.Standalone).Split(';').Contains("WULATO_UIKIT_DOTWEEN");
            Assert.That(runtime.Contains("Wulato.UIKit.DOTween"),Is.EqualTo(tween));
            var input=packages.Any(p=>p.name=="com.unity.inputsystem");
            Assert.That(runtime.Contains("Wulato.UIKit.Addressables"),Is.EqualTo(addressables));
            Assert.That(editor.Contains("Wulato.UIKit.Addressables.Editor"),Is.EqualTo(addressables));
            Assert.That(runtime.Contains("Wulato.UIKit.InputSystem"),Is.EqualTo(input));
            var folder=System.Environment.GetEnvironmentVariable("UIKIT_EVIDENCE_DIR");
            if(!string.IsNullOrEmpty(folder))System.IO.File.WriteAllText(System.IO.Path.Combine(folder,"optional-integrations.trace.json"),
                "{\"addressables\":"+addressables.ToString().ToLowerInvariant()+",\"inputSystem\":"+input.ToString().ToLowerInvariant()+",\"dotween\":"+tween.ToString().ToLowerInvariant()+",\"runtimeAssemblies\":["+string.Join(",",runtime.Select(n=>"\""+n+"\""))+"]}");
        }
        [Test] public void ResolvedDependencies_MatchPinnedVersions()
        {
            var packages=PackageInfo.GetAllRegisteredPackages();
            Assert.That(packages.Single(p=>p.name=="com.cysharp.unitask").version,Is.EqualTo("2.5.11"));
            Assert.That(packages.Single(p=>p.name=="com.unity.ugui").version,Is.EqualTo(System.Environment.GetEnvironmentVariable("UIKIT_EXPECTED_UGUI")));
            Assert.That(packages.Single(p=>p.name=="com.wulato.uikit").version,Is.EqualTo("0.4.0"));
        }
        [Test] public void CoreAssembly_DoesNotReferenceOptionalOrEditorAssemblies()
        {
            Assert.That(typeof(IUIContext).Assembly.GetName().Name,Is.EqualTo("Wulato.UIKit"));
            var names=typeof(IUIContext).Assembly.GetReferencedAssemblies().Select(a=>a.Name).ToArray();
            Assert.That(names,Does.Not.Contain("DOTween"));Assert.That(names,Does.Not.Contain("DOTween.Modules"));Assert.That(names,Does.Not.Contain("Wulato.UIKit.DOTween"));
            Assert.That(names,Does.Not.Contain("Unity.InputSystem"));Assert.That(names,Does.Not.Contain("Unity.TextMeshPro"));
            Assert.That(names.Any(n=>n.StartsWith("UnityEditor")||n.Contains("Verification")||n.Contains("Acceptance")||n.Contains("Test")),Is.False);
        }
    }
}
