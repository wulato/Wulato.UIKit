import unittest
from runtime import runtime_manifest

class AddressablesHostTests(unittest.TestCase):
    def test_preflight_manifest_locks_dependency_and_testable(self):
        m=runtime_manifest('2022.3.62f2','legacy')
        self.assertEqual('1.21.21',m['dependencies'].get('com.unity.addressables'))
        self.assertIn('com.wulato.uikit.verification.addressables',m['testables'])
        newer=runtime_manifest('6000.3.22f1','legacy')
        self.assertEqual('2.9.1',newer['dependencies'].get('com.unity.addressables'))
        self.assertEqual('2.6.1',newer['dependencies'].get('com.unity.scriptablebuildpipeline'))

class AddressablesEvidenceTests(unittest.TestCase):
    def fixture(self,root):
        import json
        host=root/'host';out=root/'out';out.mkdir();(host/'Assets').mkdir(parents=True);(host/'Packages').mkdir()
        (host/'Packages/manifest.json').write_text(json.dumps({'dependencies':{}}))
        bundle=host/'Library/com.unity.addressables/aa/OSX/probe.bundle';bundle.parent.mkdir(parents=True);bundle.write_bytes(b'real bundle fixture')
        (out/'addressables-content.json').write_text(json.dumps(dict(editor='2022.3.62f2',version='1.21.21',sbpVersion='1.21.25',target='StandaloneOSX',builderType='UnityEditor.AddressableAssets.Build.DataBuilders.BuildScriptPackedPlayMode')))
        return host,out,bundle
    def test_content_rejects_changed_source_bundle_and_identity(self):
        from addressables_content import seal_content,verify_content
        import tempfile,json
        from pathlib import Path
        with tempfile.TemporaryDirectory() as folder:
            host,out,bundle=self.fixture(Path(folder));seal_content(host,out,'2022.3.62f2')
            self.assertEqual([],verify_content(host,'2022.3.62f2'))
            (host/'Assets/new-source.txt').write_text('new');self.assertIn('addressables-stale-content-source',verify_content(host,'2022.3.62f2'))
            (host/'Assets/new-source.txt').unlink();bundle.write_bytes(b'changed');self.assertTrue(verify_content(host,'2022.3.62f2'))
            self.assertIn('addressables-content-identity',verify_content(host,'6000.3.22f1'))
    def test_fast_mode_missing_bundle_and_path_escape_are_rejected(self):
        from addressables_content import seal_content,verify_content
        import tempfile,json
        from pathlib import Path
        with tempfile.TemporaryDirectory() as folder:
            host,out,bundle=self.fixture(Path(folder));report=json.loads((out/'addressables-content.json').read_text());report['builderType']='FastMode'
            (out/'addressables-content.json').write_text(json.dumps(report))
            with self.assertRaises(ValueError):seal_content(host,out,'2022.3.62f2')
            (host/'AddressablesContent.json').write_text(json.dumps(dict(schema=1,files={'../escape.bundle':'hash'})))
            self.assertTrue(verify_content(host,'2022.3.62f2'))

class AddressablesSelectionTests(unittest.TestCase):
    def test_addressables_change_selects_its_suites(self):
        from ci_suites import select_suites
        selected=select_suites(['Packages/com.wulato.uikit.addressables/Runtime/AddressablesUIViewLoader.cs'])
        self.assertIn(('EditMode','addressables','all'),selected)
        self.assertIn(('PlayMode','addressables-runtime','all'),selected)
        self.assertIn(('PlayMode','addressables-integration','all'),selected)
    def test_verification_fixture_change_selects_real_content_and_runtime_suites(self):
        from ci_suites import select_suites
        selected=select_suites(['Verification/Packages/com.wulato.uikit.verification.addressables/PlayMode/BundleIntegrationTests.cs'])
        for mode,suite in [('EditMode','addressables'),('PlayMode','addressables-runtime'),('PlayMode','addressables-integration')]:
            self.assertIn((mode,suite,'all'),selected)

    def test_external_codegen_and_content_verifier_changes_select_addressables_regressions(self):
        from ci_suites import select_suites
        for path in ['Packages/com.wulato.uikit/Editor/Registry/UIRegistryProviderGenerator.cs',
                     'Packages/com.wulato.uikit/Editor/Registry/UIRegistryGenerator.cs',
                     'Packages/com.wulato.uikit/Runtime/UGUI/Binding/UIBindingMarker.cs',
                     'Packages/com.wulato.uikit/Runtime/UGUI/UIContextBuilder.cs',
                     'Tools/Verification/addressables_content.py']:
            with self.subTest(path=path):
                selected=select_suites([path])
                self.assertIn(('EditMode','addressables','all'),selected)
                self.assertIn(('PlayMode','addressables-integration','all'),selected)

    def test_optional_runtime_suite_is_absent_without_package(self):
        from run import applicable_suites
        self.assertEqual({},applicable_suites({'addressables-runtime':['case']},{'dependencies':{}}))

class PreparedSampleRetentionTests(unittest.TestCase):
    def test_content_preparation_keeps_previously_installed_sample_acceptance(self):
        from runtime import prepare_runtime_manifest
        manifest=runtime_manifest('2022.3.62f2','legacy')
        name='com.wulato.uikit.acceptance.samples'
        manifest['dependencies'][name]='file:../../../Packages/'+name
        manifest['testables'].append(name)
        prepared=prepare_runtime_manifest('2022.3.62f2','legacy',manifest,{'dependencies':{}})
        self.assertEqual('file:../../../Packages/'+name,prepared['dependencies'].get(name))
        self.assertIn(name,prepared['testables'])
