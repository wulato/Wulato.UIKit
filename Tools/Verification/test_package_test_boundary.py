"""Testable UPM packages must compile without repository-only prototype packages."""
import json,unittest
from pathlib import Path

ROOT=Path(__file__).resolve().parents[2]
class PackageTestBoundaryTests(unittest.TestCase):
    def test_main_and_adapter_assembly_graph_resolves_without_verification_packages(self):
        folders=[ROOT/'Packages/com.wulato.uikit',ROOT/'Packages/com.wulato.uikit.input-system']
        definitions=[(p,json.loads(p.read_text())) for base in folders for p in base.rglob('*.asmdef')]
        supplied={data['name'] for _,data in definitions}|{'UniTask','UnityEngine.UI','Unity.InputSystem','Unity.InputSystem.TestFramework'}
        missing=[str(p.relative_to(ROOT))+': '+ref for p,data in definitions for ref in data.get('references',[]) if ref not in supplied]
        self.assertEqual([],missing,'Consumer lacks these assemblies: '+', '.join(missing))
    def test_package_test_sources_do_not_import_repository_prototypes(self):
        offenders=[]
        for name in ('com.wulato.uikit','com.wulato.uikit.input-system'):
            for p in (ROOT/'Packages'/name/'Tests').rglob('*.cs'):
                if 'Wulato.UIKit.Verification' in p.read_text():offenders.append(str(p.relative_to(ROOT)))
        self.assertEqual([],offenders)
