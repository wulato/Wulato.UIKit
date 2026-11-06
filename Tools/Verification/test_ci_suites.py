import unittest
import contextlib,io,json,tempfile
from pathlib import Path
from unittest.mock import patch
from ci_suites import select_suites
class ModuleSelectionTests(unittest.TestCase):
    def test_page_navigation_source_selects_navigation_and_real_flow(self):
        import ci_suites
        for path in ('Packages/com.wulato.uikit/Runtime/Navigation/UINavigator.cs',
                     'Packages/com.wulato.uikit/Tests/Navigation/EditMode/PageNavigationTests.cs'):
            for suffix in ('','.meta'):
                chosen=ci_suites.plan([path+suffix]);self.assertEqual({'navigation','flow-integration'},{r['suite'] for r in chosen['tests']})
                self.assertNotIn('all',{r['suite'] for r in chosen['tests']})
    def test_context_api_tests_select_both_owned_categories(self):
        import ci_suites
        for suffix in ('','.meta'):
            chosen=ci_suites.plan(['Packages/com.wulato.uikit/Tests/EditMode/ContextApiTests.cs'+suffix])
            self.assertEqual({'contracts','integration'},{r['suite'] for r in chosen['tests']})
            self.assertFalse(chosen['reload'])
    def test_resource_router_and_bootstrap_select_targeted_contracts_and_reload(self):
        import ci_suites
        for path in ('Packages/com.wulato.uikit/Runtime/Loading/UIResourceResolver.cs',
                     'Packages/com.wulato.uikit/Runtime/Integrations/Addressables/AddressablesUIBootstrap.cs'):
            for suffix in ('','.meta'):
                p=ci_suites.plan([path+suffix]);suites={r['suite'] for r in p['tests']}
                self.assertIn('addressables-integration',suites)
                self.assertTrue(any(r['scope']=='integration' for r in p['reload']))
                if '/Loading/' in path:self.assertTrue({'contracts','integration','loading-layout','cleanup'}<=suites)
                self.assertNotIn('all',suites)
        direct=ci_suites.plan(['Packages/com.wulato.uikit/Tests/EditMode/ResourceResolverTests.cs'])
        self.assertEqual({'contracts'},{r['suite'] for r in direct['tests']})
    def test_integrated_optional_paths_keep_sdk_specific_scope(self):
        import ci_suites
        for section in ('Runtime','Editor','Tests'):
            for folder,suite in [('Addressables','addressables-runtime'),('InputSystem','input-runtime')]:
                p=ci_suites.plan([f'Packages/com.wulato.uikit/{section}/Integrations/{folder}/Probe.cs'])
                self.assertIn(suite,{r['suite'] for r in p['tests']})
                if folder=='InputSystem':self.assertEqual({'input'},{r['backend'] for r in p['tests']})
    def test_consolidated_runtime_metadata_keeps_boundary_suites_and_reload(self):
        import ci_suites
        for suffix in ('','.meta'):
            chosen=ci_suites.plan(['Packages/com.wulato.uikit/Runtime/Wulato.UIKit.asmdef'+suffix])
            suites={r['suite'] for r in chosen['tests']}
            self.assertTrue({'contracts','integration','editor','package-install','loading-layout','generated-integration','context-host','transitions','flow-integration','input-runtime'}<=suites)
            self.assertNotIn('all',suites)
            self.assertEqual({'legacy','input'},{r['backend'] for r in chosen['tests']})
            self.assertTrue(chosen['reload'])
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

