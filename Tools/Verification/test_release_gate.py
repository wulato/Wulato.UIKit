"""Release gate must reject plausible but incomplete release evidence."""
import copy, unittest
from release_gate import assess_mobile, MOBILE_CASES, combinations

class ReleaseGateTests(unittest.TestCase):
    def report(self):
        return dict(status='PASS',build_id='build',fingerprint='current',editor='2022.3.62f2',backend='legacy',target='Android',profile='smoke',architecture='ARM64',scripting_backend='IL2CPP',compiled=True,installed=True,launched=True,device='physical',physical_device=True,cases=[dict(name=n,status='PASS') for n in MOBILE_CASES])
    def check(self,r):return assess_mobile(r,'build','current')
    def test_valid_physical_report(self):self.assertEqual([],self.check(self.report()))
    def test_full_eight_combinations(self):self.assertEqual(8,len(combinations()))
    def test_wrong_build_id(self):
        r=self.report();r['build_id']='old';self.assertIn('build-id',self.check(r))
    def test_old_fingerprint(self):
        r=self.report();r['fingerprint']='old';self.assertIn('fingerprint',self.check(r))
    def test_zero_cases(self):
        r=self.report();r['cases']=[];self.assertIn('smoke-cases',self.check(r))
    def test_export_only_ios_rejected(self):
        r=self.report();r.update(target='iOS',compiled=False,installed=False,launched=False);self.assertIn('compiled',self.check(r))
    def test_skipped_or_missing_case_rejected(self):
        for cases in (self.report()['cases'][:-1],[dict(name=n,status='SKIP') for n in MOBILE_CASES]):
            r=self.report();r['cases']=cases;self.assertIn('smoke-cases',self.check(r))
    def test_clean_cannot_substitute_smoke(self):
        r=self.report();r['profile']='clean';self.assertIn('profile',self.check(r))
    def test_simulator_cannot_substitute_device(self):
        r=self.report();r['physical_device']=False;self.assertIn('physical-device',self.check(r))
    def test_non_il2cpp_rejected(self):
        r=self.report();r['scripting_backend']='Mono';self.assertIn('scripting-backend',self.check(r))
    def test_generation_must_be_recompiled(self):
        from release_gate import assess_build
        r=dict(status='PASS',fingerprint='current',source_after='current',generation_compiled=False,profile='smoke',build_id='build',architecture='ARM64',scripting_backend='IL2CPP')
        self.assertIn('generation-not-compiled',assess_build(r,'current'))
    def test_missing_matrix_combination_is_not_pass(self):
        from release_gate import assess_matrix
        rows=[dict(editor=v,backend=b,target=t,status='PASS') for v,b,t in combinations()]
        self.assertEqual([],assess_matrix(rows));self.assertTrue(assess_matrix(rows[:-1]))
