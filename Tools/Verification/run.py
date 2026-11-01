#!/usr/bin/env python3
"""Staged Unity verification runner: preflight and T2 runtime contracts."""
import argparse, datetime, hashlib, json, os, plistlib, re, subprocess, sys, uuid
from pathlib import Path
from results import evaluate
R=Path(__file__).resolve().parents[2]; V=R/'Verification'; A=R/'Artifacts/uikit'; C=Path(__file__).parent
VERSIONS={'2022.3.62f2':('u2022','1.0.0','1.1.33'),'6000.3.22f1':('u6000','2.0.0','1.6.0')}
COMMON='com.wulato.uikit.verification'
def dump(p,v): p.parent.mkdir(parents=True,exist_ok=True);p.write_text(json.dumps(v,indent=2,ensure_ascii=False)+'\n')
def selected(args):
 for ver,(short,_,_) in VERSIONS.items():
  if args.editor not in ('all',ver):continue
  for backend in ('legacy','input'):
   if args.backend in ('all',backend):yield ver,backend,V/'Projects'/f'{short}-{backend}'
def digest(files):
 h=hashlib.sha256()
 for p in sorted(set(files)):
  name=p.resolve().relative_to(R.resolve()).as_posix().encode();data=p.read_bytes()
  h.update(len(name).to_bytes(8,'big'));h.update(name);h.update(len(data).to_bytes(8,'big'));h.update(data)
 return h.hexdigest()
def package_sources(host):
 manifest=json.loads((host/'Packages/manifest.json').read_text());sources={}
 for name,value in manifest['dependencies'].items():
  if not (name.startswith(COMMON) or name in ('com.wulato.uikit','com.wulato.uikit.acceptance','com.wulato.uikit.input-system')):continue
  if not value.startswith('file:'):raise ValueError('verification-package-must-be-local:'+name)
  actual=(host/'Packages'/value[5:]).resolve();expected=((R/'Packages'/name) if name in ('com.wulato.uikit','com.wulato.uikit.input-system') else (V/'Packages'/name)).resolve()
  if actual!=expected:raise ValueError('noncanonical-package-source:'+name)
  if json.loads((actual/'package.json').read_text())['name']!=name:raise ValueError('local-name:'+name)
  sources[name]=actual
 return sources

def fingerprint(host):
 files=[]
 for path in package_sources(host).values():
  files.extend(p for p in path.rglob('*') if p.is_file() and p.name!='.DS_Store')
  if path.with_suffix(path.suffix+'.meta').is_file():files.append(path.with_suffix(path.suffix+'.meta'))
 for folder in ('Assets','ProjectSettings','Packages'):
  files.extend(p for p in (host/folder).rglob('*') if p.is_file() and p.name!='.DS_Store')
 files+=list(C.glob('*.py'))+[C/'matrix.json']
 return digest(files)
def snapshot(ver,backend,host):
 m=json.loads((host/'Packages/manifest.json').read_text());lock=json.loads((host/'Packages/packages-lock.json').read_text())
 setting=(host/'ProjectSettings/ProjectSettings.asset').read_text(); errors=[]
 actual=(host/'ProjectSettings/ProjectVersion.txt').read_text().splitlines()[0].split(': ')[1]
 if actual!=ver:errors.append('editor-version:'+actual)
 if not re.search(r'activeInputHandler: '+str(0 if backend=='legacy' else 1)+r'\b',setting):errors.append('active-input-handler')
 for name,wanted in [('com.unity.ugui',VERSIONS[ver][1]),('com.unity.test-framework',VERSIONS[ver][2])]+([('com.unity.inputsystem','1.17.0')] if backend=='input' else []):
  if lock['dependencies'].get(name,{}).get('version')!=wanted:errors.append('resolved-version:'+name)
 if backend=='legacy' and 'com.unity.inputsystem' in lock['dependencies']: errors.append('legacy-installed-input-system')
 for name,value in m['dependencies'].items():
  if not (name.startswith(COMMON) or name in ('com.wulato.uikit','com.wulato.uikit.acceptance','com.wulato.uikit.input-system')):continue
  path=(host/'Packages'/value.removeprefix('file:')).resolve()
  if not (path/'package.json').is_file():errors.append('local-path:'+name);continue
  if json.loads((path/'package.json').read_text())['name']!=name:errors.append('local-name:'+name)
  if name not in m['testables']:errors.append('testables:'+name)
  entry=lock['dependencies'].get(name,{})
  if entry.get('version')!=value or entry.get('source')!='local':errors.append('lock-source:'+name)
 if 'com.wulato.uikit' in m['dependencies']:
  from runtime import validate_dependencies
  errors.extend(validate_dependencies(ver,m,lock))
  from runtime import validate_main_package
  package=R/'Packages/com.wulato.uikit'
  assemblies={name:json.loads((package/('Runtime/'+name)/('Wulato.UIKit.'+name+'.asmdef')).read_text()).get('references',[]) for name in ('Core','Loading','UGUI')}
  errors.extend(validate_main_package(json.loads((package/'package.json').read_text()),assemblies))
 try:
  sources={name:str(path) for name,path in package_sources(host).items()};fp=fingerprint(host)
 except (OSError,ValueError,KeyError) as e:sources={};fp=None;errors.append(str(e))
 return {'editor':ver,'backend':backend,'host':str(host),'manifest':m,'lock':lock,'settings':setting,'errors':errors,'sources':sources,'fingerprint':fp}
