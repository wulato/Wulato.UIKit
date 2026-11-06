"""Select direct module regressions for PRs; full acceptance is an explicit workflow input."""
import argparse,hashlib,json,os,re,subprocess,sys,time
from pathlib import Path
R=Path(__file__).resolve().parents[2]
MODULES={'navigation':'navigation','dialogs':'dialogs','presentation':'presentation','popup-queue':'popup-queue'}
TEST_GROUPS={
    ('EditMode','diagnostics'): 'DiagnosticsTests SnapshotDiagnosticsTests',
    ('PlayMode','controls-input'): 'RuntimeInputScenarios',
    ('PlayMode','input-runtime'): 'InputArbitrationTests LegacyInputArbitrationTests RuntimeInputIntents RuntimeInputScenarios InputEventSpy RawInput ForegroundTests',
    ('PlayMode','transitions'): 'TransitionTests NavigationTransactionTests TransactionBoundaryTests',
    ('PlayMode','generated-integration'): 'GeneratedRegistryIntegrationTests',
    ('PlayMode','loading-layout'): 'LoadingTests LayoutReadyTests ShellLayoutTests LoadingLayoutFixture',
    ('PlayMode','cleanup'): 'LateCleanupTests ShutdownTests CleanupRuntimeFixture',
    ('PlayMode','sessions'): 'SessionLifecycleTests ClosePolicyTests',
    ('PlayMode','integration'): 'RuntimeResetTests',
    ('EditMode','editor'): 'GeneratorTests RegistryGenerationTests EditorOnlyCodegenView',
    ('EditMode','contracts'): 'ContractTests RegistryTests SettingsTests ExternalAssetContractTests ResourceResolverTests ContextApiTests',
    ('EditMode','sessions'): 'QueueTests ScopeTests SingleConsumptionTests TypeErasedOpenTests',
    ('EditMode','cleanup'): 'CachePolicyTests BudgetTests',
    ('EditMode','integration'): 'BuilderTests DiagnosticsTests ContextApiTests',
}
CI_DIR=Path('Artifacts/uikit/ci-selected')
def select_suites(paths):
    selected=set()
    def add(mode,suite,backend='all'):selected.add((mode,suite,backend))
    for path in paths:
        for section in ('Runtime','Editor','Tests'):
            for folder,package in (('Addressables','addressables'),('InputSystem','input-system')):
                path=path.replace(f'Packages/com.wulato.uikit/{section}/Integrations/{folder}/',f'Packages/com.wulato.uikit.{package}/{section}/')
        if ('/Integrations/DOTween' in path or '/Samples~/DOTween' in path) and not path.endswith('.md'):
            add('PlayMode','dotween','legacy');add('EditMode','package-install','legacy');continue
        if '/Samples~/FancyScrollView' in path and not path.endswith('.md'):
            add('PlayMode','fancy-list','legacy');add('EditMode','package-install','legacy');continue
        if '/Samples~/R3' in path and not path.endswith('.md'):
            add('PlayMode','r3-binding','legacy');add('EditMode','package-install','legacy');continue
        if path.endswith('/PreviewRoutingTests.cs') or path.endswith('/PreviewRoutingTests.cs.meta'):
            add('PlayMode','preview-addressables','input');continue
        if '/Samples~/LocalInventory/' in path:path=path.replace('Packages/com.wulato.uikit/','Packages/com.wulato.uikit.addressables/',1)
        if path.removesuffix('.meta')=='Packages/com.wulato.uikit/Runtime/Wulato.UIKit.asmdef':
            for suite in ('contracts','integration','editor','package-install',*MODULES):add('EditMode',suite)
            for suite in ('loading-layout','generated-integration','context-host','transitions','flow-integration','input-runtime'):add('PlayMode',suite)
            continue
        for folder,name in (('Navigation','navigation'),('Presentation','presentation'),('Dialogs','dialogs'),('PopupQueue','popup-queue')):
            for section in ('Runtime','Tests','Documentation~'):
                path=path.replace(f'Packages/com.wulato.uikit/{section}/{folder}/',f'Packages/com.wulato.uikit.{name}/{section}/')
        if path.endswith('.md') or '/Documentation~/' in path:continue
        prefix='Packages/com.wulato.uikit.'
        if path.startswith(prefix):
            name=path[len(prefix):].split('/')[0]
            if name=='addressables':
                add('EditMode','addressables');add('PlayMode','addressables-runtime');add('PlayMode','addressables-integration')
            elif name in MODULES:
                add('EditMode',name);add('PlayMode','flow-integration')
                if '/Samples~/' in path:add('PlayMode','flow-samples')
            elif name=='input-system':add('EditMode','input-package','input');add('PlayMode','input-runtime','input');add('PlayMode','flow-integration','input')
        elif path.startswith('Packages/com.wulato.uikit/'):
            if '/Editor/' in path:add('EditMode','editor');add('PlayMode','generated-integration')
            elif '/Samples~/' in path:add('PlayMode','samples')
            elif '/Runtime/UGUI/Input/' in path:add('PlayMode','input-runtime');add('PlayMode','flow-integration')
            elif '/Transactions/' in path:add('PlayMode','transitions');add('EditMode','navigation');add('PlayMode','flow-integration')
            elif '/Runtime/Loading/' in path:
                add('PlayMode','loading-layout');add('PlayMode','cleanup')
                if path.removesuffix('.meta').endswith('/UIResourceResolver.cs'):add('EditMode','contracts');add('EditMode','integration')
            else:
                for suite in ('contracts','integration','sessions',*MODULES):add('EditMode',suite)
                for suite in ('sessions','cleanup','integration','flow-integration'):add('PlayMode',suite)
                if '/Runtime/UGUI/' in path:add('PlayMode','loading-layout');add('PlayMode','input-runtime')
        elif path.startswith('Verification/Packages/com.wulato.uikit.verification.addressables/'):
            add('EditMode','addressables');add('PlayMode','addressables-runtime');add('PlayMode','addressables-integration')
        elif path.startswith('Verification/Packages/com.wulato.uikit.acceptance.flows/'):
            add('PlayMode','flow-integration');add('PlayMode','flow-samples')
        elif path.startswith('Verification/'):
            add('EditMode','contracts');add('PlayMode','integration')
        if path.endswith(('/UIRegistry.cs','/UICodegenPipeline.cs','/UIRegistryGenerator.cs','/UIRegistryProviderGenerator.cs','/UIBindingMarker.cs','/UIContextBuilder.cs')) or '/Runtime/Loading/' in path or path=='Tools/Verification/addressables_content.py':
            add('EditMode','addressables');add('PlayMode','addressables-runtime');add('PlayMode','addressables-integration')
    return sorted(selected)
