#!/usr/bin/env python3
"""Staged Unity verification runner: preflight and T2 runtime contracts."""
import time
import argparse, datetime, hashlib, json, os, plistlib, re, subprocess, sys, uuid
from pathlib import Path
from results import evaluate
R=Path(__file__).resolve().parents[2]; V=R/'Verification'; A=R/'Artifacts/uikit'; C=Path(__file__).parent
from editor_targets import PROFILES, DEFAULT, editors
VERSIONS={v:(p['host'],p['ugui'],p['testFramework']) for v,p in PROFILES.items()}
COMMON='com.wulato.uikit.verification'
from runtime import is_extension_package, EXTENSIONS
def dump(p,v): p.parent.mkdir(parents=True,exist_ok=True);p.write_text(json.dumps(v,indent=2,ensure_ascii=False)+'\n')
def selected(args):
 if getattr(args,"consumer_hosts",None) is not None:
  yield from args.consumer_hosts
  return
 for ver in editors(args.editor):
  short,_,_=VERSIONS[ver]
  for backend in ('legacy','input'):
    if args.backend in ('all',backend):
     original=V/'Projects'/f'{short}-{backend}'
     host_root=getattr(args,'host_root',None)
     host=isolated_host(args,ver,backend,original,host_root) if host_root else original
     yield ver,backend,host

def isolated_host(args,ver,backend,original,host_root):
 import shutil
 base=Path(host_root).resolve()
 if not base.is_relative_to((A/'ci-selected/hosts').resolve()):raise ValueError('CI host root must be under Artifacts/uikit/ci-selected/hosts')
 host=base/original.name
 if not host.exists():
  if args.command!='prepare' or args.phase!='runtime':raise ValueError('CI host requires runtime preparation:'+str(host))
  host.mkdir(parents=True);(host/'Assets').mkdir();(host/'Packages').mkdir()
  shutil.copytree(original/'ProjectSettings',host/'ProjectSettings')
  dump(host/'Packages/manifest.json',dict(dependencies={'com.unity.ugui':PROFILES[ver]['ugui']}))
  dump(host/'Packages/packages-lock.json',dict(dependencies={}))
 return host
def digest(files):
 h=hashlib.sha256()
 for p in sorted(set(files)):
  name=p.resolve().relative_to(R.resolve()).as_posix().encode();data=p.read_bytes()
  h.update(len(name).to_bytes(8,'big'));h.update(name);h.update(len(data).to_bytes(8,'big'));h.update(data)
 return h.hexdigest()
def package_sources(host):
 manifest=json.loads((host/'Packages/manifest.json').read_text());sources={}
 for name,value in manifest['dependencies'].items():
  if not (name.startswith(COMMON) or is_extension_package(name) or name in ('com.wulato.uikit','com.wulato.uikit.acceptance','com.wulato.uikit.acceptance.samples','com.wulato.uikit.acceptance.quickstart','com.wulato.uikit.acceptance.flows','com.wulato.uikit.input-system')):continue
  if not value.startswith('file:'):raise ValueError('verification-package-must-be-local:'+name)
  actual=(host/'Packages'/value[5:]).resolve();expected=((R/'Packages'/name) if name in ('com.wulato.uikit','com.wulato.uikit.input-system') or is_extension_package(name) else (V/'Packages'/name)).resolve()
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
 if (host/'AddressablesContent.json').is_file():files.append(host/'AddressablesContent.json')
 files+=list(C.glob('*.py'))+[C/'matrix.json']
 if (C/'editors.json').exists():files.append(C/'editors.json')
 return digest(files)
