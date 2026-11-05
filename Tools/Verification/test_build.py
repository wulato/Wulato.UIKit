import hashlib,json,os,subprocess,tempfile,unittest
from pathlib import Path
from unittest.mock import patch
from build import prepare_consumer,verify_files,file_index

class ConsumerFixtureTests(unittest.TestCase):
    def setUp(self):
        # These tests isolate manifest/assembly composition; snapshot provenance has real Git tests below.
        import run
        files={p.relative_to(run.R).as_posix():p.read_bytes() for p in (run.R/'Packages').rglob('*') if p.is_file()}
        mock=patch('build.git_package_files',return_value=files);mock.start();self.addCleanup(mock.stop)

class BuildTests(ConsumerFixtureTests):
    def test_changed_or_missing_artifact_rejected(self):
        with tempfile.TemporaryDirectory() as d:
            root=Path(d);p=root/'player.json';p.write_text('{}');record={'files':file_index(root)}
            self.assertEqual([],verify_files(root,record));p.write_text('changed');self.assertTrue(verify_files(root,record));p.unlink();self.assertTrue(verify_files(root,record))
    def test_artifact_path_cannot_escape(self):
        with tempfile.TemporaryDirectory() as d:self.assertTrue(verify_files(Path(d),{'files':{'../secret':'hash'}}))
    def test_clean_manifest_has_no_test_or_verification_dependency(self):
        import run
        with tempfile.TemporaryDirectory() as d:
            host=Path(d)/'consumer';source=run.R
            prepare_consumer(host,'2022.3.62f2','input',source,'a'*40,'clean')
            data=json.loads((host/'Packages/manifest.json').read_text())
            self.assertNotIn('testables',data)
            self.assertFalse(any(any(n in key for n in ('test','acceptance','verification')) for key in data['dependencies']))
            self.assertIn('#'+'a'*40,data['dependencies']['com.wulato.uikit.input-system'])
            self.assertFalse((host/'Assets/UIKitMobileSmoke').exists())
    def test_smoke_entry_is_non_test_assembly(self):
        import run
        with tempfile.TemporaryDirectory() as d:
            host=Path(d)/'consumer';prepare_consumer(host,'2022.3.62f2','legacy',run.R,'b'*40,'smoke')
            data=json.loads((host/'Assets/UIKitMobileSmoke/Wulato.UIKit.MobileSmoke.asmdef').read_text())
            self.assertNotIn('optionalUnityReferences',data);self.assertNotIn('defineConstraints',data)
            self.assertTrue((host/'Assets/UIKitMobileSmoke/Editor/MobileSmokeSceneBuilder.cs').is_file())
    def test_git_url_is_unambiguous_to_upm(self):
        import run
        with tempfile.TemporaryDirectory() as d:
            host=Path(d)/'consumer';prepare_consumer(host,'2022.3.62f2','legacy',run.R,'a'*40,'clean')
            url=json.loads((host/'Packages/manifest.json').read_text())['dependencies']['com.wulato.uikit']
            self.assertTrue(url.startswith('git+file://'))
    def test_latest_inventory_survives_deleted_attempt(self):
        from build import latest_attempt
        with tempfile.TemporaryDirectory() as d:
            root=Path(d)
            for stamp in ('01','02'):
                batch=root/stamp;batch.mkdir();(batch/'inventory.json').write_text(json.dumps({'combinations':[{'editor':'2022.3.62f2','backend':'legacy','target':'Android','profile':'smoke'}]}))
            self.assertEqual(root/'02/2022.3.62f2-legacy-Android-smoke',latest_attempt(root,'2022.3.62f2','legacy','Android','smoke'))
    def test_build_identity_changes_with_dependency_lock(self):
        from build import build_identity
        data={'editor':'2022.3.62f2','dependency_lock_sha256':'a'}
        other=dict(data,dependency_lock_sha256='b')
        self.assertNotEqual(build_identity(data),build_identity(other))
    def test_raw_build_report_cannot_claim_other_build(self):
        from build import validate_build_report
        self.assertTrue(validate_build_report({'build_id':'old'},{'build_id':'new'}))
    def test_mobile_matrix_aggregate_is_not_a_test_registration(self):
        from build import write_matrix_report
        from registry import registered
        with tempfile.TemporaryDirectory() as d:
            root=Path(d);batch=root/'mobile/attempt';batch.mkdir(parents=True)
            write_matrix_report(batch,[{'status':'BLOCKED'}])
            self.assertEqual(([],[]),registered(root))
    def test_smoke_preserves_cross_assembly_stripping_fixture(self):
        import run
        with tempfile.TemporaryDirectory() as d:
            host=Path(d)/'consumer';prepare_consumer(host,'2022.3.62f2','legacy',run.R,'c'*40,'smoke')
            definition=json.loads((host/'Assets/UIKitCodegenFixture/B/UIKit.Codegen.B.asmdef').read_text())
            self.assertIs(False,definition['autoReferenced'])
            self.assertTrue((host/'Assets/UIKitCodegenFixture/Default/DefaultView.cs').is_file())
    def test_macos_hub_modules_beside_app_are_found(self):
        from build import find_build_module
        with tempfile.TemporaryDirectory() as d:
            root=Path(d)/'2022.3.62f2';exe=root/'Unity.app/Contents/MacOS/Unity';exe.parent.mkdir(parents=True);exe.touch()
            (root/'Unity.app/Contents/PlaybackEngines/MacStandaloneSupport').mkdir(parents=True)
            wanted=root/'PlaybackEngines/AndroidPlayer';wanted.mkdir(parents=True)
            self.assertEqual(wanted,find_build_module(exe,'Android'))
    def test_modules_inside_bundle_are_also_found(self):
        from build import find_build_module
        with tempfile.TemporaryDirectory() as d:
            exe=Path(d)/'Unity.app/Contents/MacOS/Unity';exe.parent.mkdir(parents=True);exe.touch()
            wanted=exe.parent.parent/'PlaybackEngines/iOSSupport';wanted.mkdir(parents=True)
            self.assertEqual(wanted,find_build_module(exe,'iOS'))
    def test_build_report_error_count_does_not_replace_runner_errors(self):
        from build import merge_player_report
        result=merge_player_report({'status':'FAIL','errors':[]},{'status':'PASS','errors':0,'output':'UIKit.apk'})
        result['errors'].append('late-validation-failure')
        self.assertEqual(0,result['build_error_count'])
        self.assertEqual(['late-validation-failure'],result['errors'])

