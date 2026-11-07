import unittest
from smoke import validate_player_report
class MobileEvidenceTests(unittest.TestCase):
    def test_transport_cannot_claim_different_cases_from_player(self):
        raw=dict(build_id='a',status='PASS',cases=[dict(name='TouchClose',status='FAIL')])
        summary=dict(build_id='a',status='PASS',cases=[dict(name='TouchClose',status='PASS')])
        self.assertTrue(validate_player_report(raw,summary))
    def test_wrong_raw_build_id_rejected(self):
        self.assertTrue(validate_player_report(dict(build_id='old',status='PASS',cases=[]),dict(build_id='new',status='PASS',cases=[])))
    def test_matching_raw_report(self):
        raw=dict(build_id='a',status='PASS',cases=[])
        self.assertEqual([],validate_player_report(raw,raw))
    def test_android_report_uses_unity_external_files_directory(self):
        from smoke import android_report_path
        self.assertEqual('/storage/emulated/10/Android/data/com.wulato.uikit/files/uikit-smoke.json',android_report_path('com.wulato.uikit','10'))
    def test_android_report_rejects_invalid_user_or_package(self):
        from smoke import android_report_path
        for package,user in [('com.example;bad','0'),('com.example','../0')]:
            with self.assertRaises(ValueError):android_report_path(package,user)
    def test_external_report_transport_uses_adb_shell_storage_access(self):
        from smoke import android_report_commands
        clear,read=android_report_commands(['adb','-s','physical'],'/storage/emulated/0/Android/data/com.example/files/uikit-smoke.json')
        self.assertEqual(['adb','-s','physical','shell','rm','-f','/storage/emulated/0/Android/data/com.example/files/uikit-smoke.json'],clear)
        self.assertEqual(['adb','-s','physical','shell','cat','/storage/emulated/0/Android/data/com.example/files/uikit-smoke.json'],read)
    def test_ios_paired_wired_device_does_not_require_preopened_tunnel(self):
        from smoke import ios_device_candidates
        device={'identifier':'physical','hardwareProperties':{'udid':'udid'},'connectionProperties':{'pairingState':'paired','transportType':'wired','tunnelState':'disconnected'}}
        self.assertEqual([device],ios_device_candidates([device],'udid'))
    def test_ios_unavailable_or_unpaired_device_is_not_candidate(self):
        from smoke import ios_device_candidates
        for connection in ({'pairingState':'paired','tunnelState':'unavailable'},{'pairingState':'unpaired','tunnelState':'connected'}):
            self.assertEqual([],ios_device_candidates([{'identifier':'physical','connectionProperties':connection}],'physical'))

class IOSReportFreshnessTests(unittest.TestCase):
    def test_reset_requires_roundtrip_of_unique_pending_marker(self):
        from smoke import reset_ios_report
        from unittest.mock import patch
        import tempfile,json
        from pathlib import Path
        with tempfile.TemporaryDirectory() as d:
            out=Path(d);calls=[]
            def copy(cmd,log,timeout):
                calls.append(cmd)
                if 'from' in cmd:
                    (out/'reset-verified.json').write_text((out/'pending-report.json').read_text())
                return 0
            with patch('smoke.command',side_effect=copy):reset_ios_report('device','test.package',out)
            marker=json.loads((out/'pending-report.json').read_text())
            self.assertEqual('PENDING',marker['status']);self.assertTrue(marker['nonce'])
            self.assertEqual(['to','from'],[cmd[4] for cmd in calls])
            for cmd in calls:self.assertIn('appDataContainer',cmd);self.assertIn('test.package',cmd)
    def test_old_pass_and_failed_reset_are_rejected(self):
        from smoke import reset_ios_report
        from unittest.mock import patch
        import tempfile,json
        from pathlib import Path
        with tempfile.TemporaryDirectory() as d:
            out=Path(d);(out/'reset-verified.json').write_text(json.dumps({'status':'PASS'}))
            with patch('smoke.command',return_value=0):
                with self.assertRaisesRegex(ValueError,'reset-mismatch'):reset_ios_report('device','test.package',out)
            with patch('smoke.command',return_value=1):
                with self.assertRaisesRegex(ValueError,'cannot-reset'):reset_ios_report('device','test.package',out)
