"""Pinned Git consumer installation and staged ARM64/IL2CPP builds."""
import datetime,hashlib,json,os,re,shutil,subprocess,uuid
from pathlib import Path

def merge_player_report(result,report):
    merged=dict(result);merged.update({k:v for k,v in report.items() if k!='errors'})
    merged['build_error_count']=report.get('errors',0)
    return merged

def find_build_module(exe,target):
    name='AndroidPlayer' if target=='Android' else 'iOSSupport'
    for root in (exe.parents[3]/'PlaybackEngines',exe.parent.parent/'PlaybackEngines'):
        path=root/name
        if path.is_dir():return path
    return None

def write_matrix_report(batch,rows):
    import run
    run.dump(batch/'matrix-report.json',rows)

def build_identity(request):
    return hashlib.sha256(json.dumps({k:v for k,v in request.items() if k!='build_id'},sort_keys=True).encode()).hexdigest()

def latest_attempt(root,editor,backend,target,profile):
    found=[]
    for path in sorted(root.glob('*/inventory.json')):
        data=json.loads(path.read_text())
        if any((r.get('editor'),r.get('backend'),r.get('target'),r.get('profile'))==(editor,backend,target,profile) for r in data['combinations']):
            found.append(path.parent/f'{editor}-{backend}-{target}-{profile}')
    return found[-1] if found else None

def validate_build_report(raw,summary):
    return ['raw-build-report:'+key for key in ('status','build_id','output','application_id','architecture','scripting_backend','player_assemblies') if raw.get(key)!=summary.get(key)]

def source_fingerprint():
    import run
    files=[p for base in (run.R/'Packages/com.wulato.uikit',run.R/'Packages/com.wulato.uikit.input-system',run.V/'Packages') for p in base.rglob('*') if p.is_file() and p.name!='.DS_Store']
    files+=list((run.R/'.github/workflows').glob('uikit-*.yml'))+list(run.C.glob('*.py'))+[run.C/'matrix.json',run.R/'docs/superpowers/specs/2026-10-31-wulato-uikit-design.md']
    return run.digest(files)

def file_index(folder,exclude=()):
    from evidence import sha256
    return {str(p.relative_to(folder)):sha256(p) for p in (q for root in folder.iterdir() if root.name not in ('consumer','git-source') for q in (root.rglob('*') if root.is_dir() else [root])) if p.is_file() and str(p.relative_to(folder)) not in exclude and not p.is_symlink()}

def verify_files(folder,report,key='files'):
    from evidence import sha256
    errors=[]
    if not report.get(key):return ['missing-file-index']
    for name,digest in report[key].items():
        p=folder/name
        if not p.resolve().is_relative_to(folder.resolve()) or not p.is_file() or sha256(p)!=digest:errors.append('artifact-hash:'+name)
    return errors

def command(cmd,out,timeout=600,env=None):
    import run
    out.parent.mkdir(parents=True,exist_ok=True);run.dump(out.with_suffix('.command.json'),cmd)
    try:
        with out.open('w') as f:return subprocess.run(cmd,stdout=f,stderr=subprocess.STDOUT,timeout=timeout,env=env).returncode
    except subprocess.TimeoutExpired:return -1

def git_source(folder):
    """Snapshot current authorized package inputs, independent of the user's index."""
    import run
    source=folder/'git-source';source.mkdir()
    for name in ('com.wulato.uikit','com.wulato.uikit.input-system'):
        shutil.copytree(run.R/'Packages'/name,source/'Packages'/name)
    for cmd in (['git','init',str(source)],['git','-C',str(source),'add','Packages'],['git','-C',str(source),'-c','user.name=UIKit Verification','-c','user.email=verification@localhost','commit','-m','Pinned T11 package snapshot']):
        if command(cmd,folder/('git-'+str(uuid.uuid4())+'.log')):raise ValueError('git-snapshot-failed')
    revision=subprocess.check_output(['git','-C',str(source),'rev-parse','HEAD'],text=True).strip()
    return source,revision