class PhaseTwoConsumerTests(ConsumerFixtureTests):
    def test_phase_two_consumes_all_extensions_from_same_git_revision(self):
        import run
        from runtime import FLOW_EXTENSIONS as EXTENSIONS
        with tempfile.TemporaryDirectory() as folder:
            host=Path(folder)/'consumer';prepare_consumer(host,'2022.3.62f2','legacy',run.R,'b'*40,'clean',phase='phase2')
            manifest=json.loads((host/'Packages/manifest.json').read_text())
            self.assertIn('#'+'b'*40,manifest['dependencies']['com.wulato.uikit'])
            self.assertFalse(set(EXTENSIONS)&set(manifest['dependencies']))
            self.assertTrue((host/'Assets/Samples/Wulato UIKit/BusinessFlow/BusinessFlowDemo.cs').exists())
            self.assertNotIn('testables',manifest)
    def test_removed_extension_install_requires_explicit_migration(self):
        import run
        with tempfile.TemporaryDirectory() as folder:
            host=Path(folder)/'consumer'
            with self.assertRaisesRegex(ValueError,'migration'):
                prepare_consumer(host,'2022.3.62f2','legacy',run.R,'c'*40,'clean',phase='phase2',package_set='navigation')
            self.assertFalse(host.exists())

class PhaseThreeConsumerTests(ConsumerFixtureTests):
    def test_main_install_does_not_pull_addressables(self):
        import run
        with tempfile.TemporaryDirectory() as folder:
            host=Path(folder)/'consumer';prepare_consumer(host,'6000.3.22f1','legacy',run.R,'d'*40,'clean',phase='phase3',package_set='main')
            deps=json.loads((host/'Packages/manifest.json').read_text())['dependencies']
            self.assertNotIn('com.wulato.uikit.addressables',deps);self.assertNotIn('com.unity.addressables',deps)
    def test_addressables_install_is_independent_and_pinned_per_editor(self):
        import run
        from runtime import EXTENSIONS
        for editor,wanted in [('2022.3.62f2','1.21.21'),('6000.3.22f1','2.9.1')]:
            with tempfile.TemporaryDirectory() as folder:
                host=Path(folder)/'consumer';prepare_consumer(host,editor,'legacy',run.R,'e'*40,'clean',phase='phase3',package_set='addressables')
                deps=json.loads((host/'Packages/manifest.json').read_text())['dependencies']
                self.assertEqual({'com.wulato.uikit.addressables'},set(EXTENSIONS)&set(deps))
                self.assertIn('#'+'e'*40,deps['com.wulato.uikit.addressables']);self.assertEqual(wanted,deps['com.unity.addressables'])
                self.assertTrue((host/'Assets/Samples/Wulato UIKit/LocalInventory/Runtime/LocalInventoryDemo.cs').is_file())
    def test_phase3_all_installs_every_extension_at_same_revision(self):
        import run
        from runtime import EXTENSIONS
        with tempfile.TemporaryDirectory() as folder:
            host=Path(folder)/'consumer';prepare_consumer(host,'2022.3.62f2','input',run.R,'f'*40,'clean',phase='phase3')
            deps=json.loads((host/'Packages/manifest.json').read_text())['dependencies']
            for name in EXTENSIONS:self.assertIn('#'+'f'*40,deps[name])
            self.assertNotIn('testables',json.loads((host/'Packages/manifest.json').read_text()))
    def test_phase3_rejects_player_builds_and_wrong_scope(self):
        from build import validate_build_scope
        import argparse
        for profile,prepare_only,package_set in [('smoke',True,'all'),('clean',False,'all'),('clean',True,'navigation')]:
            self.assertTrue(validate_build_scope(argparse.Namespace(phase='phase3',profile=profile,prepare_only=prepare_only,package_set=package_set)))
        self.assertEqual([],validate_build_scope(argparse.Namespace(phase='phase3',profile='clean',prepare_only=True,package_set='addressables')))

