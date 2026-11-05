import unittest
from contract_probe import assess
class CompileProbeTests(unittest.TestCase):
    def test_only_expected_constraint_failure_is_accepted(self):
        self.assertTrue(assess(1,False,"Assets/UIKitContractProbe/ContractCompileProbe.cs(9,9): error CS0311: invalid constraint",negative=True))
        for log in ('error CS0246: missing type','error CS0311: invalid\nOther.cs(1,1): error CS0103: typo','', 'Other.cs(1,1): error CS0311: invalid'):
            self.assertFalse(assess(1,False,log,negative=True))
    def test_timeout_or_zero_exit_cannot_satisfy_negative_probe(self):
        log='Assets/UIKitContractProbe/ContractCompileProbe.cs(9,9): error CS0311: invalid'
        self.assertFalse(assess(0,False,log,negative=True));self.assertFalse(assess(1,True,log,negative=True))
    def test_positive_requires_successful_compile_and_marker(self):
        self.assertFalse(assess(0,False,'',negative=False))
        self.assertTrue(assess(0,False,'UIKIT_CONTRACT_PROBE_OK',negative=False))
        self.assertFalse(assess(0,False,'UIKIT_CONTRACT_PROBE_OK\nerror CS0246: missing',negative=False))

class ProbeArtifactIntegrationTests(unittest.TestCase):
    def test_probe_reports_do_not_look_like_orphaned_test_attempts(self):
        import tempfile
        from pathlib import Path
        from types import SimpleNamespace
        from unittest.mock import patch
        import run
        from contract_probe import run_probe
        from registry import registered
        with tempfile.TemporaryDirectory() as folder:
            root=Path(folder);host=root/'host';(host/'Assets').mkdir(parents=True)
            args=SimpleNamespace(editor='2022.3.62f2',backend='legacy')
            def compiler(args,ver,host,out):
                if out.name=='negative':return 1,False,'Assets/UIKitContractProbe/ContractCompileProbe.cs(9,9): error CS0311: invalid',ver
                return 0,False,'UIKIT_CONTRACT_PROBE_OK',ver
            with patch.object(run,'A',root/'artifacts'),patch.object(run,'selected',return_value=[('2022.3.62f2','legacy',host)]),patch.object(run,'fingerprint',return_value='unchanged'),patch.object(run,'snapshot',return_value={'errors':[]}),patch.object(run,'unity',side_effect=compiler):
                self.assertEqual(0,run_probe(args))
                records,errors=registered(run.A)
                self.assertEqual([],errors)
                self.assertEqual([],records)
