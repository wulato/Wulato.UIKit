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
    def test_runtime_keeps_preflight_and_installs_adapter_only_for_input(self):
        for backend in ('legacy','input'):
            m=runtime_manifest('2022.3.62f2',backend)
            self.assertIn('com.wulato.uikit',m['testables']);self.assertIn('com.wulato.uikit.acceptance',m['testables'])
            self.assertNotIn('com.wulato.uikit.input-system',m['dependencies'])
            self.assertNotIn('com.wulato.uikit.input-system',m['testables'])
            self.assertEqual(backend=='input','com.unity.inputsystem' in m['dependencies'])

class RuntimePreparationTests(unittest.TestCase):
    def test_legacy_adapter_dependencies_and_lock_are_rejected_without_mutation(self):
        from runtime import prepare_runtime_manifest
        for name in ('com.wulato.uikit.addressables','com.wulato.uikit.input-system'):
            for in_lock in (False,True):
                manifest=runtime_manifest('2022.3.62f2','legacy','main');lock={'dependencies':{}}
                (lock if in_lock else manifest)['dependencies'][name]='old'
                before=copy.deepcopy((manifest,lock))
                with self.assertRaisesRegex(ValueError,'Package migration: remove '+name):prepare_runtime_manifest('2022.3.62f2','legacy',manifest,lock,'main')
                self.assertEqual(before,(manifest,lock))
    def test_scoped_main_runtime_excludes_optional_sdk_and_probe(self):
        for backend in ('legacy','input'):
            m=runtime_manifest('2022.3.62f2',backend,'main')
            self.assertNotIn('com.wulato.uikit.addressables',m['dependencies'])
            self.assertNotIn('com.wulato.uikit.verification.addressables',m['dependencies'])
            self.assertNotIn('com.unity.addressables',m['dependencies'])
            self.assertNotIn('com.wulato.uikit.input-system',m['dependencies'])
            self.assertIn('com.wulato.uikit.acceptance.flows',m['dependencies'])
    def test_explicit_addressables_runtime_retains_profile_constraints(self):
        m=runtime_manifest('2022.3.62f2','legacy','addressables')
        self.assertEqual('1.21.21',m['dependencies']['com.unity.addressables'])
        self.assertEqual('1.21.25',m['dependencies']['com.unity.scriptablebuildpipeline'])
        self.assertIn('com.wulato.uikit',m['testables']);self.assertNotIn('com.wulato.uikit.addressables',m['testables'])
    def test_scoped_preparation_keeps_existing_dependency_rejection(self):
        from runtime import prepare_runtime_manifest
        original=runtime_manifest('2022.3.62f2','legacy');original['dependencies']['com.cysharp.unitask']='incompatible'
        before=copy.deepcopy(original)
        with self.assertRaisesRegex(ValueError,'UniTask'):
            prepare_runtime_manifest('2022.3.62f2','legacy',original,{'dependencies':{}},'main')
        self.assertEqual(before,original)
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
        package={'name':'com.wulato.uikit','version':'0.4.0','unity':'2022.3','unityRelease':'62f2','dependencies':{'com.unity.ugui':'1.0.0','com.cysharp.unitask':'2.5.11'}}
        assemblies={'Wulato.UIKit':['UniTask','UnityEngine.UI'],'Wulato.UIKit.Addressables':['Wulato.UIKit','UniTask','Unity.Addressables','Unity.ResourceManager'],'Wulato.UIKit.InputSystem':['Wulato.UIKit','UnityEngine.UI','Unity.InputSystem']}
        self.assertEqual([],validate_main_package(package,assemblies))
        package['dependencies']['com.unity.inputsystem']='1.17.0'
        self.assertTrue(validate_main_package(package,assemblies))
        del package['dependencies']['com.unity.inputsystem'];assemblies['Wulato.UIKit'].append('Unity.InputSystem')
        self.assertTrue(validate_main_package(package,assemblies))

class ExtensionBoundaryTests(unittest.TestCase):
    def test_old_core_metadata_is_rejected(self):
        from runtime import validate_main_package
        from pathlib import Path
        import json
        root=Path(__file__).resolve().parents[2]
        package=json.loads((root/'Packages/com.wulato.uikit/package.json').read_text())
        assemblies={'Wulato.UIKit':json.loads((root/'Packages/com.wulato.uikit/Runtime/Wulato.UIKit.asmdef').read_text())['references']}
        package['version']='0.1.0'
        self.assertTrue(validate_main_package(package,assemblies))
    def test_extension_identity_does_not_capture_arbitrary_packages(self):
        from runtime import is_extension_package
        self.assertFalse(is_extension_package('com.wulato.uikit.navigation'))
        self.assertFalse(is_extension_package('com.wulato.uikit.addressables'))
        self.assertFalse(is_extension_package('com.wulato.uikit.navigation.evil'))
    def test_absent_extension_suite_is_not_required(self):
        from run import applicable_suites
        matrix={'contracts':['core'],'navigation':['nav']}
        self.assertEqual({'contracts':['core']},applicable_suites(matrix,{'dependencies':{}}))
        self.assertEqual(matrix,applicable_suites(matrix,{'dependencies':{'com.wulato.uikit':'file:test'}}))