class RuntimeClosureTests(unittest.TestCase):
    def fixture(self):
        return {'runtimeAssemblies':['UIKit.Consumer','Wulato.UIKit.Core','UnityEngine.TestRunner'],
                'runtimeAssemblyGraph':[{'name':'UIKit.Consumer','references':['Wulato.UIKit.Core'],'metadataReferences':[],'assetSource':True},
                    {'name':'Wulato.UIKit.Core','references':[],'metadataReferences':[],'assetSource':False},
                    {'name':'UnityEngine.TestRunner','references':[],'metadataReferences':[],'assetSource':False}]}
    def test_unused_test_candidate_is_not_a_player_dependency(self):
        from build import runtime_dependency_closure
        self.assertEqual(['UIKit.Consumer','Wulato.UIKit.Core'],runtime_dependency_closure(self.fixture()))
    def test_reachable_test_assembly_is_rejected(self):
        from build import runtime_dependency_closure
        report=self.fixture();report['runtimeAssemblyGraph'][0]['references'].append('UnityEngine.TestRunner')
        with self.assertRaises(ValueError):runtime_dependency_closure(report)
    def test_missing_graph_or_unresolved_reference_cannot_pass(self):
        from build import runtime_dependency_closure
        for report in ({'runtimeAssemblies':['UIKit.Consumer']},self.fixture()):
            if 'runtimeAssemblyGraph' in report:report['runtimeAssemblyGraph'][0]['references'].append('Missing.Assembly')
            with self.assertRaises(ValueError):runtime_dependency_closure(report)

class PrecompiledRuntimeClosureTests(unittest.TestCase):
    def fixture(self):return RuntimeClosureTests().fixture()
    def test_reachable_precompiled_nunit_is_rejected(self):
        from build import runtime_dependency_closure
        report=self.fixture();report['runtimeAssemblyGraph'][0]['precompiledReferences']=['nunit.framework']
        report['runtimeAssemblyGraph'][0]['metadataReferences']=['nunit.framework']
        with self.assertRaises(ValueError):runtime_dependency_closure(report)

class EngineProtocolReferenceTests(unittest.TestCase):
    def test_builtin_engine_protocol_is_not_a_test_framework(self):
        from build import runtime_dependency_closure
        report=RuntimeClosureTests().fixture()
        report['runtimeAssemblyGraph'][0]['precompiledReferences']=['UnityEngine.UnityTestProtocolModule','UnityEngine.CoreModule']
        self.assertEqual(['UIKit.Consumer','Wulato.UIKit.Core'],runtime_dependency_closure(report))

class CompilerOnlyReferenceTests(unittest.TestCase):
    def test_unused_compiler_nunit_reference_is_not_an_il_import(self):
        from build import runtime_dependency_closure
        report=RuntimeClosureTests().fixture()
        report['runtimeAssemblyGraph'][0]['precompiledReferences']=['nunit.framework']
        report['runtimeAssemblyGraph'][0]['metadataReferences']=[]
        self.assertEqual(['UIKit.Consumer','Wulato.UIKit.Core'],runtime_dependency_closure(report))

