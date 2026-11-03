import unittest
from play_reset import validate_report, validate_automatic_reports, seal_reset
class PlayResetTests(unittest.TestCase):
    def test_requires_two_real_sessions_and_all_counters(self):
        good=dict(schema=1,status='PASS',completed=2,domainReload=True,sceneReload=True,activeContexts=0,oldLocks=0,subscriptions=0)
        self.assertEqual([],validate_report(good,0))
        for key in good:
            broken=dict(good);broken.pop(key)
            self.assertTrue(validate_report(broken,0),key)
        for key in ('activeContexts','oldLocks','subscriptions','completed'):
            broken=dict(good);broken[key]=1
            self.assertTrue(validate_report(broken,0),key)
    def test_reload_flags_must_match_requested_option(self):
        for option in range(4):
            good=dict(schema=1,status='PASS',completed=2,domainReload=not bool(option&1),sceneReload=not bool(option&2),activeContexts=0,oldLocks=0,subscriptions=0)
            self.assertEqual([],validate_report(good,option))
            good['domainReload']=not good['domainReload'];self.assertTrue(validate_report(good,option))

    def test_both_automatic_exit_records_are_required(self):
        import tempfile,json
        from pathlib import Path
        with tempfile.TemporaryDirectory() as folder:
            out=Path(folder)
            self.assertTrue(validate_automatic_reports(out))
            good=dict(status='PASS',activeContexts=0,oldLocks=0,subscriptions=0)
            for cycle in range(2):
                (out/('automatic-exit-'+str(cycle)+'.json')).write_text(json.dumps(good))
            self.assertEqual([],validate_automatic_reports(out))
            for content in ('[]','{}','{"status":"PASS","activeContexts":0,"oldLocks":1,"subscriptions":0}'):
                (out/'automatic-exit-1.json').write_text(content)
                self.assertTrue(validate_automatic_reports(out))

    def test_reset_and_both_exit_reports_are_sealed(self):
        import tempfile,json
        from pathlib import Path
        from evidence import sha256
        with tempfile.TemporaryDirectory() as folder:
            out=Path(folder)
            names=['play-reset.json','automatic-exit-0.json','automatic-exit-1.json']
            for name in names:(out/name).write_text('{}')
            digest=seal_reset(out)
            self.assertEqual(digest,sha256(out/'evidence-index.json'))
            files=json.loads((out/'evidence-index.json').read_text())['files']
            for name in names:self.assertEqual(files[name],sha256(out/name))
            (out/names[0]).write_text('{"tampered":true}')
            self.assertNotEqual(files[names[0]],sha256(out/names[0]))

    def test_host_reload_scope_uses_only_requested_options(self):
        from play_reset import reload_options
        self.assertEqual([0,1,2,3],reload_options('context-host','legacy'))
        self.assertEqual([0,3],reload_options('context-host','input'))
        self.assertEqual([0,1,2,3],reload_options('integration','input'))
