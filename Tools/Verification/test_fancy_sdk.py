import hashlib,io,json,tarfile,tempfile,unittest
from pathlib import Path
from unittest.mock import patch
import fancy_sdk
class FancySdkTests(unittest.TestCase):
    def test_staging_refuses_other_hosts(self):
        with self.assertRaises(ValueError):fancy_sdk.stage('/missing',Path('/private/tmp/game'))
    def test_pin_and_existing_sdk_protection_and_define_scope(self):
        with tempfile.TemporaryDirectory() as folder:
            root=Path(folder);host=root/'Artifacts/uikit/ci-selected/hosts/task/host';p=host/'ProjectSettings/ProjectSettings.asset';p.parent.mkdir(parents=True);p.write_text('  other:\n    Standalone: 0\n  scriptingDefineSymbols:\n    Standalone: EXISTING\n')
            sample=root/'Packages/com.wulato.uikit/Samples~/FancyScrollView';sample.mkdir(parents=True);(sample/'Marker.cs').write_text('// test fixture')
            archive=root/'sdk.tar.gz'
            with tarfile.open(archive,'w:gz') as t:
                value=b'// unit fixture only';entry=tarfile.TarInfo('FancyScrollView-1.9.0/Sources/Runtime/Marker.cs');entry.size=len(value);t.addfile(entry,io.BytesIO(value))
            with patch.object(fancy_sdk,'ROOT',root):
                with self.assertRaisesRegex(ValueError,'archive-hash'):fancy_sdk.stage(archive,host)
                self.assertFalse((host/'Assets').exists())
                with patch.object(fancy_sdk,'ARCHIVE_SHA',hashlib.sha256(archive.read_bytes()).hexdigest()):
                    receipt=fancy_sdk.stage(archive,host);self.assertEqual(1,len(receipt['files']));self.assertTrue(fancy_sdk.integration_enabled(host));self.assertIn('    Standalone: 0\n',p.read_text())
                    fancy_sdk.stage(archive,host,False);self.assertFalse(fancy_sdk.integration_enabled(host));self.assertIn('EXISTING',p.read_text())
                    (host/'Assets/UIKitFancySDK/Marker.cs').write_text('// changed')
                    with self.assertRaisesRegex(ValueError,'refuse-overwriting'):fancy_sdk.stage(archive,host)
    def test_optional_suites_and_selector(self):
        from run import applicable_suites
        from ci_suites import plan
        suites={'fancy-list':['sdk'],'contracts':['base']};manifest={'dependencies':{'com.wulato.uikit':'file:main'}}
        self.assertEqual({'contracts':['base']},applicable_suites(suites,manifest));self.assertEqual(suites,applicable_suites(suites,manifest,fancy=True))
        selected=plan(['Packages/com.wulato.uikit/Samples~/FancyScrollView/CatalogView.cs']);self.assertEqual({'fancy-list','package-install'},{r['suite'] for r in selected['tests']})
    def test_sample_tests_are_opt_in_and_no_core_sdk_assembly(self):
        root=fancy_sdk.ROOT/'Packages/com.wulato.uikit'
        for folder in ['Samples~/FancyScrollView','Samples~/FancyScrollView/Tests']:
            d=json.loads(next((root/folder).glob('*.asmdef')).read_text());self.assertIn('WULATO_UIKIT_FANCY_SCROLL',d['defineConstraints']);self.assertIn('FancyScrollView',d['references']);self.assertNotIn('Wulato.UIKit.Tests.Support',d['references'])
        self.assertEqual(4,len(list((root/'Runtime').rglob('*.asmdef'))))
        self.assertNotIn('jp.setchi.fancyscrollview',json.loads((root/'package.json').read_text())['dependencies'])
