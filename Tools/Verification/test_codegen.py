import unittest
from codegen import assess, validate_report
class CodegenTests(unittest.TestCase):
    def test_missing_or_skipped_probe_is_not_success(self):
        for report in ({}, {'status':'PASS','editor':'2022.3.62f2','checks':[]}):
            self.assertFalse(validate_report(report,'2022.3.62f2',{'BindingUsageProbe_CompilesAndRuns'}))
    def test_reports_require_actual_version_and_all_checks(self):
        report={'status':'PASS','editor':'2022.3.62f2','checks':['one','two']}
        self.assertTrue(validate_report(report,'2022.3.62f2',{'one','two'}))
        self.assertFalse(validate_report(report,'6000.3.22f1',{'one'}))
    def test_compiler_errors_timeout_or_missing_marker_fail(self):
        self.assertTrue(assess(0,False,'UIKIT_CODEGEN_OK'))
        for rc,to,log in [(1,False,'UIKIT_CODEGEN_OK'),(0,True,'UIKIT_CODEGEN_OK'),(0,False,'error CS0010 UIKIT_CODEGEN_OK'),(0,False,'')]:
            self.assertFalse(assess(rc,to,log))

    def test_existing_fixture_drift_is_rejected_without_overwrite(self):
        import tempfile
        from pathlib import Path
        from codegen import fixture
        with tempfile.TemporaryDirectory() as directory:
            root=Path(directory);host=root/'host';source=root/'source';source.mkdir()
            for name in ('ViewA','ViewB','DefaultView'):(source/(name+'.cs.txt')).write_text('// '+name)
            target=fixture(host,source);business=target/'A/ViewA.cs';business.write_text('// user edit')
            with self.assertRaises(ValueError):fixture(host,source)
            self.assertEqual('// user edit',business.read_text())
    def test_usage_metadata_exists_before_read_only_verification(self):
        import tempfile
        from pathlib import Path
        from codegen import fixture,usage,tree
        with tempfile.TemporaryDirectory() as directory:
            root=Path(directory);source=root/'source';source.mkdir()
            for name in ('ViewA','ViewB','DefaultView'):(source/(name+'.cs.txt')).write_text('// '+name)
            (source/'BindingUsageProbe.cs.txt').write_text('public bool BindingReady()=>true;')
            target=fixture(root/'host',source);usage(target,source);before=tree(target);usage(target,source)
            self.assertEqual(before,tree(target))
            self.assertEqual(3,len(list(target.rglob('*.Usage.cs.meta'))))
