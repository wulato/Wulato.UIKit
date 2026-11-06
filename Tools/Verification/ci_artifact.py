"""Transport raw CI evidence without hosts/SDKs; re-parse it against a pinned source revision."""
import argparse,hashlib,json,os,shutil
from pathlib import Path
import build,evidence,run

def export(destination):
 destination=Path(destination).resolve()
 workspace=os.environ.get('GITHUB_WORKSPACE')
 if not workspace or Path(workspace).resolve()!=run.R.resolve():raise ValueError('export-requires-current-github-workspace')
 if not destination.is_relative_to(run.A.resolve()) or destination==run.A.resolve():raise ValueError('export-must-be-under-artifacts')
 if destination.exists():raise ValueError('refuse-overwriting-artifact')
 excluded={'Library','Temp','.git','consumer','git-source','hosts','sdk','runner','_work','_diag','.credentials','.credentials_rsaparams'}
 paths=[p for p in run.A.rglob('*') if p.is_file() and not p.is_symlink() and not p.resolve().is_relative_to(destination) and not (set(p.relative_to(run.A).parts)&excluded) and p.name!='.DS_Store']
 destination.mkdir(parents=True)
 index={}
 for p in paths:
  name=p.relative_to(run.A).as_posix();target=destination/name;target.parent.mkdir(parents=True,exist_ok=True);shutil.copyfile(p,target);index[name]=evidence.sha256(target)
 import subprocess
 manifest=dict(schema=1,revision=subprocess.check_output(['git','rev-parse','HEAD'],cwd=run.R,text=True).strip(),source_fingerprint=build.source_fingerprint(),origin=str(run.R.resolve()),package_version=json.loads((run.R/'Packages/com.wulato.uikit/package.json').read_text())['version'],run_id=os.environ.get('GITHUB_RUN_ID'),run_attempt=os.environ.get('GITHUB_RUN_ATTEMPT'),files=index)
 (destination/'artifact.json').write_text(json.dumps(manifest,indent=2)+'\n');return manifest