def snapshot(ver,backend,host):
 m=json.loads((host/'Packages/manifest.json').read_text());lock=json.loads((host/'Packages/packages-lock.json').read_text())
 setting=(host/'ProjectSettings/ProjectSettings.asset').read_text(); errors=[]
 actual=(host/'ProjectSettings/ProjectVersion.txt').read_text().splitlines()[0].split(': ')[1]
 if actual!=ver:errors.append('editor-version:'+actual)
 if not re.search(r'activeInputHandler: '+str(0 if backend=='legacy' else 1)+r'\b',setting):errors.append('active-input-handler')
 for name,wanted in [('com.unity.ugui',VERSIONS[ver][1]),('com.unity.test-framework',VERSIONS[ver][2])]+([('com.unity.inputsystem',PROFILES[ver]['inputSystem'])] if backend=='input' else []):
  if lock['dependencies'].get(name,{}).get('version')!=wanted:errors.append('resolved-version:'+name)
 if backend=='legacy' and 'com.unity.inputsystem' in lock['dependencies']: errors.append('legacy-installed-input-system')
 for name,value in m['dependencies'].items():
  if not (name.startswith(COMMON) or is_extension_package(name) or name in ('com.wulato.uikit','com.wulato.uikit.acceptance','com.wulato.uikit.acceptance.samples','com.wulato.uikit.acceptance.quickstart','com.wulato.uikit.acceptance.flows','com.wulato.uikit.input-system')):continue
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
  assemblies={definition['name']:definition.get('references',[]) for path in (package/'Runtime').rglob('*.asmdef') for definition in [json.loads(path.read_text())]}
  errors.extend(validate_main_package(json.loads((package/'package.json').read_text()),assemblies))
 try:
  sources={name:str(path) for name,path in package_sources(host).items()};fp=fingerprint(host)
  from runtime import validate_extension_package
  for name in EXTENSIONS:
   if name in sources:
    path=Path(sources[name]);definitions=list((path/'Runtime').glob('*.asmdef'))
    if len(definitions)!=1:errors.append('extension-runtime-definition:'+name)
    else:errors.extend(validate_extension_package(json.loads((path/'package.json').read_text()),json.loads(definitions[0].read_text()).get('references',[])))
 except (OSError,ValueError,KeyError) as e:sources={};fp=None;errors.append(str(e))
 return {'editor':ver,'backend':backend,'host':str(host),'manifest':m,'lock':lock,'settings':setting,'errors':errors,'sources':sources,'fingerprint':fp}
def verify_editor(exe,expected):
 if not exe.is_file():raise ValueError('missing-editor:'+str(exe))
 actual=plistlib.loads((exe.parent.parent/'Info.plist').read_bytes()).get('CFBundleVersion')
 if actual!=expected:raise ValueError('editor-override-mismatch: expected '+expected+', actual '+str(actual))
 return actual

def timing_report(seconds,log,command):
 return dict(command=command,editor_wall_seconds=seconds,reported_compile_ms=[float(v) for v in re.findall(r'compile time[=:]\s*([0-9.]+)\s*ms',log)],scope='editor wall time includes startup, compilation and requested work; compile samples are reported log observations, not isolated test duration')

def unity(args,ver,host,out,test=False):
 exe=Path(args.editor_path) if args.editor_path else Path('/Applications/Unity/Hub/Editor')/ver/'Unity.app/Contents/MacOS/Unity'
 verify_editor(exe,ver)
 cmd=[str(exe),'-batchmode','-projectPath',str(host),'-logFile',str(out/'editor.log')]
 if test:cmd+=['-runTests','-testPlatform',args.mode,'-testResults',str(out/'results.xml')]+([] if args.suite=='all' else ['-testCategory',args.suite.replace('-','_')])
 else:cmd+=['-quit','-executeMethod',getattr(args,'execute_method','Wulato.UIKit.Verification.Editor.FixtureBuilder.Create')]
 dump(out/'command.json',cmd);env=os.environ.copy();env['UIKIT_EVIDENCE_DIR']=str(out);env['UIKIT_EXPECTED_UGUI']=PROFILES[ver]['ugui'];started=time.perf_counter()
 try:
  result=subprocess.run(cmd,env=env,timeout=args.timeout,stdout=subprocess.PIPE,stderr=subprocess.STDOUT,text=True);rc=result.returncode;timeout=False
 except subprocess.TimeoutExpired as e:rc=-1;timeout=True
 log=(out/'editor.log').read_text(errors='replace') if (out/'editor.log').exists() else ''
 # Unity emits license serials even during tests. Retain technical evidence without license material.
 log=re.sub(r'(Serial number assigned to: ).*',r'\1[REDACTED]',log)
 if (out/'editor.log').exists():(out/'editor.log').write_text(log)
 dump(out/'timing.json',timing_report(time.perf_counter()-started,log,getattr(args,'command','tests' if test else 'prepare')))
 actual=re.search(r'(?:Unity Editor version:|Initialize engine version:)\s*(\d+\.\d+\.\w+)',log)
 return rc,timeout,log,actual.group(1) if actual else None