class ExtensionMetadataTests(unittest.TestCase):
    def test_extensions_require_new_core_and_no_optional_backends(self):
        from runtime import validate_extension_package
        package={'name':'com.wulato.uikit.addressables','version':'0.2.0','unity':'2022.3','unityRelease':'62f2','dependencies':{'com.wulato.uikit':'0.4.0','com.cysharp.unitask':'2.5.11','com.unity.addressables':'1.21.21'}}
        refs=['Wulato.UIKit','UniTask','Unity.Addressables','Unity.ResourceManager']
        self.assertIn('migration',' '.join(validate_extension_package(package,refs)).lower())
        package['dependencies']['com.wulato.uikit']='0.1.0'
        self.assertTrue(validate_extension_package(package,refs))
        package['dependencies']['com.wulato.uikit']='0.4.0'
        self.assertTrue(validate_extension_package(package,refs+['Unity.InputSystem']))

class IsolatedCIHostTests(unittest.TestCase):
    def test_runtime_preparation_resolves_canonical_sources_for_both_isolated_hosts(self):
        import argparse,json,tempfile,run
        from pathlib import Path
        from unittest.mock import patch
        # Exercise the existing runner orchestration, substituting only the actual Unity process.
        base=run.A/'ci-hardening';base.mkdir(parents=True,exist_ok=True)
        with tempfile.TemporaryDirectory(dir=base) as folder:
            artifacts=Path(folder)
            args=argparse.Namespace(command='prepare',phase='runtime',editor='2022.3.62f2',backend='all',editor_path=None,package_set='main',host_root=str(artifacts/'ci-selected/hosts'))
            def unity(args,ver,host,out,test=False):
                m=json.loads((host/'Packages/manifest.json').read_text())
                self.assertNotIn('com.unity.addressables',m['dependencies'])
                self.assertTrue(all(Path(value[5:]).is_absolute() for value in m['dependencies'].values() if value.startswith('file:')))
                sources=run.package_sources(host)
                self.assertEqual(run.R/'Packages/com.wulato.uikit',sources['com.wulato.uikit'])
                run.dump(host/'Packages/packages-lock.json',dict(dependencies={name:dict(version=value,source='local' if value.startswith('file:') else 'git' if name=='com.cysharp.unitask' else 'registry') for name,value in m['dependencies'].items()}))
                run.dump(out/'dependency-snapshot.json',dict(editor=ver,packages=[dict(name=name,version=version) for name,version in [('com.wulato.uikit','0.4.0'),('com.cysharp.unitask','2.5.11'),('com.unity.ugui','1.0.0')]]))
                log='Unity Editor version: '+ver+'\n';(out/'editor.log').write_text(log)
                return 0,False,log,ver
            with patch.object(run,'A',artifacts),patch.object(run,'verify_editor',return_value='2022.3.62f2'),patch.object(run,'unity',side_effect=unity) as process:
                self.assertEqual(0,run.run(args));self.assertEqual(2,process.call_count)
    def test_isolated_host_copies_settings_without_existing_assets_or_dependencies(self):
        import argparse,json,tempfile,run
        from pathlib import Path
        from unittest.mock import patch
        with tempfile.TemporaryDirectory() as folder:
            root=Path(folder);original=root/'Verification/Projects/u2022-legacy'
            (original/'Assets').mkdir(parents=True);(original/'Assets/User.prefab').write_text('preserve')
            (original/'ProjectSettings').mkdir();(original/'ProjectSettings/ProjectVersion.txt').write_text('m_EditorVersion: 2022.3.62f2\n')
            (original/'Packages').mkdir();(original/'Packages/manifest.json').write_text('{"dependencies":{"old":"local"}}')
            args=argparse.Namespace(command='prepare',phase='runtime',editor='2022.3.62f2',backend='legacy',host_root=str(root/'Artifacts/uikit/ci-selected/hosts'))
            with patch.object(run,'A',root/'Artifacts/uikit'),patch.object(run,'V',root/'Verification'):
                ver,backend,host=next(run.selected(args))
                self.assertEqual(original.name,host.name);self.assertEqual([],list((host/'Assets').iterdir()))
                self.assertEqual((original/'ProjectSettings/ProjectVersion.txt').read_bytes(),(host/'ProjectSettings/ProjectVersion.txt').read_bytes())
                self.assertEqual({'com.unity.ugui':'1.0.0'},json.loads((host/'Packages/manifest.json').read_text())['dependencies'])
                args.command='tests';self.assertEqual(host,next(run.selected(args))[2])
                args.host_root=str(root/'outside')
                with self.assertRaisesRegex(ValueError,'CI host root'):next(run.selected(args))
            self.assertEqual('preserve',(original/'Assets/User.prefab').read_text())
    def test_missing_isolated_host_cannot_be_created_by_test_execution(self):
        import argparse,tempfile,run
        from pathlib import Path
        from unittest.mock import patch
        with tempfile.TemporaryDirectory() as folder:
            root=Path(folder)
            with patch.object(run,'A',root/'Artifacts/uikit'),self.assertRaisesRegex(ValueError,'requires runtime preparation'):
                run.isolated_host(argparse.Namespace(command='tests'),'2022.3.62f2','legacy',root/'u2022-legacy',root/'Artifacts/uikit/ci-selected/hosts')
