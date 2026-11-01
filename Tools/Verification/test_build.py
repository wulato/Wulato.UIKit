import json,tempfile,unittest
from pathlib import Path
from build import prepare_consumer,verify_files,file_index
class BuildTests(unittest.TestCase):
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