def verify_editor(exe,expected):
 if not exe.is_file():raise ValueError('missing-editor:'+str(exe))
 actual=plistlib.loads((exe.parent.parent/'Info.plist').read_bytes()).get('CFBundleVersion')
 if actual!=expected:raise ValueError('editor-override-mismatch: expected '+expected+', actual '+str(actual))
 return actual

def unity(args,ver,host,out,test=False):
 exe=Path(args.editor_path) if args.editor_path else Path('/Applications/Unity/Hub/Editor')/ver/'Unity.app/Contents/MacOS/Unity'
 verify_editor(exe,ver)
 cmd=[str(exe),'-batchmode','-projectPath',str(host),'-logFile',str(out/'editor.log')]
 if test:cmd+=['-runTests','-testPlatform',args.mode,'-testResults',str(out/'results.xml')]+([] if args.suite=='all' else ['-testCategory',args.suite.replace('-','_')])
 else:cmd+=['-quit','-executeMethod',getattr(args,'execute_method','Wulato.UIKit.Verification.Editor.FixtureBuilder.Create')]
 dump(out/'command.json',cmd);env=os.environ.copy();env['UIKIT_EVIDENCE_DIR']=str(out)
 try:
  result=subprocess.run(cmd,env=env,timeout=args.timeout,stdout=subprocess.PIPE,stderr=subprocess.STDOUT,text=True);rc=result.returncode;timeout=False
 except subprocess.TimeoutExpired as e:rc=-1;timeout=True
 log=(out/'editor.log').read_text(errors='replace') if (out/'editor.log').exists() else ''
 # Unity emits license serials even during tests. Retain technical evidence without license material.
 log=re.sub(r'(Serial number assigned to: ).*',r'\1[REDACTED]',log)
 if (out/'editor.log').exists():(out/'editor.log').write_text(log)
 actual=re.search(r'(?:Unity Editor version:|Initialize engine version:)\s*(\d+\.\d+\.\w+)',log)
 return rc,timeout,log,actual.group(1) if actual else None

