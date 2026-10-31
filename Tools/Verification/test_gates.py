import unittest
from gates import assess
class GateTests(unittest.TestCase):
    def result(self,**extra): return dict(status='PASS',fingerprint='now',cases=[dict(name='Case',result='Passed')],**extra)
    def test_missing_matrix_member_fails(self): self.assertNotEqual('PASS',assess([],{'Case'},'now')['status'])
    def test_stale_fingerprint_fails(self): self.assertNotEqual('PASS',assess([self.result()],{'Case'},'changed')['status'])
    def test_missing_required_fails(self): self.assertNotEqual('PASS',assess([self.result()],{'Other'},'now')['status'])
    def test_failed_result_fails(self):
        r=self.result();r['status']='FAIL';self.assertNotEqual('PASS',assess([r],{'Case'},'now')['status'])
    def test_skipped_required_fails(self):
        r=self.result();r['cases'][0]['result']='Skipped';self.assertNotEqual('PASS',assess([r],{'Case'},'now')['status'])
    def test_valid_passes(self): self.assertEqual('PASS',assess([self.result()],{'Case'},'now')['status'])