def changed_paths(base,root=R):
    paths=set()
    for args in (['diff','--name-only','-z',base,'HEAD'],['diff','--name-only','-z','HEAD'],['ls-files','--others','--exclude-standard','-z']):
        paths.update(p for p in subprocess.check_output(['git',*args],cwd=root).decode().split('\0') if p)
    return sorted(paths)

def plan(paths,editor=None,backend='auto',full=False):
    from editor_targets import DEFAULT,editors
    versions=editors(editor or ('all' if full else DEFAULT))
    tests={};warnings=[];tools=False
    for original in paths:
        path=original
        for section in ('Runtime','Editor','Tests'):
            for folder,package in (('Addressables','addressables'),('InputSystem','input-system')):
                path=path.replace(f'Packages/com.wulato.uikit/{section}/Integrations/{folder}/',f'Packages/com.wulato.uikit.{package}/{section}/')
        if '/Samples~/LocalInventory/' in path:path=path.replace('Packages/com.wulato.uikit/','Packages/com.wulato.uikit.addressables/',1)
        for folder,name in (('Navigation','navigation'),('Presentation','presentation'),('Dialogs','dialogs'),('PopupQueue','popup-queue')):
            for section in ('Runtime','Tests','Documentation~'):
                prefix=f'Packages/com.wulato.uikit/{section}/{folder}/'
                if path.startswith(prefix):path=f'Packages/com.wulato.uikit.{name}/{section}/'+path[len(prefix):]
        if '/Samples~/BusinessFlow/' in path:path=path.replace('com.wulato.uikit/','com.wulato.uikit.navigation/',1)
        selected=select_suites([path])
        if '/Editor/Authoring/' in path:
            selected=[('EditMode','authoring','all'),('EditMode','editor','all')]
        if path.startswith(('Tools/Verification/','.github/workflows/')):tools=True
        if '/Runtime/UGUI/UIContextHost' in path or '/Tests/PlayMode/ContextHost' in path:selected=[('PlayMode','context-host','all'),('PlayMode','input-runtime','all')]
        if '/Editor/Authoring/UIPreview' in path:selected=[('EditMode','preview-authoring','all'),('PlayMode','preview-runtime','all')]
        if '/Tests/Preview/' in path:selected=[('EditMode','preview-authoring','all'),('PlayMode','preview-runtime','all')]
        if '/Tests/PlayMode/PreviewRuntimeTests' in path:selected=[('PlayMode','preview-runtime','all'),('PlayMode','diagnostics','all')]
        if '/Tests/Preview/ContextHostResetFixture' in path:selected=[('PlayMode','context-host','all')]
        if '/Tests/EditMode/AuthoringTests' in path:selected=[('EditMode','authoring','all')]
        if '/Editor/Authoring/UIContextHost' in path:selected=[('EditMode','authoring','all'),('PlayMode','context-host','all'),('PlayMode','input-runtime','all')]
        if path.startswith('Packages/com.wulato.uikit/Tests/'):
            stem=Path(path.removesuffix('.meta')).stem
            direct=[(mode,suite,'all') for (mode,suite),names in TEST_GROUPS.items() if stem in names.split()]
            if direct:selected=direct
            if path.removesuffix('.meta').endswith('.asmdef') and any('/Tests/'+folder+'/' in path for folder in ('PlayMode','EditMode','Support')):
                modes=['PlayMode'] if '/Tests/PlayMode/' in path else ['EditMode'] if '/Tests/EditMode/' in path else ['EditMode','PlayMode']
                selected=[(mode,suite,'all') for mode,suite in TEST_GROUPS if mode in modes]
                if 'PlayMode' in modes:selected += [('PlayMode','context-host','all'),('PlayMode','preview-runtime','all')]
                if 'EditMode' in modes:selected.append(('EditMode','authoring','all'))
        if '/Editor/Diagnostics/' in path or '/Core/Diagnostics/' in path:selected += [('EditMode','diagnostics','all'),('PlayMode','diagnostics','all')]
        if '/Editor/Authoring/UIAuthoringWorkbench' in path:
            selected+= [('EditMode','preview-authoring','all'),('PlayMode','preview-runtime','all')]
        if '/Runtime/UGUI/UIContextHost' in path:selected.append(('EditMode','authoring','all'))
        if path.endswith('/UIPlayModeResetCheck.cs'):
            selected=[('PlayMode','integration','all'),('PlayMode','context-host','all'),('PlayMode','preview-runtime','all')]
        boundary=any(suite in ('context-host','preview-authoring','preview-runtime') for mode,suite,owner in selected)
        if path.endswith('.md') or '/Documentation~/' in path:continue
        known=path.endswith(('.cs','.asmdef','.meta','.json','.unity','.prefab','.asset','.txt'))
        if original.startswith(('Packages/com.wulato.uikit','Verification/')) and not known:warnings.append('Unclassified relevant path: '+original)
        for mode,suite,owner in selected:
            cross_backend=backend=='all' or (backend=='auto' and (boundary or suite=='input-runtime' or path.endswith(('package.json','.asmdef')) or path.endswith(('/UIContracts.cs','/IUIContext.cs','/UIContextBuilder.cs'))))
            backs=['input'] if owner=='input' else (['legacy','input'] if cross_backend else [backend if backend!='auto' else 'legacy'])
            for ver in versions:
                for back in backs:
                    key=(ver,back,mode,suite);tests.setdefault(key,[]).append(original)
    rows=[dict(editor=v,backend=b,mode=m,suite=s,reason=sorted(set(reasons))) for (v,b,m,s),reasons in sorted(tests.items())]
    if full:
        rows=[dict(editor=v,backend=b,mode=m,suite='all',reason=['explicit full Editor gate']) for v in versions for b in ('legacy','input') for m in ('EditMode','PlayMode')]
    prepare=[];reload=[]
    for ver,back in sorted({(r['editor'],r['backend']) for r in rows}):
        suites={r['suite'] for r in rows if (r['editor'],r['backend'])==(ver,back)}
        def add(command,**extra):prepare.append(dict(command=command,editor=ver,backend=back,reason=sorted(suites),**extra))
        addressables=full or bool(suites & {'addressables','addressables-runtime','addressables-integration'})
        package_set='all' if addressables else 'main'
        add('prepare',phase='runtime',package_set=package_set)
        needs_samples=full or bool(suites & {'samples','flow-samples','addressables-integration'})
        needs_codegen=needs_samples or bool(suites & {'generated-integration','preview-runtime'})
        if needs_codegen:add('codegen')
        if needs_samples:
            business=full or 'flow-samples' in suites
            sample_set='all' if business and addressables else 'business' if business else 'addressables' if addressables else 'base'
            add('prepare-samples',sample_set=sample_set)
        if needs_codegen:add('check-generated')
        if full or 'addressables-integration' in suites:add('prepare',phase='addressables',package_set=package_set)
        if full or suites & {'navigation','contracts'}:add('compile-probe')
        scopes=[]
        if full or 'context-host' in suites:scopes.append('context-host')
        if full or suites & {'preview-runtime','preview-authoring'}:scopes.append('preview')
        if full or any(p.removesuffix('.meta').endswith(('/RuntimeResetTests.cs','/UIPlayModeResetCheck.cs','/UIResourceResolver.cs','/AddressablesUIBootstrap.cs')) for p in paths):scopes.append('integration')
        for scope in scopes:reload.append(dict(command='play-reset',editor=ver,backend=back,scope=scope,reason=sorted(suites)))
    return dict(schema=2,paths=paths,tools=tools or full,warnings=warnings,tests=rows,prepare=prepare,reload=reload,full=full,host_root=None if full else str(CI_DIR/'hosts'))

