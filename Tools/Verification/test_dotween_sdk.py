import hashlib,json,tempfile,unittest,zipfile
from pathlib import Path
from unittest.mock import patch
import dotween_sdk
class SdkStagingTests(unittest.TestCase):
    def test_outside_ignored_hosts_is_rejected(self):
        with self.assertRaises(ValueError):dotween_sdk.stage('/missing.zip',Path('/private/tmp/consumer'))
    def test_stage_preserves_other_standalone_fields_and_explicitly_isolates_sdk(self):
        with tempfile.TemporaryDirectory() as folder:
            root=Path(folder);host=root/'Artifacts/uikit/ci-selected/hosts/task/u2022-legacy';settings=host/'ProjectSettings/ProjectSettings.asset';settings.parent.mkdir(parents=True)
            settings.write_text('  other:\n    Standalone: 0\n  scriptingDefineSymbols:\n    Standalone: EXISTING\n  tail: 1\n')
            archive=root/'sdk.zip'
            with zipfile.ZipFile(archive,'w') as sdk:
                sdk.writestr('DOTween/DOTween.dll',b'unit-test-fixture-only')
                for n in ['Utils','Physics','Physics2D']:sdk.writestr('DOTween/Modules/DOTweenModule'+n+'.cs',b'// staging fixture')
            with patch.object(dotween_sdk,'ROOT',root),patch.object(dotween_sdk,'ARCHIVE_SHA256',hashlib.sha256(archive.read_bytes()).hexdigest()):
                receipt=dotween_sdk.stage(archive,host);self.assertTrue(receipt['enabled']);self.assertTrue(dotween_sdk.integration_enabled(host))
                self.assertIn('    Standalone: 0\n',settings.read_text());self.assertIn('EXISTING;WULATO_UIKIT_DOTWEEN',settings.read_text())
                self.assertIn('isExplicitlyReferenced: 1',(host/'Assets/UIKitDOTweenSDK/DOTween.dll.meta').read_text())
                self.assertEqual(3,len(receipt['modules']));dotween_sdk.stage(archive,host,False)
                self.assertFalse(dotween_sdk.integration_enabled(host));self.assertNotIn('WULATO_UIKIT_DOTWEEN',settings.read_text());self.assertIn('EXISTING',settings.read_text())
                (host/'Assets/UIKitDOTweenSDK/DOTween.dll').write_bytes(b'changed')
                with self.assertRaises(ValueError):dotween_sdk.stage(archive,host)
    def test_unpinned_archive_is_rejected_before_staging(self):
        with tempfile.TemporaryDirectory() as folder:
            root=Path(folder);host=root/'Artifacts/uikit/ci-selected/hosts/task/host';settings=host/'ProjectSettings/ProjectSettings.asset';settings.parent.mkdir(parents=True);settings.write_text('  scriptingDefineSymbols:\n    Standalone: EXISTING\n')
            archive=root/'other.zip';archive.write_bytes(b'not-authorized-sdk')
            with patch.object(dotween_sdk,'ROOT',root),self.assertRaises(ValueError):dotween_sdk.stage(archive,host)
            self.assertFalse((host/'Assets').exists())

class OptionalSuiteTests(unittest.TestCase):
    def test_default_suite_scope_does_not_require_optional_dotween(self):
        from run import applicable_suites
        suites={'contracts':['core'],'dotween':['sdk']};manifest={'dependencies':{'com.wulato.uikit':'file:main'}}
        self.assertEqual({'contracts':['core']},applicable_suites(suites,manifest))
        self.assertEqual(suites,applicable_suites(suites,manifest,dotween=True))
    def test_adapter_tests_and_sample_require_explicit_dll_and_define(self):
        root=dotween_sdk.ROOT/'Packages/com.wulato.uikit'
        for name in ['Runtime/Integrations/DOTween','Tests/Integrations/DOTween','Samples~/DOTween']:
            paths=list((root/name).glob('*.asmdef'));self.assertEqual(1,len(paths))
            d=json.loads(paths[0].read_text());self.assertTrue(d['overrideReferences']);self.assertEqual(['DOTween.dll'],d['precompiledReferences']);self.assertEqual(['WULATO_UIKIT_DOTWEEN'],d['defineConstraints'])

    def test_dotween_selection_is_explicit_without_auto_installation(self):
        from ci_suites import select_suites
        self.assertEqual({('PlayMode','dotween','legacy'),('EditMode','package-install','legacy')},set(select_suites(['Packages/com.wulato.uikit/Tests/Integrations/DOTween/DOTweenTransitionTests.cs'])))