class GitSnapshotTests(unittest.TestCase):
    def setUp(self):
        self.temp=tempfile.TemporaryDirectory();self.addCleanup(self.temp.cleanup)
        self.root=Path(self.temp.name);self.inputs=self.root/'inputs'
        for package in ('com.wulato.uikit','com.wulato.uikit.input-system','com.wulato.uikit.addressables'):
            folder=self.inputs/'Packages'/package;folder.mkdir(parents=True)
            (folder/'package.json').write_text('{}\n')
            docs=folder/'Documentation~';docs.mkdir();(docs/'index.md').write_text('documentation\n')
            for name in ('QuickStart','Modals','Inventory','BusinessFlow','LocalInventory'):
                sample=folder/'Samples~'/name;sample.mkdir(parents=True)
                (sample/'View.cs').write_text('committed sample\n');(sample/'View.cs.meta').write_text('committed guid\n')
        ignore=self.root/'global-ignore';ignore.write_text('*~\n')
        config=self.root/'gitconfig';config.write_text('[core]\n\texcludesFile = '+str(ignore)+'\n')
        env=patch.dict(os.environ,{'GIT_CONFIG_GLOBAL':str(config),'GIT_CONFIG_NOSYSTEM':'1'});env.start();self.addCleanup(env.stop)
        import run
        inputs=patch.object(run,'R',self.inputs);inputs.start();self.addCleanup(inputs.stop)
        self.out=self.root/'attempt';self.out.mkdir()
    def snapshot(self):
        from build import git_source
        return git_source(self.out)
    def test_snapshot_contains_globally_ignored_samples_docs_and_meta(self):
        from build import git_package_files
        source,revision=self.snapshot();files=git_package_files(source,revision)
        expected={p.relative_to(self.inputs).as_posix():p.read_bytes() for p in (self.inputs/'Packages').rglob('*') if p.is_file()}
        self.assertEqual(expected,files)
        report=json.loads((self.out/'package-snapshot.json').read_text())
        self.assertEqual(revision,report['git_revision'])
        self.assertEqual({name:hashlib.sha256(body).hexdigest() for name,body in expected.items()},report['files'])
    def test_snapshot_validator_rejects_missing_extra_and_changed_files(self):
        from build import validate_git_snapshot
        source,revision=self.snapshot();expected=json.loads((self.out/'package-snapshot.json').read_text())['files']
        for change in ('missing','extra','changed'):
            wrong=dict(expected)
            if change=='missing':wrong['Packages/missing.txt']='0'*64
            elif change=='extra':wrong.pop(next(iter(wrong)))
            else:wrong[next(iter(wrong))]='0'*64
            with self.subTest(change=change),self.assertRaisesRegex(ValueError,'git-snapshot-content-mismatch'):
                validate_git_snapshot(source,revision,wrong)
    def test_consumer_imports_pinned_sample_bytes_despite_dirty_snapshot_worktree(self):
        source,revision=self.snapshot()
        sample=source/'Packages/com.wulato.uikit/Samples~/QuickStart'
        (sample/'View.cs').write_text('uncommitted edit\n');(sample/'Untracked.cs').write_text('untracked\n')
        (sample/'View.cs.meta').unlink()
        host=self.root/'consumer'
        # Restore the real repository location for host ProjectSettings, without changing the snapshot source.
        import run
        with patch.object(run,'R',Path(__file__).resolve().parents[2]):
            prepare_consumer(host,'2022.3.62f2','legacy',source,revision,'clean',phase='phase3',package_set='main')
        imported=host/'Assets/Samples/Wulato UIKit/QuickStart'
        self.assertEqual('committed sample\n',(imported/'View.cs').read_text())
        self.assertEqual('committed guid\n',(imported/'View.cs.meta').read_text())
        self.assertFalse((imported/'Untracked.cs').exists())
    def test_missing_committed_sample_fails_even_if_worktree_has_it(self):
        source,revision=self.snapshot()
        subprocess.run(['git','-C',str(source),'rm','-r','--cached','Packages/com.wulato.uikit/Samples~/QuickStart'],check=True,stdout=subprocess.DEVNULL)
        subprocess.run(['git','-C',str(source),'-c','user.name=Test','-c','user.email=test@localhost','commit','-m','Remove committed sample'],check=True,stdout=subprocess.DEVNULL)
        revision=subprocess.check_output(['git','-C',str(source),'rev-parse','HEAD']).decode().strip()
        import run
        with patch.object(run,'R',Path(__file__).resolve().parents[2]),self.assertRaisesRegex(ValueError,'git-snapshot-missing-sample'):
            prepare_consumer(self.root/'consumer','2022.3.62f2','legacy',source,revision,'clean',phase='phase3',package_set='main')