def binding():
    from build import source_fingerprint
    names=('ci_suites.py','run.py','runtime.py','samples.py','editors.json','matrix.json')
    return dict(head=subprocess.check_output(['git','rev-parse','HEAD'],cwd=R,text=True).strip(),source_fingerprint=source_fingerprint(),configuration={n:hashlib.sha256((R/'Tools/Verification'/n).read_bytes()).hexdigest() for n in names})

def freeze(chosen,path,base):
    inputs=binding()
    if chosen['host_root']:
        namespace=hashlib.sha256(json.dumps(dict(binding=inputs,selection=chosen),sort_keys=True).encode()).hexdigest()[:16]
        chosen['host_root']=str(CI_DIR/'hosts'/namespace)
    data=dict(schema=1,binding=inputs,base=subprocess.check_output(['git','rev-parse',base],cwd=R,text=True).strip(),selection=chosen,
              execution={k:os.environ[k] for k in ('GITHUB_RUN_ID','GITHUB_RUN_ATTEMPT','GITHUB_SHA') if k in os.environ})
    data['sha256']=hashlib.sha256(json.dumps(data,sort_keys=True).encode()).hexdigest()
    path.parent.mkdir(parents=True,exist_ok=True);path.write_text(json.dumps(data,indent=2)+'\n')

