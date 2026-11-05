"""Real cross-assembly compiler probe: positive, expected CS0311, clean recovery."""
import datetime,json,re,shutil,uuid
from pathlib import Path

def assess(returncode,timed_out,log,negative):
    if timed_out:return False
    errors=re.findall(r'^.*error CS\d+.*$',log,re.M)
    if negative:
        return returncode!=0 and bool(errors) and all('error CS0311:' in line and 'Assets/UIKitContractProbe/ContractCompileProbe.cs' in line for line in errors)
    return returncode==0 and not errors and 'Scripts have compiler errors' not in log and 'UIKIT_CONTRACT_PROBE_OK' in log

def run_probe(args):
    import run
    from evidence import sha256
    from locking import guard,VerificationBusy
    try:
        with guard(run.A):
            batch=run.A/'contract-probes'/(datetime.datetime.now(datetime.timezone.utc).strftime('%Y%m%dT%H%M%S%f')+'-'+uuid.uuid4().hex[:6])
            summaries=[]
            for ver,backend,host in run.selected(args):
                out=batch/host.name;out.mkdir(parents=True)
                folder=host/'Assets/UIKitContractProbe'
                if folder.exists() or folder.with_suffix('.meta').exists():raise ValueError('probe-path-already-exists:'+str(folder))
                before=run.fingerprint(host)
                snapshot=run.snapshot(ver,backend,host);run.dump(out/'host-snapshot.json',snapshot)
                if snapshot['errors']:raise ValueError(str(snapshot['errors']))
                steps=[]
                try:
                    folder.mkdir()
                    navigation='com.wulato.uikit' in snapshot.get('manifest',{}).get('dependencies',{})
                    references=['Wulato.UIKit','UniTask']
                    run.dump(folder/'Wulato.UIKit.Acceptance.Compiler.asmdef',dict(name='Wulato.UIKit.Acceptance.Compiler',references=references,includePlatforms=['Editor']))
                    (folder/'ProbeEntry.cs').write_text('public static class UIKitProbeEntry { public static void Run() { UnityEngine.Debug.Log("UIKIT_CONTRACT_PROBE_OK"); } }\n')
                    source=(run.V/'Packages/com.wulato.uikit.acceptance/Tests/ContractCompileProbe.cs.txt').read_text()
                    if navigation:source='#define UIKIT_NAVIGATION_PROBE\n'+source
                    for name,negative in [('positive',False),('negative',True),('recovery',False)]:
                        step=out/name;step.mkdir()
                        target=folder/'ContractCompileProbe.cs';target.write_text(('#define UIKIT_NEGATIVE_PROBE\n' if negative else '')+source)
                        (step/'probe-source.cs.txt').write_text(target.read_text())
                        args.execute_method='UIKitProbeEntry.Run'
                        rc,to,log,actual=run.unity(args,ver,host,step)
                        passed=assess(rc,to,log,negative) and actual==ver
                        steps.append(dict(step=name,status='PASS' if passed else 'FAIL',returncode=rc,timed_out=to,actual_editor=actual))
                        print(steps[-1],flush=True)
                finally:
                    shutil.rmtree(folder)
                    folder.with_suffix('.meta').unlink(missing_ok=True)
                passed=len(steps)==3 and all(s['status']=='PASS' for s in steps) and run.fingerprint(host)==before
                result=dict(status='PASS' if passed else 'FAIL',editor=ver,backend=backend,fingerprint=before,steps=steps)
                result['files']={str(p.relative_to(out)):sha256(p) for p in out.rglob('*') if p.is_file()}
                run.dump(out/'summary.json',result);summaries.append(result)
            run.dump(batch/'probe-summary.json',summaries);print('EVIDENCE',batch,flush=True)
            return int(not summaries or any(s['status']!='PASS' for s in summaries))
    except (VerificationBusy,OSError,ValueError) as e:
        print('FAIL',e,flush=True);return 1
