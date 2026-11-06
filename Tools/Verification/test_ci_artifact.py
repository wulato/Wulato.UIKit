import json,tempfile,unittest
from pathlib import Path
from unittest.mock import patch
import ci_artifact as artifact
class PortableArtifactTests(unittest.TestCase):
 def test_export_preserves_raw_failure_logs_and_excludes_hosts_sdks_and_libraries(self):
  with tempfile.TemporaryDirectory() as directory:
   root=Path(directory);a=root/'Artifacts/uikit';a.mkdir(parents=True)
   (root/'Packages/com.wulato.uikit').mkdir(parents=True);(root/'Packages/com.wulato.uikit/package.json').write_text('{"version":"0.5.0"}')
   paths=['failed/editor.log','ci-selected/selection.json','ci-selected/hosts/project/Assets/secret.cs','project/Library/cache','snapshot/git-source/secret.cs','sdk/vendor.dll','runner/.credentials','runner/_diag/log']
   for name in paths:
    p=a/name;p.parent.mkdir(parents=True,exist_ok=True);p.write_text('raw failure evidence')
   with patch.dict('os.environ',{'GITHUB_WORKSPACE':str(root)}),patch.object(artifact.run,'R',root),patch.object(artifact.run,'A',a),patch.object(artifact.build,'source_fingerprint',return_value='fingerprint'),patch('subprocess.check_output',return_value='a'*40+'\n'):
    exported=artifact.export(a/'upload')
    self.assertEqual({'failed/editor.log','ci-selected/selection.json'},set(exported['files']))
    self.assertEqual((a/'failed/editor.log').read_bytes(),(a/'upload/failed/editor.log').read_bytes())
    with self.assertRaisesRegex(ValueError,'overwriting'):artifact.export(a/'upload')
 def fixture(self,folder):
  data=dict(schema=1,revision='a'*40,run_id='123',source_fingerprint='source',package_version='0.5.0',origin='/remote/repo',files={})
  (folder/'artifact.json').write_text(json.dumps(data));return data
 def test_wrong_revision_run_source_or_modified_files_fail_before_raw_parsing(self):
  with tempfile.TemporaryDirectory() as directory:
   folder=Path(directory);data=self.fixture(folder)
   with patch.object(artifact.build,'source_fingerprint',return_value='different'):
    result=artifact.verify(folder,'b'*40,'456')
   for error in ['artifact-revision','artifact-run-id','artifact-source-fingerprint']:self.assertIn(error,result['errors'])
   (folder/'nested').mkdir();(folder/'nested/artifact.json').write_text('extra')
   with patch.object(artifact.build,'source_fingerprint',return_value='source'):self.assertIn('artifact-file-set',artifact.verify(folder,'a'*40,'123')['errors'])
 def test_path_traversal_is_rejected(self):
  with tempfile.TemporaryDirectory() as directory:
   folder=Path(directory);data=self.fixture(folder);data['files']={'../outside':'hash'};(folder/'artifact.json').write_text(json.dumps(data))
   with patch.object(artifact.build,'source_fingerprint',return_value='source'):
    with self.assertRaisesRegex(ValueError,'unsafe-artifact-member'):artifact.verify(folder,'a'*40,'123')

 def test_export_refuses_an_arbitrary_local_workspace(self):
  with patch.dict('os.environ',{'GITHUB_WORKSPACE':''}):
   with self.assertRaisesRegex(ValueError,'current-github-workspace'):artifact.export('/tmp/not-a-ci-job')

 def test_symlink_is_rejected_without_reading_its_target(self):
  with tempfile.TemporaryDirectory() as directory:
   folder=Path(directory);self.fixture(folder);(folder/'external').symlink_to('/etc/passwd')
   with patch.object(artifact.build,'source_fingerprint',return_value='source'):
    self.assertIn('artifact-symlink',artifact.verify(folder,'a'*40,'123')['errors'])
