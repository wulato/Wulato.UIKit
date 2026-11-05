import json,unittest
from pathlib import Path
from argparse import Namespace
ROOT=Path(__file__).resolve().parents[2]
class IntegratedPackageTests(unittest.TestCase):
    def test_business_assemblies_ship_in_main_without_new_runtime_dependencies(self):
        root=ROOT/'Packages/com.wulato.uikit'
        for name in ('Navigation','Presentation','Dialogs','PopupQueue'):
            self.assertTrue((root/'Runtime'/name/('Wulato.UIKit.'+name+'.asmdef')).is_file())
        self.assertEqual({'com.unity.ugui':'1.0.0','com.cysharp.unitask':'2.5.11'},json.loads((root/'package.json').read_text())['dependencies'])
    def test_main_install_requires_business_suites_without_old_packages(self):
        from run import applicable_suites
        suites={'navigation':['nav'],'contracts':['core']}
        self.assertEqual(suites,applicable_suites(suites,{'dependencies':{'com.wulato.uikit':'file:main'}}))
    def test_old_package_scope_is_explicit_migration_error(self):
        from build import validate_build_scope
        errors=validate_build_scope(Namespace(phase='phase2',profile='clean',prepare_only=True,package_set='navigation'))
        self.assertTrue(errors);self.assertIn('migrat',' '.join(errors).lower())