def load_plan(path):
    data=json.loads(path.read_text());digest=data.pop('sha256',None)
    if hashlib.sha256(json.dumps(data,sort_keys=True).encode()).hexdigest()!=digest:raise ValueError('frozen-plan-hash-mismatch')
    if data.get('schema')!=1 or data.get('binding')!=binding() or data['selection'].get('schema')!=2:raise ValueError('stale-or-unsupported-frozen-plan')
    return data['selection']

def commands(chosen,prepare):
    if prepare:return chosen['prepare']
    rows=[dict(r,command='tests') for r in chosen['tests']]+chosen['reload']
    return rows+([dict(command='gate',name='preflight')] if chosen['full'] else [])

def command_args(row,chosen):
    cmd=[sys.executable,str(R/'Tools/Verification/run.py'),row['command']]
    for key in ('editor','backend','mode','suite','phase','package_set','sample_set','scope','name'):
        if key in row:cmd+=['--'+key.replace('_','-'),row[key]]
    if chosen['host_root'] and row['command']!='gate':cmd+=['--host-root',chosen['host_root']]
    return cmd

def execute(chosen,prepare=False):
    out=R/CI_DIR;out.mkdir(parents=True,exist_ok=True)
    timings=[];target=out/('prepare-timings.json' if prepare else 'test-timings.json')
    result=dict(plan=chosen,state='running',runs=timings);target.write_text(json.dumps(result,indent=2)+'\n')
    failed=False
    for index,row in enumerate(commands(chosen,prepare)):
        cmd=command_args(row,chosen)
        name=str(index)+'-'+row['command']+'-'+row.get('backend','')+'-'+row.get('suite',row.get('scope',''))
        log=out/(('prepare-' if prepare else 'test-')+name+'.log');started=time.monotonic()
        with log.open('w') as stream:
            try:code=subprocess.call(cmd,cwd=R,stdout=stream,stderr=subprocess.STDOUT)
            except OSError as error:stream.write(str(error)+'\n');code=1
        timings.append(dict(command=cmd,seconds=time.monotonic()-started,exit_code=code,log=str(log.relative_to(R))))
        failed=code!=0;result['state']='failed' if failed else 'running';target.write_text(json.dumps(result,indent=2)+'\n')
        print(name,'PASS' if code==0 else 'FAIL',log,flush=True)
        if failed:break
    if not failed:result['state']='completed';target.write_text(json.dumps(result,indent=2)+'\n')
    return int(failed)

