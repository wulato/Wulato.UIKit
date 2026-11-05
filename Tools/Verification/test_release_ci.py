import tempfile,unittest
from pathlib import Path
from build import file_index
from release_ci import assess_install_record
class InstallOnlyTests(unittest.TestCase):
    def fixture(self,folder):
        (folder/'raw.log').write_text('UIKIT_BUILD_STAGE_OK')
        return dict(status='BLOCKED',install_status='PASS',phase='phase2',fingerprint='current',source_after='current',generation_compiled=True,errors=['prepare-only; no mobile Player built'],files=file_index(folder))
    def test_expected_no_player_blocker_can_pass_install(self):
        with tempfile.TemporaryDirectory() as d:
            root=Path(d);self.assertEqual([],assess_install_record(self.fixture(root),root,'phase2','current'))
    def test_other_blocker_is_not_ignored(self):
        with tempfile.TemporaryDirectory() as d:
            root=Path(d);record=self.fixture(root);record['errors']=['missing-sdk'];self.assertTrue(assess_install_record(record,root,'phase2','current'))
    def test_changed_inputs_during_prepare_are_rejected(self):
        with tempfile.TemporaryDirectory() as d:
            root=Path(d);record=self.fixture(root);record['fingerprint']='old';self.assertTrue(assess_install_record(record,root,'phase2','current'))
    def test_raw_evidence_tamper_is_rejected(self):
        with tempfile.TemporaryDirectory() as d:
            root=Path(d);record=self.fixture(root);(root/'raw.log').write_text('tampered');self.assertTrue(assess_install_record(record,root,'phase2','current'))
