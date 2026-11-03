import unittest
from ci_suites import select_suites
class ModuleSelectionTests(unittest.TestCase):
    def test_navigation_change_does_not_run_unrelated_modules_or_all(self):
        self.assertEqual([('EditMode','navigation','all'),('PlayMode','flow-integration','all')],select_suites(['Packages/com.wulato.uikit/Runtime/Navigation/UINavigator.cs']))
    def test_input_adapter_uses_only_input_hosts(self):
        selected=select_suites(['Packages/com.wulato.uikit.input-system/Runtime/InputSystemUIInputDriver.cs'])
        self.assertTrue(selected);self.assertTrue(all(backend=='input' for mode,suite,backend in selected))
    def test_document_only_change_does_not_launch_unity(self):
        self.assertEqual([],select_suites(['Packages/com.wulato.uikit/Documentation~/Navigation/index.md']))
    def test_transaction_change_requires_navigation_and_transition_regressions(self):
        self.assertEqual({'navigation','transitions','flow-integration'},{suite for mode,suite,backend in select_suites(['Packages/com.wulato.uikit/Runtime/Core/Transactions/UIContextEngine.Pages.cs'])})

    def test_input_adapter_metadata_change_includes_editmode_package_contract(self):
        self.assertIn(('EditMode','input-package','input'),select_suites(['Packages/com.wulato.uikit.input-system/package.json']))

class IncrementalPlanTests(unittest.TestCase):
    def test_authoring_has_own_suite_and_no_sample_preparation(self):
        import ci_suites
        plan=ci_suites.plan(['Packages/com.wulato.uikit/Editor/Authoring/UISceneAuthoring.cs'])
        self.assertIn('authoring',[r['suite'] for r in plan['tests']])
        self.assertEqual(['2022.3.62f2'],sorted({r['editor'] for r in plan['tests']}))
        self.assertNotIn('prepare-samples',[r['command'] for r in plan['prepare']])
        self.assertTrue(all(r['reason'] for r in plan['tests']))
    def test_new_module_path_selects_only_direct_regression(self):
        import ci_suites
        suites={r['suite'] for r in ci_suites.plan(['Packages/com.wulato.uikit/Runtime/Navigation/UINavigator.cs'])['tests']}
        self.assertEqual({'navigation','flow-integration'},suites)
    def test_unknown_relevant_path_is_reported(self):
        import ci_suites
        self.assertTrue(ci_suites.plan(['Packages/com.wulato.uikit/new.config'])['warnings'])
    def test_tools_only_change_does_not_start_unity(self):
        import ci_suites
        p=ci_suites.plan(['Tools/Verification/ci_suites.py'])
        self.assertTrue(p['tools']);self.assertEqual([],p['tests']);self.assertEqual([],p['prepare'])
    def test_input_only_and_explicit_compatibility_target(self):
        import ci_suites
        p=ci_suites.plan(['Packages/com.wulato.uikit.input-system/Runtime/InputSystemUIInputDriver.cs'],editor='6000.3.22f1')
        self.assertEqual({'input'},{r['backend'] for r in p['tests']})
        self.assertEqual({'6000.3.22f1'},{r['editor'] for r in p['tests']})
    def test_unknown_editor_is_not_silently_substituted(self):
        import ci_suites
        with self.assertRaisesRegex(ValueError,'editor-target'):ci_suites.plan([],editor='unknown')
    def test_changed_paths_includes_commits_index_worktree_and_untracked(self):
        import tempfile,subprocess
        from pathlib import Path
        import ci_suites
        with tempfile.TemporaryDirectory() as d:
            def git(*args):return subprocess.check_output(['git',*args],cwd=d,text=True).strip()
            git('init','-q');git('config','user.email','test@example.invalid');git('config','user.name','Test')
            p=Path(d);(p/'base.cs').write_text('base');git('add','base.cs');git('commit','-qm','base');base=git('rev-parse','HEAD')
            (p/'committed.cs').write_text('committed');git('add','committed.cs');git('commit','-qm','next')
            (p/'staged.cs').write_text('staged');git('add','staged.cs');(p/'base.cs').write_text('working');(p/'new.cs').write_text('new')
            self.assertEqual({'base.cs','committed.cs','staged.cs','new.cs'},set(ci_suites.changed_paths(base,Path(d))))

class NewBoundarySelectionTests(unittest.TestCase):
    def test_preview_and_host_tests_do_not_expand_to_unrelated_core_suites(self):
        import ci_suites
        for path,wanted in [('Packages/com.wulato.uikit/Tests/Preview/PreviewAuthoringTests.cs',{'preview-authoring','preview-runtime'}),('Packages/com.wulato.uikit/Tests/PlayMode/ContextHostTests.cs',{'context-host','input-runtime'})]:
            self.assertEqual(wanted,{r['suite'] for r in ci_suites.plan([path])['tests']})
    def test_public_package_metadata_expands_backend_but_not_editor(self):
        import ci_suites
        rows=ci_suites.plan(['Packages/com.wulato.uikit/package.json'])['tests']
        self.assertEqual({'legacy','input'},{r['backend'] for r in rows})
        self.assertEqual({'2022.3.62f2'},{r['editor'] for r in rows})
