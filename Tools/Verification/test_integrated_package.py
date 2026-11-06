import json,subprocess,unittest
from pathlib import Path
from argparse import Namespace
ROOT=Path(__file__).resolve().parents[2]
class IntegratedPackageTests(unittest.TestCase):
    def test_business_assemblies_ship_in_main_without_new_runtime_dependencies(self):
        root=ROOT/'Packages/com.wulato.uikit'
        definitions=list((root/'Runtime').rglob('*.asmdef'))
        self.assertEqual({'Wulato.UIKit','Wulato.UIKit.Addressables','Wulato.UIKit.InputSystem','Wulato.UIKit.DOTween'},{json.loads(p.read_text())['name'] for p in definitions})
        definition=json.loads((root/'Runtime/Wulato.UIKit.asmdef').read_text())
        self.assertEqual('Wulato.UIKit',definition['name'])
        self.assertEqual({'UniTask','UnityEngine.UI'},set(definition['references']))
        for name in ('Core','Loading','UGUI','Navigation','Presentation','Dialogs','PopupQueue'):
            self.assertTrue(list((root/'Runtime'/name).rglob('*.cs')))
        self.assertEqual({'com.unity.ugui':'1.0.0','com.cysharp.unitask':'2.5.11'},json.loads((root/'package.json').read_text())['dependencies'])
    def test_optional_integrations_are_gated_in_runtime_editor_tests_and_samples(self):
        root=ROOT/'Packages/com.wulato.uikit'
        self.assertFalse((ROOT/'Packages/com.wulato.uikit.addressables').exists())
        self.assertFalse((ROOT/'Packages/com.wulato.uikit.input-system').exists())
        for sdk,folder in [('com.unity.addressables','Addressables'),('com.unity.inputsystem','InputSystem')]:
            paths=list((root/'Runtime/Integrations'/folder).rglob('*.asmdef'))+list((root/'Tests/Integrations'/folder).rglob('*.asmdef'))
            if folder=='Addressables':paths+=list((root/'Editor/Integrations'/folder).rglob('*.asmdef'))+list((root/'Samples~/LocalInventory').rglob('*.asmdef'))
            self.assertGreaterEqual(len(paths),3)
            for path in paths:
                d=json.loads(path.read_text());defines=[x['define'] for x in d.get('versionDefines',[]) if x['name']==sdk]
                self.assertEqual(1,len(defines),str(path));self.assertIn(defines[0],d.get('defineConstraints',[]),str(path))
    def test_four_dependency_combinations_use_one_uikit_package(self):
        from runtime import runtime_manifest
        for backend in ('legacy','input'):
            for scope in ('main','addressables'):
                m=runtime_manifest('2022.3.62f2',backend,scope);d=m['dependencies']
                self.assertFalse({'com.wulato.uikit.addressables','com.wulato.uikit.input-system'}&set(d))
                self.assertEqual(backend=='input','com.unity.inputsystem' in d)
                self.assertEqual(scope=='addressables','com.unity.addressables' in d)
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