def applicable_suites(matrix,manifest,dotween=False,r3=False):
 optional={'samples','performance','performance-markers','flow-samples'}
 mapping={suite:package for package,suite in EXTENSIONS.items()}
 mapping.update({suite:'com.wulato.uikit' for suite in ('navigation','presentation','dialogs','popup-queue')})
 mapping['addressables']='com.unity.addressables'
 mapping['addressables-integration']='com.wulato.uikit.verification.addressables'
 mapping['addressables-runtime']='com.unity.addressables'
 mapping['input-package']='com.unity.inputsystem'
 mapping['addressables-preflight']='com.wulato.uikit.verification.addressables'
 mapping['flow-integration']='com.wulato.uikit.acceptance.flows'
 mapping['flow-samples']='com.wulato.uikit.acceptance.flows'
 mapping['quickstart-only']='com.wulato.uikit.acceptance.quickstart'
 return {name:cases for name,cases in matrix.items() if (name!='dotween' or dotween) and (name!='r3-binding' or r3) and (name not in optional or 'com.wulato.uikit.acceptance.samples' in manifest['dependencies']) and (name not in mapping or mapping[name] in manifest['dependencies'])}

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
    if backend=='input':deps['com.unity.inputsystem']=PROFILES[ver]['inputSystem']
    if args.phase in ('p1','runtime','addressables'):
     package=COMMON+('.legacy' if backend=='legacy' else '.input-system')
     deps[package]='file:../../../Packages/'+package;testables.append(package)
     if backend=='input':testables.append('com.unity.inputsystem')
    if args.phase in ('runtime','addressables'):
     from runtime import prepare_runtime_manifest
     current=json.loads((host/'Packages/manifest.json').read_text())
     current_lock=json.loads((host/'Packages/packages-lock.json').read_text())
     manifest=prepare_runtime_manifest(ver,backend,current,current_lock,getattr(args,'package_set','all'))
     if getattr(args,'host_root',None):
      canonical=V/'Projects'/f'{VERSIONS[ver][0]}-{backend}'/'Packages'
      manifest['dependencies']={name:('file:'+str((canonical/value[5:]).resolve()) if value.startswith('file:') and not Path(value[5:]).is_absolute() else value) for name,value in manifest['dependencies'].items()}
    else:manifest=dict(dependencies=deps,testables=testables)
    dump(host/'Packages/manifest.json',manifest)
   else:
    from dotween_sdk import integration_enabled
    matrix=applicable_suites(json.loads((C/'matrix.json').read_text())[ver][backend][mode],json.loads((host/'Packages/manifest.json').read_text()),dotween=integration_enabled(host),r3=__import__('r3_sdk').integration_enabled(host))
    if suite=='r3-binding' and 'r3-binding' not in matrix:raise ValueError('R3 binding suite requires WULATO_UIKIT_R3, imported R3 sample and the official SDK')
    if suite=='dotween' and 'dotween' not in matrix:raise ValueError('DOTween suite requires WULATO_UIKIT_DOTWEEN and the official SDK')
    required=matrix[suite] if suite!='all' else sum(matrix.values(),[])
    if suite in ('addressables-preflight','addressables-integration'):
     from addressables_content import verify_content
     content_errors=verify_content(host,ver)
     if content_errors:raise ValueError(str(content_errors))
    metadata['source_before']=fingerprint(host);dump(out/'run.json',metadata)
   if args.command=='prepare' and args.phase in ('runtime','addressables'):args.execute_method='Wulato.UIKit.Acceptance.Editor.T2HostProbe.Prepare'
   if args.command=='prepare' and args.phase=='addressables':args.execute_method='Wulato.UIKit.Verification.Addressables.Editor.AddressablesContent.Prepare'
   rc,to,log,actual=unity(args,ver,host,out,args.command=='tests')
   if args.command=='prepare' and args.phase=='addressables' and rc==0:
    from addressables_content import seal_content
    seal_content(host,out,ver)
   snap=snapshot(ver,backend,host);dump(out/'host-snapshot.json',snap)
   result=evaluate(out/'results.xml',log,required,rc,to) if args.command=='tests' else {
       'status':'PASS' if rc==0 and not to and not re.search(r'error CS\d+|Scripts have compiler errors',log) else 'FAIL','errors':([] if rc==0 and not to and not re.search(r'error CS\d+|Scripts have compiler errors',log) else ['prepare-editor-failed'])}
   if args.command=='tests' and metadata['source_before']!=snap['fingerprint']:
    snap['errors'].append('inputs-changed-during-run')
   if actual!=ver:snap['errors'].append('actual-editor:'+str(actual))
   if args.command=='prepare' and args.phase in ('runtime','addressables'):
    from runtime import validate_installed_report
    result['errors']+=validate_installed_report(ver,out)
   if args.command=='tests' and suite in ('addressables-preflight','addressables-integration'):
    result['errors']+=verify_content(host,ver)
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
  q=sub.add_parser(name);q.add_argument('--editor',choices=[*VERSIONS,'all'],default=DEFAULT);q.add_argument('--backend',choices=['legacy','input','all'],default='legacy');q.add_argument('--editor-path');q.add_argument('--timeout',type=int,default=600)
  if name=='prepare':
   q.add_argument('--phase',choices=['p0','p1','runtime','addressables'],required=True)
   q.add_argument('--package-set',choices=['main','addressables','all'],default='all')
  else:q.add_argument('--suite',required=True);q.add_argument('--mode',choices=['PlayMode','EditMode'],default='PlayMode')
 for name in ('codegen','check-generated','prepare-samples'):
  q=sub.add_parser(name);q.add_argument('--editor',choices=[*VERSIONS,'all'],default=DEFAULT);q.add_argument('--backend',choices=['legacy','input','all'],default='legacy');q.add_argument('--editor-path');q.add_argument('--timeout',type=int,default=600)
 q=sub.choices['prepare-samples'];q.add_argument('--quickstart-only',action='store_true',help='fresh isolated consumers, versioned QuickStart only, using the existing Unity runner and evidence verifier');q.add_argument('--red',action='store_true',help='import/compile only, before sample asset implementation');q.add_argument('--check-only',action='store_true',help='compile and validate existing imported sample assets without regeneration')
 q.add_argument('--sample-set',choices=['base','business','addressables','all'],default='all')
 q=sub.add_parser('play-reset');q.add_argument('--editor',choices=[*VERSIONS,'all'],default=DEFAULT);q.add_argument('--backend',choices=['legacy','input','all'],default='legacy');q.add_argument('--editor-path');q.add_argument('--timeout',type=int,default=600)
 sub.choices['play-reset'].add_argument('--scope',choices=['integration','context-host','preview'],default='integration')
 sub.choices['play-reset'].add_argument('--reload-option',type=int,choices=range(4))
 sub.choices['play-reset'].add_argument('--interactive',action='store_true')
 sub.choices['play-reset'].add_argument('--preview-action',choices=['window','compile','cancel'],default='window')
 q=sub.add_parser('compile-probe');q.add_argument('--editor',choices=[*VERSIONS,'all'],default=DEFAULT);q.add_argument('--backend',choices=['legacy','input','all'],default='legacy');q.add_argument('--editor-path');q.add_argument('--timeout',type=int,default=600)
 q=sub.add_parser('gate');q.add_argument('--name',choices=['preflight','release'],required=True);q.add_argument('--phase',choices=['phase1','phase2'],default='phase1')
 for name in ('build',):
  q=sub.add_parser(name);q.add_argument('--editor',choices=[*VERSIONS,'all'],required=True);q.add_argument('--backend',choices=['legacy','input','all'],required=True);q.add_argument('--target',choices=['Android','iOS','all'],required=True);q.add_argument('--profile',choices=['smoke','clean'],required=True);q.add_argument('--phase',choices=['phase1','phase2','phase3'],default='phase1');q.add_argument('--package-set',choices=['all','main','navigation','dialogs','presentation','popup-queue','addressables'],default='all');q.add_argument('--prepare-only',action='store_true');q.add_argument('--timeout',type=int,default=3600);q.add_argument('--editor-path')
 q=sub.add_parser('smoke');q.add_argument('--build',required=True);q.add_argument('--device',required=True);q.add_argument('--timeout',type=int,default=180)
 for name in ('prepare','tests','codegen','check-generated','prepare-samples','play-reset','compile-probe'):
  sub.choices[name].add_argument('--host-root')
 a=p.parse_args()
 if a.command=='build':
  from build import run_build
  return run_build(a)
 if a.command=='smoke':
  from smoke import smoke
  return smoke(a)
 if a.command=='prepare-samples':
  from samples import prepare
  return prepare(a)
 if a.command in ('codegen','check-generated'):
  from codegen import run_codegen
  return run_codegen(a)
 if a.command=='play-reset':
  from play_reset import run_reset
  return run_reset(a)
 if a.command=='compile-probe':
  from contract_probe import run_probe
  return run_probe(a)
 if a.command=='gate' and a.name=='release':
  from release_gate import release
  return release(R,a.phase)
 if a.command=='gate':
  from gates import preflight
  return preflight(R)
 return run(a)
if __name__=='__main__':sys.exit(main())