def prepare_consumer(host,ver,backend,source,revision,profile):
    import run
    from runtime import UNITASK
    host.mkdir();(host/'Packages').mkdir();(host/'Assets').mkdir()
    original=run.V/'Projects'/f'{run.VERSIONS[ver][0]}-{backend}'
    shutil.copytree(original/'ProjectSettings',host/'ProjectSettings')
    deps={'com.wulato.uikit':'git+'+source.as_uri()+'?path=Packages/com.wulato.uikit#'+revision,'com.cysharp.unitask':UNITASK,'com.unity.ugui':run.VERSIONS[ver][1],**{'com.unity.modules.'+n:'1.0.0' for n in ('ui','imgui','physics','physics2d','jsonserialize')}}
    if backend=='input':deps.update({'com.unity.inputsystem':'1.17.0','com.wulato.uikit.input-system':'git+'+source.as_uri()+'?path=Packages/com.wulato.uikit.input-system#'+revision})
    if profile=='smoke':
        # Only the isolated runtime smoke assembly is copied. No TestAssemblies or prototypes.
        shutil.copytree(run.V/'Packages/com.wulato.uikit.acceptance/Runtime',host/'Assets/UIKitMobileSmoke')
        from codegen import fixture
        fixture(host,run.V/'Packages/com.wulato.uikit.acceptance/Fixtures/Codegen')
        # Enable this copied, non-TestAssemblies entry only in dedicated smoke consumers.
        definition=host/'Assets/UIKitMobileSmoke/Wulato.UIKit.MobileSmoke.asmdef'
        data=json.loads(definition.read_text());data.pop('defineConstraints',None);run.dump(definition,data)
        editor=host/'Assets/UIKitMobileSmoke/Editor';editor.mkdir()
        body=(run.V/'Packages/com.wulato.uikit.acceptance/Editor/MobileSmokeSceneBuilder.cs').read_text().replace('#if UIKIT_MOBILE_SMOKE','').replace('#endif','')
        (editor/'MobileSmokeSceneBuilder.cs').write_text(body)
        run.dump(editor/'Wulato.UIKit.MobileSmoke.Editor.asmdef',dict(name='Wulato.UIKit.MobileSmoke.Editor',references=['Wulato.UIKit.MobileSmoke','Wulato.UIKit.Core','Wulato.UIKit.UGUI','Wulato.UIKit.Editor','UnityEngine.UI'],includePlatforms=['Editor']))
    run.dump(host/'Packages/manifest.json',dict(dependencies=deps))
    for name in ('QuickStart','Modals','Inventory'):
        shutil.copytree(source/'Packages/com.wulato.uikit/Samples~'/name,host/'Assets/Samples/Wulato UIKit'/name)

