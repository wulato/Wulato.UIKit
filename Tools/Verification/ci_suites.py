"""Select direct module regressions for PRs; full acceptance is an explicit workflow input."""
import argparse,json,os,subprocess,sys
from pathlib import Path
R=Path(__file__).resolve().parents[2]
MODULES={'navigation':'navigation','dialogs':'dialogs','presentation':'presentation','popup-queue':'popup-queue'}
def select_suites(paths):
    selected=set()
    def add(mode,suite,backend='all'):selected.add((mode,suite,backend))
    for path in paths:
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
def main():
    parser=argparse.ArgumentParser();parser.add_argument('--base',default='HEAD~1');parser.add_argument('--github-output');args=parser.parse_args()
    paths=subprocess.check_output(['git','diff','--name-only',args.base,'HEAD'],cwd=R,text=True).splitlines();selected=select_suites(paths)
    if args.github_output:
        with open(args.github_output,'a') as stream:
            stream.write('count='+str(len(selected))+'\n')
            stream.write('tools='+str(any(p.startswith(('Tools/Verification/','.github/workflows/')) for p in paths)).lower()+'\n')
            stream.write('content='+str(any(s=='addressables-integration' for m,s,b in selected)).lower()+'\n')
            stream.write('probe='+str(any(s in ('navigation','contracts') for m,s,b in selected)).lower()+'\n')
        print(json.dumps(selected));return 0
    out=R/'Artifacts/uikit/ci-selected';out.mkdir(parents=True,exist_ok=True)
    import run
    failures=[]
    for mode,suite,backend in selected:
        # Optional suites are only meaningful in hosts with their owning package installed.
        applicable=any(suite in run.applicable_suites(json.loads((run.C/'matrix.json').read_text())[ver][back][mode],json.loads((host/'Packages/manifest.json').read_text())) for ver,back,host in run.selected(argparse.Namespace(editor='all',backend=backend)))
        if not applicable:continue
        log=out/(mode+'-'+suite+'-'+backend+'.log')
        with log.open('w') as stream:code=subprocess.call([sys.executable,str(run.C/'run.py'),'tests','--editor','all','--backend',backend,'--mode',mode,'--suite',suite],cwd=R,stdout=stream,stderr=subprocess.STDOUT)
        print(mode,suite,backend,'PASS' if code==0 else 'FAIL',log,flush=True)
        if code:failures.append(suite)
    return int(bool(failures))
if __name__=='__main__':sys.exit(main())
