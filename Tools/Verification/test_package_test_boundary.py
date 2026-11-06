"""Testable UPM packages must compile without repository-only prototype packages."""
import json,unittest
from pathlib import Path

ROOT=Path(__file__).resolve().parents[2]
class PackageTestBoundaryTests(unittest.TestCase):
    def test_main_and_adapter_assembly_graph_resolves_without_verification_packages(self):
        folders=[ROOT/'Packages/com.wulato.uikit']
        definitions=[(p,json.loads(p.read_text())) for base in folders for p in base.rglob('*.asmdef')]
        supplied={data['name'] for _,data in definitions}|{'UniTask','UnityEngine.UI','Unity.InputSystem','Unity.InputSystem.TestFramework','Unity.Addressables','Unity.Addressables.Editor','Unity.ResourceManager','R3.Unity','FancyScrollView','Unity.Localization'}
        # External R3.Unity is allowed only behind the explicit sample opt-in.
        for path,data in definitions:
            if 'R3.Unity' in data.get('references',[]):
                self.assertIn('Samples~/R3/',str(path))
                self.assertIn('WULATO_UIKIT_R3',data.get('defineConstraints',[]))
            if 'Unity.Localization' in data.get('references',[]):
                self.assertIn('Samples~/Localization/',str(path))
                self.assertTrue({'WULATO_UIKIT_LOCALIZATION','WULATO_LOCALIZATION_AVAILABLE'}<=set(data.get('defineConstraints',[])))
                self.assertIn(dict(name='com.unity.localization',expression='[1.4.5,2.0.0)',define='WULATO_LOCALIZATION_AVAILABLE'),data.get('versionDefines',[]))
            if 'FancyScrollView' in data.get('references',[]):
                self.assertIn('Samples~/FancyScrollView/',str(path))
                self.assertIn('WULATO_UIKIT_FANCY_SCROLL',data.get('defineConstraints',[]))
        missing=[str(p.relative_to(ROOT))+': '+ref for p,data in definitions for ref in data.get('references',[]) if ref not in supplied]
        self.assertEqual([],missing,'Consumer lacks these assemblies: '+', '.join(missing))
    def test_package_test_sources_do_not_import_repository_prototypes(self):
        offenders=[]
        for name in ('com.wulato.uikit',):
            for p in (ROOT/'Packages'/name/'Tests').rglob('*.cs'):
                if 'Wulato.UIKit.Verification' in p.read_text():offenders.append(str(p.relative_to(ROOT)))
        self.assertEqual([],offenders)
