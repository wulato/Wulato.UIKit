"""G0 requires both suites on every host, with the current source/config fingerprint."""
import datetime,json
from pathlib import Path

def assess(results,required,current):
 errors=[]
 if len(results)!=1:errors.append('missing-or-ambiguous-result')
 for r in results:
  if r['status']!='PASS':errors.append('suite-not-pass')
  if r['fingerprint']!=current:errors.append('stale-fingerprint')
  passed={c['name'].split('.')[-1] for c in r.get('cases',[]) if c['result']=='Passed'}
  for name in required-passed:errors.append('missing-passed-required:'+name)
 return dict(status='FAIL' if errors else 'PASS',errors=errors)

def preflight(root):
 from run import VERSIONS,V,A,C,fingerprint,snapshot,dump
 matrix=json.loads((C/'matrix.json').read_text());items=[]
 records=[]
 for p in A.glob('*/*/summary.json'):
  try:
   r=json.loads(p.read_text())
   if r.get('suite'):records.append((p.stat().st_mtime,r))
  except (ValueError,AttributeError):continue
 for ver,(short,_,_) in VERSIONS.items():
  for backend in ('legacy','input'):
   host=V/'Projects'/f'{short}-{backend}';current=fingerprint(host)
   snap=snapshot(ver,backend,host)
   expected='com.wulato.uikit.verification.'+('legacy' if backend=='legacy' else 'input-system')
   if expected not in snap['manifest']['dependencies']:snap['errors'].append('missing-p1-backend-package')
   if backend=='input' and 'com.unity.inputsystem' not in snap['manifest']['testables']:snap['errors'].append('missing-input-system-testables')
   for suite in ('init','input-preflight'):
    matches=sorted((m,r) for m,r in records if r['editor']==ver and r['backend']==backend and r['suite']==suite)
    latest=[matches[-1][1]] if matches else []
    check=assess(latest,set(matrix[ver][backend]['PlayMode'][suite]),current)
    check['errors']+=snap['errors']
    if check['errors']:check['status']='FAIL'
    check.update(editor=ver,backend=backend,suite=suite,result=latest,current_fingerprint=current);items.append(check)
 status='PASS' if all(i['status']=='PASS' for i in items) else 'FAIL'
 out=A/('g0-'+datetime.datetime.now().strftime('%Y%m%dT%H%M%S'))/'preflight-report.json'
 dump(out,dict(gate='G0',status=status,scope='P0/P1 prototypes only; T2+ not started',items=items))
 print('G0',status,'EVIDENCE',out)
 return int(status!='PASS')