class FrozenSelectionTests(unittest.TestCase):
    def setUp(self):
        import ci_suites
        self.temp=tempfile.TemporaryDirectory();self.addCleanup(self.temp.cleanup)
        self.root=Path(self.temp.name)
        for target,value in [('R',self.root),('binding',lambda:{'head':'fixed','source_fingerprint':'inputs','configuration':{'selector':'hash'}})]:
            mock=patch.object(ci_suites,target,value);mock.start();self.addCleanup(mock.stop)
    def freeze(self,chosen):
        import ci_suites
        path=self.root/'selection.json'
        with patch.object(ci_suites.subprocess,'check_output',return_value='base\n'):ci_suites.freeze(chosen,path,'BASE')
        return path
    def test_read_plan_ignores_generated_diffs_and_keeps_original_scope(self):
        import ci_suites
        chosen=ci_suites.plan(['Packages/com.wulato.uikit/Runtime/Navigation/UINavigator.cs'])
        path=self.freeze(chosen)
        with patch.object(ci_suites,'changed_paths',side_effect=AssertionError('must not rescan')),patch('sys.argv',['ci_suites','--read-plan',str(path),'--plan']),contextlib.redirect_stdout(io.StringIO()) as output:
            self.assertEqual(0,ci_suites.main())
        self.assertEqual(chosen,json.loads(output.getvalue()))
        self.assertEqual({'navigation','flow-integration'},{r['suite'] for r in chosen['tests']})
    def test_tampered_or_stale_plan_is_rejected(self):
        import ci_suites
        chosen=ci_suites.plan([]);path=self.freeze(chosen)
        with patch.object(ci_suites,'binding',return_value={'head':'different'}),self.assertRaisesRegex(ValueError,'stale'):
            ci_suites.load_plan(path)
        inputs=ci_suites.binding();inputs['source_fingerprint']='changed-without-commit'
        with patch.object(ci_suites,'binding',return_value=inputs),self.assertRaisesRegex(ValueError,'stale'):
            ci_suites.load_plan(path)
        data=json.loads(path.read_text());data['selection']['tests']=[{}];path.write_text(json.dumps(data))
        with self.assertRaisesRegex(ValueError,'hash-mismatch'):ci_suites.load_plan(path)
    def test_execute_passes_identical_host_and_scope_to_existing_runner(self):
        import ci_suites
        chosen=ci_suites.plan(['Packages/com.wulato.uikit/Runtime/UGUI/UIContextHost.cs'])
        with patch.object(ci_suites.subprocess,'call',return_value=0) as call,contextlib.redirect_stdout(io.StringIO()):
            self.assertEqual(0,ci_suites.execute(chosen,True));self.assertEqual(0,ci_suites.execute(chosen))
        calls=[c.args[0] for c in call.call_args_list]
        self.assertTrue(all(str(Path('Tools/Verification/run.py')) in cmd[1] for cmd in calls))
        self.assertTrue(all(cmd[cmd.index('--host-root')+1]==chosen['host_root'] for cmd in calls))
        with patch.object(ci_suites,'command_evidence',return_value=[]):self.assertEqual(0,ci_suites.check_evidence(chosen))
        (self.root/ci_suites.CI_DIR/'test-timings.json').unlink()
        self.assertEqual(1,ci_suites.check_evidence(chosen))
    def test_failure_stops_execution_and_preserves_logs_and_partial_timings(self):
        import ci_suites
        chosen=ci_suites.plan(['Packages/com.wulato.uikit/Runtime/Navigation/UINavigator.cs'])
        with patch.object(ci_suites.subprocess,'call',side_effect=[0,1]) as call,contextlib.redirect_stdout(io.StringIO()):
            self.assertEqual(1,ci_suites.execute(chosen,True))
        report=json.loads((self.root/ci_suites.CI_DIR/'prepare-timings.json').read_text())
        self.assertEqual('failed',report['state']);self.assertEqual(2,call.call_count)
        self.assertEqual([0,1],[r['exit_code'] for r in report['runs']])
        self.assertTrue(all((self.root/r['log']).is_file() for r in report['runs']))
        self.assertEqual(1,ci_suites.check_evidence(chosen))
    def test_python_only_evidence_must_include_success_status_and_log(self):
        import ci_suites
        chosen=ci_suites.plan(['Tools/Verification/ci_suites.py']);out=self.root/ci_suites.CI_DIR;out.mkdir(parents=True)
        self.assertEqual(1,ci_suites.check_evidence(chosen))
        (out/'python.log').write_text('tests passed');(out/'python-status.json').write_text('{"exit_code":0}')
        self.assertEqual(0,ci_suites.check_evidence(chosen))
        (out/'python-status.json').write_text('{"exit_code":1}');self.assertEqual(1,ci_suites.check_evidence(chosen))

