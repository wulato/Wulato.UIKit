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
            root=Path(folder);host=root/'Verification/Projects/u2022-legacy';package=root/'Packages/com.wulato.uikit.navigation'
            package.mkdir(parents=True);(host/'Packages').mkdir(parents=True);(root/'Tools').mkdir()
            (root/'Tools/matrix.json').write_text('{}');(package/'package.json').write_text('{"name":"com.wulato.uikit.navigation"}')
            source=package/'Marker.cs';source.write_text('before')
            manifest=host/'Packages/manifest.json';manifest.write_text(json.dumps({'dependencies':{'com.wulato.uikit.navigation':'file:../../../../Packages/com.wulato.uikit.navigation'}}))
            with patch.object(run,'R',root),patch.object(run,'C',root/'Tools'):
                before=run.fingerprint(host);source.write_text('after');self.assertNotEqual(before,run.fingerprint(host))
                manifest.write_text(json.dumps({'dependencies':{'com.wulato.uikit.navigation':'file:../../../../Packages/alternate'}}))
                with self.assertRaisesRegex(ValueError,'noncanonical-package-source'):run.package_sources(host)