def command_evidence(row,log,chosen):
    """Re-use runner evidence checks before upload, without launching Unity."""
    import run
    from build import verify_files
    from evidence import sha256,verify
    from play_reset import reload_options,validate_report,validate_automatic_reports
    errors=[]
    try:
        matches=re.findall(r'EVIDENCE\s+(.+)$',log.read_text(),re.M)
        if len(matches)!=1:raise ValueError('missing-or-ambiguous-evidence-path')
        batch=Path(matches[0].strip()).resolve()
        if not batch.is_relative_to(run.A.resolve()):raise ValueError('evidence-outside-artifacts')
        if row['command']=='gate':
            if json.loads(batch.read_text()).get('status')!='PASS':errors.append('gate-not-pass')
            return errors
        name='probe-summary.json' if row['command']=='compile-probe' else 'summary.json'
        records=json.loads((batch/name).read_text())
        expected=reload_options(row['scope'],row['backend']) if row['command']=='play-reset' else [None]
        if not isinstance(records,list) or len(records)!=len(expected):raise ValueError('incomplete-command-inventory')
        if row['command']=='play-reset' and sorted(r['option'] for r in records)!=expected:raise ValueError('incomplete-reload-options')
        host_name=run.VERSIONS[row['editor']][0]+'-'+row['backend']
        host=R/chosen['host_root']/host_name if chosen['host_root'] else run.V/'Projects'/host_name
        for record in records:
            if record.get('status')!='PASS' or (record.get('editor'),record.get('backend'))!=(row['editor'],row['backend']):raise ValueError('command-record-identity-or-status')
            folder=Path(record.get('evidence',str(batch/host_name))).resolve()
            if folder.parent!=batch:raise ValueError('command-record-path')
            # Compare the indexed copy as well as its raw payload, not just the aggregate status.
            if json.loads((folder/'summary.json').read_text())!=record:raise ValueError('command-summary-mismatch')
            if 'evidence_index_sha256' in record:
                if sha256(folder/'evidence-index.json')!=record['evidence_index_sha256']:raise ValueError('command-index-hash')
                index=json.loads((folder/'evidence-index.json').read_text());errors+=verify_files(folder,index)
            else:errors+=verify_files(folder,record)
            if row['command']=='tests':
                if (record.get('mode'),record.get('suite'))!=(row['mode'],row['suite']):raise ValueError('test-command-scope')
                snap=run.snapshot(row['editor'],row['backend'],host)
                matrix=run.applicable_suites(json.loads((run.C/'matrix.json').read_text())[row['editor']][row['backend']][row['mode']],snap['manifest'],dotween=__import__('dotween_sdk').integration_enabled(host),r3=__import__('r3_sdk').integration_enabled(host),fancy=__import__('fancy_sdk').integration_enabled(host))
                required=sum(matrix.values(),[]) if row['suite']=='all' else matrix[row['suite']]
                errors+=verify(record,folder,required,snap,acceptance=True)['errors']
            if row['command']=='play-reset':
                if record.get('scope')!=row['scope'] or record.get('fingerprint')!=run.fingerprint(host):raise ValueError('stale-or-wrong-reload-scope')
                errors+=validate_report(json.loads((folder/'play-reset.json').read_text()),record['option'])+validate_automatic_reports(folder)
    except (OSError,ValueError,KeyError,TypeError,IndexError) as error:errors.append('invalid-command-evidence:'+str(error))
    return errors

