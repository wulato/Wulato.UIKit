import copy, unittest
from runtime import validate_dependencies, runtime_manifest, UNITASK

class RuntimeDependencyTests(unittest.TestCase):
    def fixture(self, ver='6000.3.22f1'):
        manifest=runtime_manifest(ver,'legacy')
        lock={'dependencies':{name:{'version':value} for name,value in manifest['dependencies'].items()}}
        lock['dependencies']['com.cysharp.unitask']={'version':UNITASK,'source':'git'}
        return manifest,lock
    def test_exact_versions_accepted(self):
        for ver in ('2022.3.62f2','6000.3.22f1'):
            m,l=self.fixture(ver);self.assertEqual([],validate_dependencies(ver,m,l))
    def test_old_unitask_rejected_with_repair(self):
        m,l=self.fixture();l['dependencies']['com.cysharp.unitask']['version']='2.3.3'
        self.assertIn('UniTask 2.5.11', '\n'.join(validate_dependencies('6000.3.22f1',m,l)))
    def test_unity6_missing_explicit_ugui_rejected(self):
        m,l=self.fixture();del m['dependencies']['com.unity.ugui']
        self.assertTrue(validate_dependencies('6000.3.22f1',m,l))
    def test_unity6_wrong_lock_rejected(self):
        m,l=self.fixture();l['dependencies']['com.unity.ugui']['version']='1.0.0'
        self.assertTrue(validate_dependencies('6000.3.22f1',m,l))
    def test_runtime_keeps_preflight_and_never_installs_future_adapter(self):
        for backend in ('legacy','input'):
            m=runtime_manifest('2022.3.62f2',backend)
            self.assertIn('com.wulato.uikit',m['testables']);self.assertIn('com.wulato.uikit.acceptance',m['testables'])
            self.assertNotIn('com.wulato.uikit.input-system',m['dependencies'])
            self.assertEqual(backend=='input','com.unity.inputsystem' in m['dependencies'])

class RuntimePreparationTests(unittest.TestCase):
    def test_prepare_refuses_existing_old_unitask_without_rewriting(self):
        from runtime import prepare_runtime_manifest
        manifest=runtime_manifest('6000.3.22f1','legacy');manifest['dependencies']['com.cysharp.unitask']=UNITASK.replace('2.5.11','2.3.3')
        original=copy.deepcopy(manifest)
        with self.assertRaisesRegex(ValueError,'UniTask 2.5.11'):
            prepare_runtime_manifest('6000.3.22f1','legacy',manifest,{'dependencies':{}})
        self.assertEqual(original,manifest)
    def test_prepare_rejects_unity6_missing_ugui(self):
        from runtime import prepare_runtime_manifest
        manifest=runtime_manifest('6000.3.22f1','legacy');del manifest['dependencies']['com.unity.ugui']
        with self.assertRaisesRegex(ValueError,'ugui'):
            prepare_runtime_manifest('6000.3.22f1','legacy',manifest,{'dependencies':{}})
    def test_prepare_accepts_preflight_host_without_unitask(self):
        from runtime import prepare_runtime_manifest
        manifest={'dependencies':{'com.unity.ugui':'2.0.0'}}
        prepared=prepare_runtime_manifest('6000.3.22f1','legacy',manifest,{'dependencies':{'com.unity.ugui':{'version':'2.0.0'}}})
        self.assertEqual(UNITASK,prepared['dependencies']['com.cysharp.unitask'])
    def test_package_and_core_boundary_validation_rejects_optional_dependencies(self):
        from runtime import validate_main_package
        package={'name':'com.wulato.uikit','version':'0.1.0','unity':'2022.3','unityRelease':'62f2','dependencies':{'com.unity.ugui':'1.0.0','com.cysharp.unitask':'2.5.11'}}
        assemblies={'Core':['UniTask'],'Loading':['Wulato.UIKit.Core','UniTask'],'UGUI':['Wulato.UIKit.Core','Wulato.UIKit.Loading','UniTask','UnityEngine.UI']}
        self.assertEqual([],validate_main_package(package,assemblies))
        package['dependencies']['com.unity.inputsystem']='1.17.0'
        self.assertTrue(validate_main_package(package,assemblies))
        del package['dependencies']['com.unity.inputsystem'];assemblies['Core'].append('UnityEngine.UI')
        self.assertTrue(validate_main_package(package,assemblies))
