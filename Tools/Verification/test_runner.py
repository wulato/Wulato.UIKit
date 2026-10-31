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
