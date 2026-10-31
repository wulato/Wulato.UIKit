import tempfile
import unittest
from pathlib import Path
from results import evaluate

class ResultsTests(unittest.TestCase):
    def check_result(self, xml, log='', required=('Case',), **kwargs):
        with tempfile.TemporaryDirectory() as d:
            p=Path(d)/'results.xml'
            if xml is not None: p.write_text(xml)
            return evaluate(p, log, required, **kwargs)['status']
    def test_missing_xml_fails(self): self.assertEqual('FAIL',self.check_result(None))
    def test_zero_tests_fails(self): self.assertEqual('FAIL',self.check_result('<test-run/>'))
    def test_skipped_gate_fails(self): self.assertEqual('FAIL',self.check_result('<test-run><test-case fullname="Case" result="Skipped"/></test-run>'))
    def test_compilation_error_fails(self): self.assertEqual('FAIL',self.check_result(self.green(),'error CS0246: missing'))
    def test_missing_required_fails(self): self.assertEqual('FAIL',self.check_result(self.green(),required=('Other',)))
    def test_timeout_fails(self): self.assertEqual('FAIL',self.check_result(self.green(),timed_out=True))
    def test_inconclusive_fails(self): self.assertEqual('FAIL',self.check_result(self.green().replace('Passed','Inconclusive')))
    def test_failed_case_fails(self): self.assertEqual('FAIL',self.check_result(self.green().replace('Passed','Failed')))
    def test_nonzero_exit_fails(self): self.assertEqual('FAIL',self.check_result(self.green(),returncode=1))
    def test_passes(self): self.assertEqual('PASS',self.check_result(self.green()))
    def green(self): return '<test-run result="Passed"><test-case fullname="Case" result="Passed"/></test-run>'
