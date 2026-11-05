import json,subprocess,unittest
from pathlib import Path
from argparse import Namespace
ROOT=Path(__file__).resolve().parents[2]
class IntegratedPackageTests(unittest.TestCase):
    def test_business_assemblies_ship_in_main_without_new_runtime_dependencies(self):
        root=ROOT/'Packages/com.wulato.uikit'
        definitions=list((root/'Runtime').rglob('*.asmdef'))
        self.assertEqual([root/'Runtime/Wulato.UIKit.asmdef'],definitions)
        definition=json.loads(definitions[0].read_text())
        self.assertEqual('Wulato.UIKit',definition['name'])
        self.assertEqual({'UniTask','UnityEngine.UI'},set(definition['references']))
        for name in ('Core','Loading','UGUI','Navigation','Presentation','Dialogs','PopupQueue'):
            self.assertTrue(list((root/'Runtime'/name).rglob('*.cs')))
        self.assertEqual({'com.unity.ugui':'1.0.0','com.cysharp.unitask':'2.5.11'},json.loads((root/'package.json').read_text())['dependencies'])
    def test_consumers_do_not_reference_removed_runtime_assemblies(self):
        legacy={'Wulato.UIKit.'+n for n in ('Core','Loading','UGUI','Navigation','Presentation','Dialogs','PopupQueue')}
        tracked=subprocess.check_output(['git','ls-files','--','Packages','Verification','Assets'],cwd=ROOT,text=True).splitlines()
        for name in tracked:
            path=ROOT/name
            if path.suffix!='.asmdef' or not path.exists():continue
            definition=json.loads(path.read_text(encoding='utf-8-sig'))
            self.assertFalse(legacy.intersection(definition.get('references',[])),str(path))
    def test_main_install_requires_business_suites_without_old_packages(self):
        from run import applicable_suites
        suites={'navigation':['nav'],'contracts':['core']}
        self.assertEqual(suites,applicable_suites(suites,{'dependencies':{'com.wulato.uikit':'file:main'}}))
    def test_old_package_scope_is_explicit_migration_error(self):
        from build import validate_build_scope
        errors=validate_build_scope(Namespace(phase='phase2',profile='clean',prepare_only=True,package_set='navigation'))
        self.assertTrue(errors);self.assertIn('migrat',' '.join(errors).lower())
