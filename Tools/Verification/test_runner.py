import plistlib,tempfile,unittest
from pathlib import Path
from run import verify_editor
class EditorGuardTests(unittest.TestCase):
    def fixture(self,root,version):
        p=Path(root)/'Unity.app/Contents/MacOS/Unity';p.parent.mkdir(parents=True);p.write_text('fixture')
        (p.parent.parent/'Info.plist').write_bytes(plistlib.dumps({'CFBundleVersion':version}));return p
    def test_wrong_override_rejected_before_host_is_opened(self):
        with tempfile.TemporaryDirectory() as d:
            p=self.fixture(d,'6000.3.22f1')
            with self.assertRaises(ValueError):verify_editor(p,'2022.3.62f2')
    def test_exact_editor_allowed(self):
        with tempfile.TemporaryDirectory() as d:
            p=self.fixture(d,'2022.3.62f2');self.assertEqual('2022.3.62f2',verify_editor(p,'2022.3.62f2'))

class ExtensionFingerprintTests(unittest.TestCase):
    def test_canonical_extension_source_is_fingerprinted_and_alternate_is_rejected(self):
        import json
        from unittest.mock import patch
        import run
        with tempfile.TemporaryDirectory() as folder:
            root=Path(folder);host=root/'Verification/Projects/u2022-legacy';package=root/'Packages/com.wulato.uikit.addressables'
            package.mkdir(parents=True);(host/'Packages').mkdir(parents=True);(root/'Tools').mkdir()
            (root/'Tools/matrix.json').write_text('{}');(package/'package.json').write_text('{"name":"com.wulato.uikit.addressables"}')
            source=package/'Marker.cs';source.write_text('before')
            manifest=host/'Packages/manifest.json';manifest.write_text(json.dumps({'dependencies':{'com.wulato.uikit.addressables':'file:../../../../Packages/com.wulato.uikit.addressables'}}))
            with patch.object(run,'R',root),patch.object(run,'C',root/'Tools'):
                before=run.fingerprint(host);source.write_text('after');self.assertNotEqual(before,run.fingerprint(host))
                manifest.write_text(json.dumps({'dependencies':{'com.wulato.uikit.addressables':'file:../../../../Packages/alternate'}}))
                with self.assertRaisesRegex(ValueError,'noncanonical-package-source'):run.package_sources(host)

class ConfiguredTargetTests(unittest.TestCase):
    def test_all_selects_configured_targets_not_all_known_versions(self):
        import run,argparse
        self.assertEqual(['2022.3.62f2'],[v for v,b,p in run.selected(argparse.Namespace(editor='all',backend='legacy'))])
    def test_missing_target_configuration_rejected(self):
        import run,argparse
        with self.assertRaisesRegex(ValueError,'editor-target'):list(run.selected(argparse.Namespace(editor='2099.0.0f1',backend='legacy')))

class UnityTimingTests(unittest.TestCase):
    def test_reports_compile_samples_without_claiming_test_only_duration(self):
        import run
        report=run.timing_report(2.5,'compile time=120 ms\ncompile time=30 ms', 'tests')
        self.assertEqual(2.5,report['editor_wall_seconds'])
        self.assertEqual([120.,30.],report['reported_compile_ms'])
        self.assertIn('includes',report['scope'])
