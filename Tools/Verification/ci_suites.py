"""Select direct module regressions for PRs; full acceptance is an explicit workflow input."""
import argparse,json,os,subprocess,sys
from pathlib import Path
R=Path(__file__).resolve().parents[2]
MODULES={'navigation':'navigation','dialogs':'dialogs','presentation':'presentation','popup-queue':'popup-queue'}
def select_suites(paths):
    selected=set()
    def add(mode,suite,backend='all'):selected.add((mode,suite,backend))
    for path in paths:
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
            elif '/Runtime/Loading/' in path:add('PlayMode','loading-layout');add('PlayMode','cleanup')
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

def plan(paths,editor=None,backend='auto'):
    from editor_targets import DEFAULT,editors
    versions=editors(editor or DEFAULT)
    tests={};warnings=[];tools=False
    for original in paths:
        path=original
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
        if '/Tests/PlayMode/PreviewRuntimeTests' in path:selected=[('PlayMode','preview-runtime','all')]
        if '/Tests/EditMode/AuthoringTests' in path:selected=[('EditMode','authoring','all')]
        if path.endswith('.md') or '/Documentation~/' in path:continue
        known=path.endswith(('.cs','.asmdef','.meta','.json','.unity','.prefab','.asset','.txt'))
        if original.startswith(('Packages/com.wulato.uikit','Verification/')) and not known:warnings.append('Unclassified relevant path: '+original)
        for mode,suite,owner in selected:
            cross_backend=backend=='all' or (backend=='auto' and (path.endswith(('package.json','.asmdef')) or path.endswith(('/UIContracts.cs','/IUIContext.cs','/UIContextBuilder.cs'))))
            backs=['input'] if owner=='input' else (['legacy','input'] if cross_backend else [backend if backend!='auto' else 'legacy'])
            for ver in versions:
                for back in backs:
                    key=(ver,back,mode,suite);tests.setdefault(key,[]).append(original)
    rows=[dict(editor=v,backend=b,mode=m,suite=s,reason=sorted(set(reasons))) for (v,b,m,s),reasons in sorted(tests.items())]
    prepare=[]
    for ver,back in sorted({(r['editor'],r['backend']) for r in rows}):
        suites={r['suite'] for r in rows if (r['editor'],r['backend'])==(ver,back)}
        def add(command,**extra):prepare.append(dict(command=command,editor=ver,backend=back,reason=sorted(suites),**extra))
        add('prepare',phase='runtime')
        if suites & {'generated-integration','samples','flow-samples','flow-integration','quickstart-only','addressables-integration'}:
            add('codegen');add('prepare-samples');add('check-generated')
        if 'addressables-integration' in suites:add('prepare',phase='addressables')
        if suites & {'navigation','contracts'}:add('compile-probe')
    return dict(schema=1,paths=paths,tools=tools,warnings=warnings,tests=rows,prepare=prepare)

def main():
    from editor_targets import DEFAULT
    import time
    parser=argparse.ArgumentParser()
    parser.add_argument('--base',default='HEAD~1');parser.add_argument('--github-output')
    parser.add_argument('--editor',default=DEFAULT);parser.add_argument('--backend',choices=['auto','legacy','input','all'],default='auto')
    parser.add_argument('--plan',action='store_true');parser.add_argument('--prepare',action='store_true')
    args=parser.parse_args()
    try:chosen=plan(changed_paths(args.base),args.editor,args.backend)
    except (ValueError,subprocess.CalledProcessError) as e:parser.error(str(e))
    if args.github_output:
        with open(args.github_output,'a') as stream:
            stream.write('count='+str(len(chosen['tests']))+'\n')
            stream.write('tools='+str(chosen['tools']).lower()+'\n')
        print(json.dumps(chosen));return 0
    if args.plan:print(json.dumps(chosen,indent=2));return 0
    out=R/'Artifacts/uikit/ci-selected';out.mkdir(parents=True,exist_ok=True)
    failures=[];timings=[]
    commands=chosen['prepare'] if args.prepare else [dict(r,command='tests') for r in chosen['tests']]
    for row in commands:
        cmd=[sys.executable,str(R/'Tools/Verification/run.py'),row['command'],'--editor',row['editor'],'--backend',row['backend']]
        for key in ('mode','suite','phase'):
            if key in row:cmd+=['--'+key,row[key]]
        name='-'.join(str(row.get(k,'')) for k in ('command','editor','backend','mode','suite','phase'))
        log=out/(name+'.log');started=time.monotonic()
        with log.open('w') as stream:code=subprocess.call(cmd,cwd=R,stdout=stream,stderr=subprocess.STDOUT)
        timings.append(dict(command=cmd,seconds=time.monotonic()-started,exit_code=code,log=str(log)))
        print(name,'PASS' if code==0 else 'FAIL',log,flush=True)
        if code:failures.append(name);break
    (out/('prepare-timings.json' if args.prepare else 'test-timings.json')).write_text(json.dumps(dict(plan=chosen,runs=timings),indent=2)+'\n')
    return int(bool(failures))
if __name__=='__main__':sys.exit(main())