def check_evidence(chosen):
    out=R/CI_DIR;out.mkdir(parents=True,exist_ok=True);errors=[]
    if chosen['tools']:
        try:
            status=json.loads((out/'python-status.json').read_text()).get('exit_code')
            if not (out/'python.log').read_bytes() or type(status) is not int or status!=0:errors.append('failed-python-evidence')
        except (OSError,ValueError):errors.append('missing-python-evidence')
    for prepare in (True,False):
        expected=commands(chosen,prepare)
        if not expected:continue
        path=out/('prepare-timings.json' if prepare else 'test-timings.json')
        try:
            report=json.loads(path.read_text())
            if report['plan']!=chosen or report['state']!='completed' or len(report['runs'])!=len(expected):errors.append('incomplete-stage:'+path.name)
            for row,attempt in zip(expected,report['runs']):
                log=R/attempt['log']
                if attempt['command']!=command_args(row,chosen) or attempt['exit_code']!=0 or not log.resolve().is_relative_to(out.resolve()) or not log.is_file():errors.append('invalid-command-evidence:'+path.name)
                else:errors+=command_evidence(row,log,chosen)
        except (OSError,ValueError,KeyError,TypeError):errors.append('missing-or-invalid-stage:'+path.name)
    (out/'upload-check.json').write_text(json.dumps(dict(status='FAIL' if errors else 'PASS',errors=errors),indent=2)+'\n')
    return int(bool(errors))

def main():
    from editor_targets import DEFAULT
    parser=argparse.ArgumentParser()
    parser.add_argument('--base',default='HEAD~1');parser.add_argument('--github-output')
    parser.add_argument('--editor',default=DEFAULT);parser.add_argument('--backend',choices=['auto','legacy','input','all'],default='auto')
    parser.add_argument('--plan',action='store_true');parser.add_argument('--prepare',action='store_true')
    group=parser.add_mutually_exclusive_group();group.add_argument('--write-plan',type=Path);group.add_argument('--read-plan',type=Path)
    parser.add_argument('--full',action='store_true',default=os.environ.get('UIKIT_CI_FULL')=='true')
    parser.add_argument('--check-evidence',action='store_true')
    args=parser.parse_args()
    try:
        if args.read_plan:chosen=load_plan(args.read_plan)
        else:
            chosen=plan(changed_paths(args.base),('all' if args.full and args.editor==DEFAULT else args.editor),args.backend,args.full)
            if args.write_plan:freeze(chosen,args.write_plan,args.base)
    except (OSError,ValueError,KeyError,subprocess.CalledProcessError) as e:parser.error(str(e))
    if args.check_evidence:
        if not args.read_plan:parser.error('--check-evidence requires --read-plan')
        return check_evidence(chosen)
    if args.github_output:
        with open(args.github_output,'a') as stream:
            stream.write('count='+str(len(chosen['tests']))+'\n')
            stream.write('tools='+str(chosen['tools']).lower()+'\n')
        print(json.dumps(chosen));return 0
    if args.plan:print(json.dumps(chosen,indent=2));return 0
    if args.write_plan:return 0
    return execute(chosen,args.prepare)
if __name__=='__main__':sys.exit(main())
