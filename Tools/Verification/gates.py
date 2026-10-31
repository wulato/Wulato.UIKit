"""G0 re-parses complete raw evidence from the latest registered attempt on every host."""
import datetime
import json
import uuid
from pathlib import Path
from evidence import verify
from registry import registered

def assess(results,required,current):
    errors=[]
    if len(results)!=1:errors.append('missing-or-ambiguous-result')
    for result in results:
        if result.get('status')!='PASS':errors.append('suite-not-pass')
        if result.get('fingerprint')!=current:errors.append('stale-fingerprint')
        passed={c['name'] for c in result.get('cases',[]) if c['result']=='Passed'}
        for name in required-passed:errors.append('missing-passed-required:'+name)
    return dict(status='FAIL' if errors else 'PASS',errors=errors)


def _preflight_locked(root):
    from run import VERSIONS,V,A,C,snapshot,dump
    matrix=json.loads((C/'matrix.json').read_text());items=[]
    records,registration_errors=registered(A)
    for ver,(short,_,_) in VERSIONS.items():
        for backend in ('legacy','input'):
            host=V/'Projects'/f'{short}-{backend}'
            try:
                current=snapshot(ver,backend,host)
                package='com.wulato.uikit.verification.'+('legacy' if backend=='legacy' else 'input-system')
                if package not in current['manifest']['dependencies']:current['errors'].append('missing-p1-backend-package')
                if backend=='input' and 'com.unity.inputsystem' not in current['manifest']['testables']:current['errors'].append('missing-input-system-testables')
            except (OSError,ValueError,KeyError) as e:
                current={'fingerprint':None,'errors':['invalid-current-host:'+str(e)]}
            for suite in ('init','input-preflight'):
                required=set(matrix[ver][backend]['PlayMode'][suite])
                matches=[r for r in records if r[2].get('editor')==ver and r[2].get('backend')==backend and r[2].get('mode')=='PlayMode' and r[2].get('suite') in (suite,'all')]
                latest=max(matches,key=lambda r:(r[0],r[1])) if matches else None
                selected=[];errors=list(current['errors'])+registration_errors;path=None
                if latest:
                    path=latest[3]
                    errors+=latest[2].get('registration_errors',[])
                    try:
                        result=json.loads((path/'summary.json').read_text());selected=[result]
                        if latest[2].get('state')!='completed':errors.append('latest-attempt-incomplete')
                        if result.get('run_id')!=latest[2]['run_id']:errors.append('registration-summary-mismatch')
                        errors+=verify(result,path,required,current)['errors']
                    except (OSError,ValueError,KeyError,TypeError) as e:errors.append('missing-latest-summary:'+str(e))
                check=assess(selected,required,current['fingerprint']);errors+=check['errors']
                items.append(dict(editor=ver,backend=backend,suite=suite,status='FAIL' if errors else 'PASS',errors=errors,
                                  result=selected,current_fingerprint=current['fingerprint'],evidence=str(path) if path else None))
    status='PASS' if all(i['status']=='PASS' for i in items) else 'FAIL'
    out=A/('g0-'+datetime.datetime.now(datetime.timezone.utc).strftime('%Y%m%dT%H%M%S%f')+'-'+uuid.uuid4().hex[:6])/'preflight-report.json'
    dump(out,dict(gate='G0',schema=3,status=status,scope='P0/P1 prototypes only; production contracts and later runtime acceptance are separate',items=items))
    print('G0',status,'EVIDENCE',out)
    return int(status!='PASS')


def preflight(root):
    from locking import guard,VerificationBusy
    from run import A,dump
    try:
        with guard(A,shared=True):return _preflight_locked(root)
    except VerificationBusy as e:
        out=A/('g0-busy-'+uuid.uuid4().hex)/'preflight-report.json'
        dump(out,dict(gate='G0',schema=3,status='FAIL',errors=[str(e)],items=[]))
        print('G0 FAIL',str(e),'EVIDENCE',out)
        return 1