class CoverageAndPreparationTests(unittest.TestCase):
    def test_upload_rejects_missing_or_tampered_raw_command_evidence(self):
        import ci_suites,run,hashlib
        with tempfile.TemporaryDirectory() as folder:
            root=Path(folder);batch=root/'attempt';host=batch/'u2022-legacy';host.mkdir(parents=True)
            raw=host/'raw.log';raw.write_text('original')
            record=dict(status='PASS',editor='2022.3.62f2',backend='legacy',files={'raw.log':hashlib.sha256(raw.read_bytes()).hexdigest()})
            (host/'summary.json').write_text(json.dumps(record));(batch/'summary.json').write_text(json.dumps([record]))
            log=root/'command.log';log.write_text('EVIDENCE '+str(batch)+'\n')
            chosen=ci_suites.plan([]);row=dict(command='codegen',editor='2022.3.62f2',backend='legacy')
            with patch.object(run,'A',root):
                self.assertEqual([],ci_suites.command_evidence(row,log,chosen))
                raw.write_text('changed');self.assertTrue(ci_suites.command_evidence(row,log,chosen))
                raw.unlink();self.assertTrue(ci_suites.command_evidence(row,log,chosen))
                log.write_text('no evidence');self.assertTrue(ci_suites.command_evidence(row,log,chosen))
    def test_changed_test_files_select_their_own_suite(self):
        import ci_suites
        for file,suite in [('InputArbitrationTests','input-runtime'),('TransitionTests','transitions'),('GeneratedRegistryIntegrationTests','generated-integration'),('LoadingTests','loading-layout')]:
            with self.subTest(file=file):
                rows=ci_suites.plan([f'Packages/com.wulato.uikit/Tests/PlayMode/{file}.cs.meta'])['tests']
                self.assertEqual({suite},{r['suite'] for r in rows})
    def test_playmode_test_assembly_metadata_selects_its_owned_suites(self):
        import ci_suites
        p=ci_suites.plan(['Packages/com.wulato.uikit/Tests/PlayMode/Wulato.UIKit.Tests.PlayMode.asmdef.meta'])
        self.assertTrue({'input-runtime','transitions','loading-layout','generated-integration','context-host','preview-runtime'}.issubset({r['suite'] for r in p['tests']}))
        self.assertEqual({'legacy','input'},{r['backend'] for r in p['tests']})
    def test_all_categorized_main_package_tests_select_their_category(self):
        import ci_suites,re
        root=Path(__file__).resolve().parents[2]
        for file in (root/'Packages/com.wulato.uikit/Tests').rglob('*.cs'):
            categories=set(re.findall(r'Category\("([a-z_]+)"\)',file.read_text()))
            if not categories:continue
            suites={r['suite'].replace('-','_') for r in ci_suites.plan([file.relative_to(root).as_posix()])['tests']}
            with self.subTest(file=file.name):self.assertTrue(categories.issubset(suites),(categories,suites))
    def test_host_preview_and_workbench_cover_two_backends_and_reload_scopes(self):
        import ci_suites
        for path,scope in [('Runtime/UGUI/UIContextHost.cs','context-host'),('Editor/Authoring/UIPreviewController.cs','preview'),('Editor/Authoring/UIAuthoringWorkbench.cs','preview')]:
            with self.subTest(path=path):
                p=ci_suites.plan(['Packages/com.wulato.uikit/'+path])
                self.assertEqual({'legacy','input'},{r['backend'] for r in p['tests']})
                self.assertEqual({('legacy',scope),('input',scope)},{(r['backend'],r['scope']) for r in p['reload']})
    def test_navigation_avoids_unrelated_sdk_samples_and_codegen(self):
        import ci_suites
        p=ci_suites.plan(['Packages/com.wulato.uikit/Runtime/Navigation/UINavigator.cs'])
        self.assertEqual(['prepare','compile-probe'],[r['command'] for r in p['prepare']])
        self.assertEqual('main',p['prepare'][0]['package_set'])
    def test_generated_integration_needs_codegen_but_no_sample_import(self):
        import ci_suites
        p=ci_suites.plan(['Packages/com.wulato.uikit/Tests/PlayMode/GeneratedRegistryIntegrationTests.cs'])
        self.assertEqual(['prepare','codegen','check-generated'],[r['command'] for r in p['prepare']])
    def test_business_and_addressables_choose_distinct_sample_sets(self):
        import ci_suites
        for path,sample_set,package_set in [('Packages/com.wulato.uikit/Samples~/BusinessFlow/BusinessFlowDemo.cs','business','main'),('Packages/com.wulato.uikit.addressables/Runtime/AddressablesUIViewLoader.cs','addressables','all')]:
            p=ci_suites.plan([path]);self.assertEqual(sample_set,next(r for r in p['prepare'] if r['command']=='prepare-samples')['sample_set'])
            self.assertEqual(package_set,p['prepare'][0]['package_set'])
    def test_explicit_full_uses_configured_targets_and_all_reload_scopes(self):
        import ci_suites
        from editor_targets import TARGETS
        p=ci_suites.plan([],full=True)
        self.assertEqual(set(TARGETS),{r['editor'] for r in p['tests']});self.assertEqual('Artifacts/uikit/ci-selected/hosts',p['host_root'])
        self.assertEqual({'integration','context-host','preview'},{r['scope'] for r in p['reload']})
        self.assertEqual('gate',ci_suites.commands(p,False)[-1]['command'])
        self.assertTrue(p['tools'])
        command=ci_suites.command_args(ci_suites.commands(p,False)[-1],p)
        self.assertEqual(p['host_root'],command[command.index('--host-root')+1])
    def test_workflow_freezes_once_and_uploads_failure_evidence_without_host_libraries(self):
        text=(Path(__file__).resolve().parents[2]/'.github/workflows/uikit-checks.yml').read_text()
        self.assertEqual(1,text.count('--write-plan'))
        self.assertEqual(3,text.count('--read-plan'))
        for required in ('--check-evidence','python.log','python-status.json','if-no-files-found: error','ci_artifact.py export','if: always()'):
            self.assertIn(required,text)

class HostedPlannerTests(unittest.TestCase):
    def test_python_only_changes_do_not_require_a_native_runner(self):
        text=(Path(__file__).resolve().parents[2]/'.github/workflows/uikit-checks.yml').read_text()
        self.assertIn('runs-on: ubuntu-latest',text)
        self.assertIn("needs.plan.outputs.count != '0'",text)
        self.assertIn('uikit-05-editor',text)
        self.assertIn('github.event.pull_request.head.repo.full_name == github.repository',text)
        self.assertIn('persist-credentials: false',text)
        self.assertIn('ref: ${{ github.sha }}',text)
    def test_smoke_uses_actual_api_contract_scope(self):
        import ci_suites
        chosen=ci_suites.plan(['Packages/com.wulato.uikit/Tests/EditMode/ContextApiTests.cs'])
        self.assertEqual({'contracts','integration'},{r['suite'] for r in chosen['tests']})
        self.assertEqual({'legacy'},{r['backend'] for r in chosen['tests']})
        self.assertFalse(chosen['full'])