def run_build(args):
    import run
    from locking import guard,VerificationBusy
    try:
        with guard(run.A):
            batch=run.A/('install-checks' if args.prepare_only else 'mobile')/(datetime.datetime.now(datetime.timezone.utc).strftime('%Y%m%dT%H%M%S%f')+'-'+uuid.uuid4().hex[:6]);batch.mkdir(parents=True)
            fingerprint=source_fingerprint();rows=[]
            selections=[(v,b,h,t) for v,b,h in run.selected(args) for t in ('Android','iOS') if args.target in ('all',t)]
            run.dump(batch/'inventory.json',dict(schema=1,fingerprint=fingerprint,combinations=[dict(editor=v,backend=b,target=t,profile=args.profile) for v,b,h,t in selections]))
            for ver,backend,_,target in selections:
                out=batch/f'{ver}-{backend}-{target}-{args.profile}';out.mkdir()
                request=dict(editor=ver,backend=backend,target=target,profile=args.profile,fingerprint=fingerprint,architecture='ARM64',scripting_backend='IL2CPP',commit=subprocess.check_output(['git','rev-parse','HEAD'],cwd=run.R,text=True).strip())
                request['attempt']=batch.name;request['build_id']=build_identity(request)
                run.dump(out/'request.json',request);result=dict(request,status='FAIL',generation_compiled=False,errors=[])
                try:
                    exe=Path(args.editor_path) if args.editor_path else Path('/Applications/Unity/Hub/Editor')/ver/'Unity.app/Contents/MacOS/Unity'
                    run.verify_editor(exe,ver)
                    module=find_build_module(exe,target)
                    if module is None and not args.prepare_only:
                        result.update(status='BLOCKED',errors=['missing-build-support:'+str(module)])
                        run.dump(out/'environment.json',dict(editor=ver,module=None,searched_roots=[str(exe.parents[3]/'PlaybackEngines'),str(exe.parent.parent/'PlaybackEngines')]))
                    else:
                        source,revision=git_source(out);host=out/'consumer';prepare_consumer(host,ver,backend,source,revision,args.profile)
                        result.update(git_revision=revision,git_source=str(source))
                        env=os.environ.copy();env.update(UIKIT_BUILD_REQUEST=str((out/'request.json').resolve()),UIKIT_EVIDENCE_DIR=str(out.resolve()))
                        # Generate and complete run in distinct editor invocations; build only validates.
                        for step,method in [('01-import','PrepareSamples'),('02-generate','Generate'),('03-complete','Complete'),('04-wire','Wire'),('05-compile-validate','Validate'),('06-build','Build')]:
                            if args.prepare_only and method=='Build':continue
                            log=out/(step+'.log');cmd=[str(exe),'-batchmode','-quit','-projectPath',str(host),'-executeMethod','Wulato.UIKit.Editor.Verification.UIPlayerBuild.'+method,'-logFile',str(log)]
                            rc=command(cmd,out/(step+'-stdout.log'),args.timeout,env)
                            body=log.read_text(errors='replace') if log.exists() else ''
                            body=re.sub(r'(Serial number assigned to: ).*',r'\1[REDACTED]',body);log.write_text(body)
                            if rc or re.search(r'error CS\d+|Scripts have compiler errors',body) or 'UIKIT_BUILD_STAGE_OK' not in body:raise ValueError('stage-failed:'+step)
                            if method=='Generate' and args.profile=='smoke':
                                from codegen import usage
                                usage(host/'Assets/UIKitCodegenFixture',run.V/'Packages/com.wulato.uikit.acceptance/Fixtures/Codegen')
                            if method=='PrepareSamples':
                                lock=host/'Packages/packages-lock.json'
                                request.update(dependency_lock_sha256=hashlib.sha256(lock.read_bytes()).hexdigest(),git_revision=revision)
                                request['build_id']=build_identity(request);run.dump(out/'request.json',request);result.update(request)
                        if args.prepare_only:
                            result.update(status='BLOCKED',install_status='PASS',errors=['prepare-only; no mobile Player built'])
                        else:
                            report=json.loads((out/'build-report.json').read_text());result=merge_player_report(result,report)
                        result['generation_compiled']=True
                        lock=host/'Packages/packages-lock.json';shutil.copy2(lock,out/'packages-lock.json')
                        dependencies=json.loads(lock.read_text())['dependencies']
                        for name in ('com.wulato.uikit',*(['com.wulato.uikit.input-system'] if backend=='input' else [])):
                            if dependencies[name].get('source')!='git' or revision not in dependencies[name].get('version',''):raise ValueError('not-pinned-git-install:'+name)
                        result['dependency_lock_sha256']=hashlib.sha256(lock.read_bytes()).hexdigest()
                except (OSError,ValueError,subprocess.SubprocessError,KeyError) as e:
                    result.update(status='FAIL',install_status='FAIL');result['errors'].append(str(e))
                result['inventory_sha256']=hashlib.sha256((batch/'inventory.json').read_bytes()).hexdigest()
                result['source_after']=source_fingerprint()
                if result['source_after']!=fingerprint:result.update(status='FAIL',errors=result['errors']+['inputs-changed'])
                # Exclude huge consumer Libraries and the Git snapshot; archive logs, build and provenance.
                result['files']={n:h for n,h in file_index(out).items() if not n.startswith(('consumer/','git-source/'))}
                run.dump(out/'summary.json',result);rows.append(result);print(result['status'],ver,backend,target,args.profile,result['errors'],flush=True)
            write_matrix_report(batch,rows);print('EVIDENCE',batch,flush=True)
            return int(any(r['status']!='PASS' for r in rows) or not rows)
    except VerificationBusy as e:print('FAIL',e);return 1
