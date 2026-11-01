"""Self-hosted CI orchestration; never treats a missing SDK/device as green."""
import json,os,subprocess,sys
from pathlib import Path
import run

def cli(*args):
    return subprocess.run([sys.executable,str(run.C/'run.py'),*args],cwd=run.R).returncode

def main():
    failures=[]
    steps=[('prepare','--phase','runtime'),('codegen',),('prepare-samples',),('check-generated',),('compile-probe',),('tests','--mode','EditMode','--suite','all'),('tests','--mode','PlayMode','--suite','all'),('play-reset',)]
    for step in steps:
        # The negative contract compilation probe uses one legacy host per version.
        backend='legacy' if step[0]=='compile-probe' else 'all'
        if cli(*step,'--editor','all','--backend',backend):failures.append(' '.join(step))
    if cli('gate','--name','preflight'):failures.append('G0')
    if cli('build','--editor','all','--backend','all','--target','Android','--profile','smoke','--prepare-only'):pass # install-only deliberately cannot mark a mobile build PASS
    if cli('build','--editor','all','--backend','input','--target','Android','--profile','clean'):failures.append('clean Android')
    target='all' if os.environ.get('UIKIT_RELEASE_SCOPE','release')=='release' else 'Android'
    if cli('build','--editor','all','--backend','all','--target',target,'--profile','smoke'):failures.append('mobile builds')
    for ver,b,t in __import__('release_gate').combinations():
        if target=='Android' and t!='Android':continue
        paths=sorted((run.A/'mobile').glob(f'*/{ver}-{b}-{t}-smoke/request.json'))
        device=os.environ.get('UIKIT_ANDROID_DEVICE' if t=='Android' else 'UIKIT_IOS_DEVICE')
        if not paths or not device:failures.append('missing-device-or-build:'+ver+'/'+b+'/'+t);continue
        if cli('smoke','--build',str(paths[-1].parent),'--device',device,'--timeout','900'):failures.append('smoke:'+ver+'/'+b+'/'+t)
    if target=='all' and cli('gate','--name','release'):failures.append('release')
    print('CI FAILURES',failures)
    return int(bool(failures))
if __name__=='__main__':sys.exit(main())