def verify(folder,revision,run_id):
 folder=Path(folder).resolve();manifest=json.loads((folder/'artifact.json').read_text());errors=[];groups=[]
 def check(ok,message):
  if not ok:errors.append(message)
 check(manifest.get('schema')==1,'artifact-schema');check(manifest.get('revision')==revision,'artifact-revision');check(manifest.get('run_id')==run_id,'artifact-run-id');check(manifest.get('package_version')=='0.5.0','artifact-package-version')
 check(manifest.get('source_fingerprint')==build.source_fingerprint(),'artifact-source-fingerprint')
 check(not any(p.is_symlink() for p in folder.rglob('*')),'artifact-symlink')
 if errors:return dict(status='FAIL',errors=errors,groups=groups)
 actual={p.relative_to(folder).as_posix() for p in folder.rglob('*') if p.is_file() and p!=folder/'artifact.json'}
 check(actual==set(manifest['files']),'artifact-file-set')
 for name,digest in manifest['files'].items():
  path=Path(name)
  if path.is_absolute() or '..' in path.parts:raise ValueError('unsafe-artifact-member')
  check(evidence.sha256(folder/path)==digest,'artifact-byte:'+name)
 check(not errors,'artifact-envelope')
 if errors:return dict(status='FAIL',errors=errors,groups=groups)
 selection=json.loads((folder/'ci-selected/selection.json').read_text());digest=selection.pop('sha256')
 check(hashlib.sha256(json.dumps(selection,sort_keys=True).encode()).hexdigest()==digest,'frozen-plan-hash')
 check(selection['binding']['head']==revision,'plan-revision');check(selection['binding']['source_fingerprint']==manifest['source_fingerprint'],'plan-source')
 chosen=selection['selection'];check(bool(chosen['tests']),'empty-native-selection');origin=Path(manifest['origin']);origin_a=origin/'Artifacts/uikit'
 check(json.loads((folder/'ci-selected/upload-check.json').read_text())['status']=='PASS','remote-current-source-upload-check')
 if chosen['tools']:check(json.loads((folder/'ci-selected/python-status.json').read_text())['exit_code']==0 and (folder/'ci-selected/python.log').stat().st_size>0,'python-result')
 matrix=json.loads((run.C/'matrix.json').read_text())
 from ci_suites import commands
 from play_reset import reload_options,validate_report,validate_automatic_reports
 for prepare in (True,False):
  expected=commands(chosen,prepare)
  if not expected:continue
  report=json.loads((folder/'ci-selected'/('prepare-timings.json' if prepare else 'test-timings.json')).read_text())
  check(report['plan']==chosen and report['state']=='completed' and len(report['runs'])==len(expected),'complete-command-inventory')
  for row,attempt in zip(expected,report['runs']):
   check(attempt['exit_code']==0,'command-exit')
   # Paths remain the original recorded paths. No JSON/log bytes are rewritten.
   import re
   log_path=Path(attempt['log'])
   if '..' in log_path.parts:raise ValueError('unsafe-command-log')
   log=folder/log_path.relative_to('Artifacts/uikit')
   matches=re.findall(r'EVIDENCE\s+(.+)$',log.read_text(),re.M);check(len(matches)==1,'command-evidence-path')
   original=Path(matches[0]);relative=original.relative_to(origin_a);batch=folder/relative
   if row['command']=='gate':check(json.loads(batch.read_text())['status']=='PASS','preflight-gate');continue
   records=json.loads((batch/('probe-summary.json' if row['command']=='compile-probe' else 'summary.json')).read_text())
   expected_options=reload_options(row['scope'],row['backend']) if row['command']=='play-reset' else [None]
   check(len(records)==len(expected_options),'command-host-inventory')
   if row['command']=='play-reset':check(sorted(r['option'] for r in records)==expected_options,'reload-options')
   for record in records:
    check(record['status']=='PASS','command-record-status')
    check((record.get('editor'),record.get('backend'))==(row['editor'],row['backend']),'command-record-host')
    if row['command']=='tests':
     recorded=Path(record['evidence']);f=folder/recorded.resolve().relative_to(origin_a);snapshot=json.loads((f/'host-snapshot.json').read_text())
     # Recompute every applicable required case from the recorded manifest and explicit SDK flags.
     check((record['mode'],record['suite'])==(row['mode'],row['suite']),'command-record-selector')
     symbols=re.search(r'^  scriptingDefineSymbols:\n((?:    [^\n]*\n)+)',snapshot['settings'],re.M)
     standalone=re.search(r'^    Standalone:([^\n]*)$',symbols.group(1),re.M) if symbols else None
     defines=set(standalone.group(1).strip().split(';')) if standalone else set()
     suites=run.applicable_suites(matrix[row['editor']][row['backend']][row['mode']],snapshot['manifest'],dotween='WULATO_UIKIT_DOTWEEN' in defines,r3='WULATO_UIKIT_R3' in defines,fancy='WULATO_UIKIT_FANCY_SCROLL' in defines,localization='WULATO_UIKIT_LOCALIZATION' in defines)
     required=sum(suites.values(),[]) if row['suite']=='all' else suites[row['suite']]
     result=evidence.verify(record,f,required,snapshot,acceptance=True,recorded_folder=recorded);errors+=result['errors']
     groups.append(dict(editor=row['editor'],backend=row['backend'],mode=row['mode'],suite=row['suite'],count=record['count']))
    if row['command']=='play-reset':
     f=folder/Path(record['evidence']).resolve().relative_to(origin_a)
     check(record.get('scope')==row['scope'],'reload-scope')
     check(evidence.sha256(f/'evidence-index.json')==record['evidence_index_sha256'],'reload-index')
     for name,value in json.loads((f/'evidence-index.json').read_text())['files'].items():check(evidence.sha256(f/name)==value,'reload-byte:'+name)
     errors+=validate_report(json.loads((f/'play-reset.json').read_text()),record['option'])+validate_automatic_reports(f)
 check(bool(groups),'zero-test-groups')
 return dict(status='FAIL' if errors else 'PASS',errors=errors,groups=groups,scope='offline raw evidence; remote upload-check attests the current host fingerprint, not a local re-execution')

def main():
 parser=argparse.ArgumentParser();sub=parser.add_subparsers(dest='command',required=True)
 p=sub.add_parser('export');p.add_argument('--destination',required=True)
 p=sub.add_parser('verify');p.add_argument('--folder',required=True);p.add_argument('--revision',required=True);p.add_argument('--run-id',required=True);p.add_argument('--report',required=True)
 args=parser.parse_args()
 if args.command=='export':export(args.destination);return 0
 try:report=verify(args.folder,args.revision,args.run_id)
 except (OSError,ValueError,KeyError,TypeError,IndexError) as error:report=dict(status='FAIL',errors=['invalid-artifact:'+str(error)])
 Path(args.report).write_text(json.dumps(report,indent=2)+'\n');print(json.dumps(report));return int(report['status']!='PASS')
if __name__=='__main__':raise SystemExit(main())
