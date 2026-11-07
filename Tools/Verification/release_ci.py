"""Self-hosted CI orchestration; never treats a missing SDK/device as green."""
import json,os,subprocess,sys
from pathlib import Path
import run

def cli(*args):
    return subprocess.run([sys.executable,str(run.C/'run.py'),*args],cwd=run.R).returncode

def assess_install_record(record,folder,phase,current_fingerprint):
    from build import verify_files
    errors=verify_files(folder,record)
    if record.get('status')!='BLOCKED' or record.get('install_status')!='PASS' or not record.get('generation_compiled'):errors.append('install-not-complete')
    if record.get('phase','phase1')!=phase:errors.append('install-phase')
    if record.get('fingerprint')!=current_fingerprint or record.get('source_after')!=current_fingerprint:errors.append('install-fingerprint')
    if record.get('errors')!=['prepare-only; no mobile Player built']:errors.append('unexpected-install-blocker')
    return errors

def uikit05():
    """Mobile continuation of an independently verified, matching full Editor run."""
    import release_gate
    errors=release_gate.assess_editor_artifact(os.environ.get('UIKIT_EDITOR_ARTIFACT',''))
    if errors:
        print('BLOCKED full Editor/Reload/G0 artifact',errors);return 1
    failures=[]
    target='all' if os.environ.get('UIKIT_RELEASE_SCOPE','release')=='release' else 'Android'
    from build import latest_attempt,source_fingerprint
    for backend in ('legacy','input'):
        cli('build','--phase','uikit05','--editor','2022.3.62f2','--backend',backend,'--target','Android','--profile','smoke','--prepare-only')
        folder=latest_attempt(run.A/'install-checks','2022.3.62f2',backend,'Android','smoke','uikit05')
        try:
            record=json.loads((folder/'summary.json').read_text())
            if assess_install_record(record,folder,'uikit05',source_fingerprint()):failures.append('Git install:'+backend)
        except (OSError,ValueError,TypeError):failures.append('missing Git install:'+backend)
        for profile in ('smoke','clean'):
            if cli('build','--phase','uikit05','--editor','2022.3.62f2','--backend',backend,'--target',target,'--profile',profile):
                failures.append('build:'+backend+'/'+profile)
    # Physical taps remain required. Never synthesize TouchClose in CI.
    from build import latest_attempt
    for ver,backend,platform in release_gate.combinations():
        if target=='Android' and platform!='Android':continue
        folder=latest_attempt(run.A/'mobile',ver,backend,platform,'smoke','uikit05')
        device=os.environ.get('UIKIT_ANDROID_DEVICE' if platform=='Android' else 'UIKIT_IOS_DEVICE')
        if not folder or not device:failures.append('missing-build-or-device:'+backend+'/'+platform);continue
        if cli('smoke','--build',str(folder),'--device',device,'--timeout','900'):failures.append('smoke:'+backend+'/'+platform)
    if target=='all' and cli('gate','--name','release','--phase','uikit05'):failures.append('release')
    print('CI FAILURES',failures)
    return int(bool(failures))

def main():
    failures=[]
    phase=os.environ.get('UIKIT_ACCEPTANCE_PHASE','phase1')
    if phase=='uikit05':return uikit05()
    if phase not in ('phase1','phase2'):raise ValueError('Unknown acceptance phase')
    tools=run.A/'phase2/ci-tools.log';tools.parent.mkdir(parents=True,exist_ok=True)
    with tools.open('w') as log:
        if subprocess.call([sys.executable,'-m','unittest','discover','-s','Tools/Verification','-p','test_*.py'],cwd=run.R,stdout=log,stderr=subprocess.STDOUT):failures.append('Python verifier regression')
    steps=[('prepare','--phase','runtime'),('codegen',),('prepare-samples',),('check-generated',),('compile-probe',),('tests','--mode','EditMode','--suite','all'),('tests','--mode','PlayMode','--suite','all'),('play-reset',)]
    for step in steps:
        # The negative contract compilation probe uses one legacy host per version.
        backend='legacy' if step[0]=='compile-probe' else 'all'
        if cli(*step,'--editor','all','--backend',backend):failures.append(' '.join(step))
    if cli('gate','--name','preflight'):failures.append('G0')
    # Prepare-only deliberately returns nonzero. Require all four actual install records instead of ignoring the result.
    cli('build','--phase',phase,'--editor','all','--backend','all','--target','Android','--profile','smoke','--prepare-only')
    from build import latest_attempt,source_fingerprint,verify_files
    for ver in __import__("editor_targets").editors("all"):
        for backend in ('legacy','input'):
            folder=latest_attempt(run.A/'install-checks',ver,backend,'Android','smoke',phase)
            try:
                record=json.loads((folder/'summary.json').read_text())
                errors=assess_install_record(record,folder,phase,source_fingerprint())
                if errors:failures.append('Git install:'+ver+'/'+backend)
            except (OSError,ValueError,TypeError):failures.append('missing Git install:'+ver+'/'+backend)
    if cli('build','--phase',phase,'--editor','all','--backend','input','--target','Android','--profile','clean'):failures.append('clean Android')
    target='all' if os.environ.get('UIKIT_RELEASE_SCOPE','release')=='release' else 'Android'
    if cli('build','--phase',phase,'--editor','all','--backend','all','--target',target,'--profile','smoke'):failures.append('mobile builds')
    for ver,b,t in __import__('release_gate').combinations():
        if target=='Android' and t!='Android':continue
        folder=latest_attempt(run.A/'mobile',ver,b,t,'smoke',phase)
        paths=[] if folder is None else [folder/'request.json']
        device=os.environ.get('UIKIT_ANDROID_DEVICE' if t=='Android' else 'UIKIT_IOS_DEVICE')
        if not paths or not device:failures.append('missing-device-or-build:'+ver+'/'+b+'/'+t);continue
        if cli('smoke','--build',str(paths[-1].parent),'--device',device,'--timeout','900'):failures.append('smoke:'+ver+'/'+b+'/'+t)
    if target=='all' and cli('gate','--name','release','--phase',phase):failures.append('release')
    print('CI FAILURES',failures)
    return int(bool(failures))
if __name__=='__main__':sys.exit(main())
