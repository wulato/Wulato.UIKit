import hashlib,io,json,tarfile,tempfile,unittest,zipfile
from pathlib import Path
from unittest.mock import patch
import r3_sdk

class R3StagingTests(unittest.TestCase):
    def test_staging_is_restricted_to_ignored_hosts(self):
        with self.assertRaises(ValueError):r3_sdk.stage('/missing',Path('/private/tmp/consumer'))

    def test_pins_are_checked_before_writes(self):
        with tempfile.TemporaryDirectory() as folder:
            root=Path(folder);host=root/'Artifacts/uikit/ci-selected/hosts/task/host';p=host/'ProjectSettings/ProjectSettings.asset';p.parent.mkdir(parents=True);p.write_text('  scriptingDefineSymbols:\n    Standalone: EXISTING\n')
            (root/r3_sdk.SOURCE).write_bytes(b'unapproved')
            with patch.object(r3_sdk,'ROOT',root),self.assertRaisesRegex(ValueError,'source-hash'):r3_sdk.stage(root,host)
            self.assertFalse((host/'Assets').exists())

    def test_staging_preserves_defines_and_refuses_changed_sdk(self):
        with tempfile.TemporaryDirectory() as folder:
            root=Path(folder);host=root/'Artifacts/uikit/ci-selected/hosts/task/host';p=host/'ProjectSettings/ProjectSettings.asset';p.parent.mkdir(parents=True);p.write_text('  other:\n    Standalone: 0\n  scriptingDefineSymbols:\n    Standalone: EXISTING\n  tail: 1\n')
            source=root/r3_sdk.SOURCE
            with tarfile.open(source,'w:gz') as tar:
                data=b'// staging fixture only';entry=tarfile.TarInfo('R3-1.3.1/src/R3.Unity/Assets/R3.Unity/Runtime/R3.Unity.asmdef');entry.size=len(data);tar.addfile(entry,io.BytesIO(data))
            archive=root/'fixture.nupkg'
            with zipfile.ZipFile(archive,'w') as z:z.writestr('lib/netstandard2.1/R3.dll',b'staging-fixture-not-sdk')
            files={archive.name:(hashlib.sha256(archive.read_bytes()).hexdigest(),'lib/netstandard2.1/R3.dll')}
            with patch.object(r3_sdk,'ROOT',root),patch.object(r3_sdk,'SOURCE_SHA',hashlib.sha256(source.read_bytes()).hexdigest()),patch.object(r3_sdk,'FILES',files):
                receipt=r3_sdk.stage(root,host);self.assertTrue(r3_sdk.integration_enabled(host));self.assertEqual(2,len(receipt['files']))
                self.assertIn('    Standalone: 0\n',p.read_text());self.assertIn('EXISTING;WULATO_UIKIT_R3',p.read_text())
                self.assertIn('validateReferences: 1',(host/'Assets/UIKitR3SDK/Plugins/R3.dll.meta').read_text())
                r3_sdk.stage(root,host,False);self.assertFalse(r3_sdk.integration_enabled(host));self.assertIn('EXISTING',p.read_text())
                (host/'Assets/UIKitR3SDK/Plugins/R3.dll').write_bytes(b'changed')
                with self.assertRaisesRegex(ValueError,'refuse-overwriting'):r3_sdk.stage(root,host)

class OptionalBindingTests(unittest.TestCase):
    def test_binding_is_opt_in_and_independent_of_dotween(self):
        from run import applicable_suites
        m={'contracts':['core'],'r3-binding':['binding'],'dotween':['tween']};manifest={'dependencies':{'com.wulato.uikit':'file:main'}}
        self.assertEqual({'contracts':['core']},applicable_suites(m,manifest))
        self.assertEqual({'contracts':['core'],'r3-binding':['binding']},applicable_suites(m,manifest,r3=True))

    def test_sample_and_tests_are_guarded_without_core_dependencies(self):
        root=r3_sdk.ROOT/'Packages/com.wulato.uikit'
        for sub in ('Samples~/R3','Samples~/R3/Tests'):
            d=json.loads(next((root/sub).glob('*.asmdef')).read_text());self.assertIn('WULATO_UIKIT_R3',d['defineConstraints']);self.assertEqual(['R3.dll'],d['precompiledReferences']);self.assertTrue(d['overrideReferences']);self.assertNotIn('Wulato.UIKit.Tests.Support',d['references'])
        test=json.loads((root/'Samples~/R3/Tests/Wulato.UIKit.Samples.R3.Tests.asmdef').read_text());self.assertEqual([],test['includePlatforms']);self.assertIn('TestAssemblies',test['optionalUnityReferences'])
        self.assertNotIn('r3',json.dumps(json.loads((root/'package.json').read_text())['dependencies']).lower())
        self.assertEqual(4,len(list((root/'Runtime').rglob('*.asmdef'))))

    def test_selector_requires_explicit_sdk_suite(self):
        from ci_suites import select_suites,plan
        self.assertEqual({('PlayMode','r3-binding','legacy'),('EditMode','package-install','legacy')},set(select_suites(['Packages/com.wulato.uikit/Samples~/R3/CounterView.cs'])))

        selected=plan(['Packages/com.wulato.uikit/Samples~/R3/CounterView.cs'])
        self.assertEqual({'r3-binding','package-install'},{r['suite'] for r in selected['tests']})

    def test_locked_dependency_assets_match_netstandard21_host(self):
        self.assertEqual(6,len(r3_sdk.FILES));self.assertIn('microsoft.bcl.asyncinterfaces.6.0.0.nupkg',r3_sdk.FILES)
        self.assertTrue(r3_sdk.FILES['r3.1.3.1.nupkg'][1].startswith('lib/netstandard2.1/'))
        self.assertFalse(any('tasks.extensions' in name for name in r3_sdk.FILES))