def _run_locked(args):
 from evidence import seal,sha256
 from registry import RUN_SCHEMA
 batch=A/(datetime.datetime.now(datetime.timezone.utc).strftime('%Y%m%dT%H%M%S%f')+'-'+args.command+'-'+uuid.uuid4().hex[:6])
 batch.mkdir(parents=True)
 selections=list(selected(args))
 inventory=dict(schema=1,serialization='exclusive-batch',batch_id=batch.name,command=args.command,started_at=datetime.datetime.now(datetime.timezone.utc).isoformat(),
                attempts=[dict(run_id=batch.name+'/'+host.name,folder=host.name,editor=ver,backend=backend,
                    mode=args.mode if args.command=='tests' else None,suite=args.suite if args.command=='tests' else None) for ver,backend,host in selections])
 dump(batch/'batch.json',inventory);batch_hash=sha256(batch/'batch.json')
 summaries=[]
 for ver,backend,host in selections:
  out=batch/host.name;out.mkdir()
  mode=args.mode if args.command=='tests' else None
  suite=args.suite if args.command=='tests' else None
  metadata=dict(schema=RUN_SCHEMA,batch_id=batch.name,batch_sha256=batch_hash,run_id=batch.name+'/'+host.name,state='running',editor=ver,backend=backend,mode=mode,suite=suite,
                started_at=datetime.datetime.now(datetime.timezone.utc).isoformat(),source_before=None)
  dump(out/'run.json',metadata)
  print('START',args.command,suite or args.phase,host.name,flush=True)
  actual=None;rc=-1;to=False;snap=None
  try:
   exe=Path(args.editor_path) if args.editor_path else Path('/Applications/Unity/Hub/Editor')/ver/'Unity.app/Contents/MacOS/Unity'
   verify_editor(exe,ver)
   if args.command=='prepare':
    _,ugui,utf=VERSIONS[ver]
    deps={'com.unity.ugui':ugui,'com.unity.test-framework':utf,COMMON:'file:../../../Packages/'+COMMON,
          **{'com.unity.modules.'+x:'1.0.0' for x in ['ui','imgui','physics','physics2d','jsonserialize']}}
    testables=[COMMON]
    if backend=='input':deps['com.unity.inputsystem']='1.17.0'
    if args.phase in ('p1','runtime'):
     package=COMMON+('.legacy' if backend=='legacy' else '.input-system')
     deps[package]='file:../../../Packages/'+package;testables.append(package)
     if backend=='input':testables.append('com.unity.inputsystem')
    if args.phase=='runtime':
     from runtime import prepare_runtime_manifest
     current=json.loads((host/'Packages/manifest.json').read_text())
     current_lock=json.loads((host/'Packages/packages-lock.json').read_text())
     manifest=prepare_runtime_manifest(ver,backend,current,current_lock)
    else:manifest=dict(dependencies=deps,testables=testables)
    dump(host/'Packages/manifest.json',manifest)
   else:
    matrix=json.loads((C/'matrix.json').read_text())[ver][backend][mode]
    required=matrix[suite] if suite!='all' else sum(matrix.values(),[])
    metadata['source_before']=fingerprint(host);dump(out/'run.json',metadata)
   if args.command=='prepare' and args.phase=='runtime':args.execute_method='Wulato.UIKit.Acceptance.Editor.T2HostProbe.Prepare'
   rc,to,log,actual=unity(args,ver,host,out,args.command=='tests')
   snap=snapshot(ver,backend,host);dump(out/'host-snapshot.json',snap)
   result=evaluate(out/'results.xml',log,required,rc,to) if args.command=='tests' else {
       'status':'PASS' if rc==0 and not to and not re.search(r'error CS\d+|Scripts have compiler errors',log) else 'FAIL','errors':([] if rc==0 and not to and not re.search(r'error CS\d+|Scripts have compiler errors',log) else ['prepare-editor-failed'])}
   if args.command=='tests' and metadata['source_before']!=snap['fingerprint']:
    snap['errors'].append('inputs-changed-during-run')
   if actual!=ver:snap['errors'].append('actual-editor:'+str(actual))
   if args.command=='prepare' and args.phase=='runtime':
    from runtime import validate_installed_report
    result['errors']+=validate_installed_report(ver,out)
   result['errors']+=snap['errors']
   if result['errors']:result['status']='FAIL'
   traces={p.name:json.loads(p.read_text()) for p in out.glob('*.trace.json')};dump(out/'trace.json',traces)
   if args.command=='tests' and result['status']=='PASS' and not traces and mode=='PlayMode':
    result['status']='FAIL';result['errors'].append('missing-trace')
  except Exception as e:
   result={'status':'FAIL','errors':['runner-error:'+type(e).__name__+':'+str(e)],'cases':[],'count':0}
  metadata.update(state='completed',finished_at=datetime.datetime.now(datetime.timezone.utc).isoformat(),source_after=snap['fingerprint'] if snap else None,
                  returncode=rc,timed_out=to,actual_editor=actual)
  dump(out/'run.json',metadata)
  result.update(schema=RUN_SCHEMA,batch_id=batch.name,batch_sha256=batch_hash,run_id=metadata['run_id'],editor=ver,backend=backend,mode=mode,suite=suite,
                phase=args.phase if args.command=='prepare' else None,fingerprint=metadata['source_after'],evidence=str(out))
  result['evidence_index_sha256']=seal(out)
  dump(out/'summary.json',result);summaries.append(result)
  print(result['status'],host.name,result['errors'],flush=True)
 dump(batch/('init-report.json' if suite=='init' else 'summary.json'),summaries)
 print('EVIDENCE',batch,flush=True)
 return int(any(s['status']!='PASS' for s in summaries))

def run(args):
 from locking import guard,VerificationBusy
 try:
  with guard(A):return _run_locked(args)
 except VerificationBusy as e:
  print('FAIL',str(e),file=sys.stderr);return 1

def main():
 p=argparse.ArgumentParser();sub=p.add_subparsers(dest='command',required=True)
 for name in ('prepare','tests'):
  q=sub.add_parser(name);q.add_argument('--editor',choices=[*VERSIONS,'all'],default='all');q.add_argument('--backend',choices=['legacy','input','all'],default='all');q.add_argument('--editor-path');q.add_argument('--timeout',type=int,default=600)
  if name=='prepare':q.add_argument('--phase',choices=['p0','p1','runtime'],required=True)
  else:q.add_argument('--suite',required=True);q.add_argument('--mode',choices=['PlayMode','EditMode'],default='PlayMode')
 q=sub.add_parser('compile-probe');q.add_argument('--editor',choices=[*VERSIONS,'all'],default='all');q.add_argument('--backend',choices=['legacy','input','all'],default='legacy');q.add_argument('--editor-path');q.add_argument('--timeout',type=int,default=600)
 q=sub.add_parser('gate');q.add_argument('--name',choices=['preflight'],required=True)
 a=p.parse_args()
 if a.command=='compile-probe':
  from contract_probe import run_probe
  return run_probe(a)
 if a.command=='gate':
  from gates import preflight
  return preflight(R)
 return run(a)
if __name__=='__main__':sys.exit(main())
