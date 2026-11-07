from editor_targets import PROFILES
"""Pinned Git consumer installation and staged ARM64/IL2CPP builds."""
import datetime,hashlib,io,json,os,re,shutil,subprocess,tarfile,uuid,zipfile
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

def latest_attempt(root,editor,backend,target,profile,phase="phase1",package_set="all"):
    found=[]
    for path in sorted(root.glob('*/inventory.json')):
        data=json.loads(path.read_text())
        if any((r.get('editor'),r.get('backend'),r.get('target'),r.get('profile'))==(editor,backend,target,profile) and r.get('phase','phase1')==phase and r.get('package_set','all')==package_set for r in data['combinations']):
            found.append(path.parent/(f'{editor}-{backend}-{target}-{profile}'+('' if package_set=='all' else '-only-'+package_set)))
    return found[-1] if found else None

def validate_build_report(raw,summary):
    return ['raw-build-report:'+key for key in ('status','build_id','output','application_id','architecture','scripting_backend','player_assemblies','phase') if raw.get(key)!=summary.get(key)]

def source_fingerprint():
    import run
    files=[p for base in (run.R/'Packages/com.wulato.uikit',run.V/'Packages') for p in base.rglob('*') if p.is_file() and p.name!='.DS_Store']
    from runtime import EXTENSIONS
    files += [p for name in EXTENSIONS for p in (run.R/'Packages'/name).rglob('*') if p.is_file() and p.name!='.DS_Store']
    files+=list((run.R/'.github/workflows').glob('uikit-*.yml'))+list(run.C.glob('*.py'))+[run.C/'matrix.json',run.C/'editors.json',run.R/'docs/superpowers/specs/2026-10-31-wulato-uikit-design.md']
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

def git_package_files(source,revision):
    """Read package bytes from the pinned commit, never its working tree."""
    archive=subprocess.check_output(['git','-C',str(source),'archive',revision,'Packages'])
    files={}
    with tarfile.open(fileobj=io.BytesIO(archive)) as tar:
        for member in tar:
            path=Path(member.name)
            if path.is_absolute() or '..' in path.parts or path.parts[0]!='Packages':
                raise ValueError('git-snapshot-unsafe-path:'+member.name)
            if member.isdir():continue
            if not member.isfile():raise ValueError('git-snapshot-non-file:'+member.name)
            files[member.name]=tar.extractfile(member).read()
    return files

def validate_git_snapshot(source,revision,expected):
    actual={name:hashlib.sha256(body).hexdigest() for name,body in git_package_files(source,revision).items()}
    if actual!=expected:
        missing=sorted(set(expected)-set(actual));extra=sorted(set(actual)-set(expected))
        changed=sorted(name for name in set(actual)&set(expected) if actual[name]!=expected[name])
        raise ValueError('git-snapshot-content-mismatch:'+json.dumps(dict(missing=missing,extra=extra,changed=changed)))
    return actual

def git_source(folder):
    """Snapshot current authorized package inputs, independent of the user's index."""
    import run
    source=folder/'git-source';source.mkdir()
    from runtime import EXTENSIONS
    for name in ('com.wulato.uikit',):
        shutil.copytree(run.R/'Packages'/name,source/'Packages'/name,ignore=shutil.ignore_patterns('.DS_Store'))
    expected={p.relative_to(source).as_posix():hashlib.sha256(p.read_bytes()).hexdigest() for p in (source/'Packages').rglob('*') if p.is_file()}
    # Force only this isolated package snapshot; user/global ignore rules must not omit UPM ~ folders.
    for cmd in (['git','init',str(source)],['git','-C',str(source),'add','--force','--','Packages'],['git','-C',str(source),'-c','user.name=UIKit Verification','-c','user.email=verification@localhost','commit','-m','Pinned T11 package snapshot']):
        if command(cmd,folder/('git-'+str(uuid.uuid4())+'.log')):raise ValueError('git-snapshot-failed')
    revision=subprocess.check_output(['git','-C',str(source),'rev-parse','HEAD'],text=True).strip()
    validate_git_snapshot(source,revision,expected)
    run.dump(folder/'package-snapshot.json',dict(schema=1,git_revision=revision,files=expected))
    return source,revision

def prepare_consumer(host,ver,backend,source,revision,profile,phase="phase1",package_set="all"):
    import run
    from runtime import UNITASK
    if package_set in ('navigation','dialogs','presentation','popup-queue'):raise ValueError('Package migration: use main, addressables or all.')
    sample_files=git_package_files(source,revision)
    def copy_sample(package,name):
        prefix=f'Packages/{package}/Samples~/{name}/'
        selected={path[len(prefix):]:body for path,body in sample_files.items() if path.startswith(prefix)}
        if not selected:raise ValueError('git-snapshot-missing-sample:'+prefix)
        destination=host/'Assets/Samples/Wulato UIKit'/name
        for path,body in selected.items():
            target=destination/path;target.parent.mkdir(parents=True,exist_ok=True);target.write_bytes(body)
    host.mkdir();(host/'Packages').mkdir();(host/'Assets').mkdir()
    original=run.V/'Projects'/f'{run.VERSIONS[ver][0]}-{backend}'
    shutil.copytree(original/'ProjectSettings',host/'ProjectSettings')
    deps={'com.wulato.uikit':'git+'+source.as_uri()+'?path=Packages/com.wulato.uikit#'+revision,'com.cysharp.unitask':UNITASK,'com.unity.ugui':run.VERSIONS[ver][1],**{'com.unity.modules.'+n:'1.0.0' for n in ('ui','imgui','physics','physics2d','jsonserialize')}}
    if backend=='input':deps.update({'com.unity.inputsystem':PROFILES[ver]['inputSystem']})
    from runtime import EXTENSIONS
    selected=selected_packages(phase,package_set)
    for name in selected:deps[name]='git+'+source.as_uri()+'?path=Packages/'+name+'#'+revision
    if phase in ('phase3','uikit05') and package_set!='main':
        from addressables_content import VERSIONS as ADDRESSABLES_VERSIONS
        deps.update(dict(zip(('com.unity.addressables','com.unity.scriptablebuildpipeline'),ADDRESSABLES_VERSIONS[ver])))
    if profile=='smoke':
        # Only the isolated runtime smoke assembly is copied. No TestAssemblies or prototypes.
        shutil.copytree(run.V/'Packages/com.wulato.uikit.acceptance/Runtime',host/'Assets/UIKitMobileSmoke')
        from codegen import fixture
        fixture(host,run.V/'Packages/com.wulato.uikit.acceptance/Fixtures/Codegen')
        # Enable this copied, non-TestAssemblies entry only in dedicated smoke consumers.
        definition=host/'Assets/UIKitMobileSmoke/Wulato.UIKit.MobileSmoke.asmdef'
        data=json.loads(definition.read_text());data.pop('defineConstraints',None)
        if phase in ('phase2','uikit05'):
            data['references']+=['Wulato.UIKit.Samples.BusinessFlow']
            data['versionDefines']=[dict(name='com.wulato.uikit',expression='0.5.0',define='UIKIT_PHASE2_SMOKE')]
            if phase=='uikit05':
                data['references']+=['Wulato.UIKit.Addressables','Unity.Addressables','Unity.ResourceManager','Wulato.UIKit.Samples.Addressables']
                data['versionDefines'].append(dict(name='com.wulato.uikit',expression='0.5.0',define='UIKIT_05_SMOKE'))
        run.dump(definition,data)
        editor=host/'Assets/UIKitMobileSmoke/Editor';editor.mkdir()
        body=(run.V/'Packages/com.wulato.uikit.acceptance/Editor/MobileSmokeSceneBuilder.cs').read_text().replace('#if UIKIT_MOBILE_SMOKE','').replace('#endif','')
        (editor/'MobileSmokeSceneBuilder.cs').write_text(body)
        run.dump(editor/'Wulato.UIKit.MobileSmoke.Editor.asmdef',dict(name='Wulato.UIKit.MobileSmoke.Editor',references=['Wulato.UIKit.MobileSmoke','Wulato.UIKit','Wulato.UIKit.Editor','UnityEngine.UI'],includePlatforms=['Editor']))
    run.dump(host/'Packages/manifest.json',dict(dependencies=deps))
    if phase in ('phase2','phase3','uikit05') and package_set=='all':copy_sample('com.wulato.uikit','BusinessFlow')
    if phase in ('phase3','uikit05') and package_set!='main':
        copy_sample('com.wulato.uikit','LocalInventory')
        probe=host/'Assets/AddressablesCompileProbe';probe.mkdir()
        run.dump(probe/'AddressablesCompileProbe.asmdef',dict(name='UIKit.AddressablesConsumer',references=['Wulato.UIKit','Wulato.UIKit.Addressables'],**({'includePlatforms':['Editor']} if phase=='uikit05' else {})))
        (probe/'Probe.cs').write_text('public static class AddressablesCompileProbe { public static Wulato.UIKit.UIContextBuilder Compose(Wulato.UIKit.UIContextBuilder b) => b.WithLoader(new Wulato.UIKit.AddressablesUIViewLoader()); }\n')
    for name in ('QuickStart','Modals','Inventory'):
        copy_sample('com.wulato.uikit',name)

def addressables_payload(output,target):
    output=Path(output)
    if target=='Android':
        with zipfile.ZipFile(output) as archive:names=archive.namelist()
    else:names=[p.relative_to(output).as_posix() for p in output.rglob('*') if p.is_file()]
    names=[n for n in names if '/aa/' in '/'+n]
    if not any('/aa/'+target+'/' in '/'+n and n.endswith('.bundle') for n in names):
        raise ValueError('missing-target-addressables-bundle:'+target)
    if not any(Path(n).name.startswith('catalog.') for n in names):raise ValueError('missing-addressables-catalog')
    return sorted(names)

def compile_ios_native(output,out,timeout):
    """Compile the exported ARM64 project without accessing signing accounts."""
    cmd=['xcodebuild','-project',str(Path(output)/'Unity-iPhone.xcodeproj'),
         '-scheme','Unity-iPhone','-configuration','Release','-sdk','iphoneos',
         '-destination','generic/platform=iOS','-derivedDataPath',str(out/'UnsignedDerivedData'),
         'ARCHS=arm64','CODE_SIGNING_ALLOWED=NO','build']
    return command(cmd,out/'ios-native-compile.log',timeout)==0

def run_build(args):
    import run
    from locking import guard,VerificationBusy
    phase=getattr(args,'phase','phase1');package_set=getattr(args,'package_set','all')
    scope_errors=validate_build_scope(args)
    if scope_errors:
        print('FAIL',scope_errors);return 1
    try:
        with guard(run.A):
            batch=run.A/('install-checks' if args.prepare_only else 'mobile')/(datetime.datetime.now(datetime.timezone.utc).strftime('%Y%m%dT%H%M%S%f')+'-'+uuid.uuid4().hex[:6]);batch.mkdir(parents=True)
            fingerprint=source_fingerprint();rows=[]
            selections=[(v,b,h,t) for v,b,h in run.selected(args) for t in ('Android','iOS') if args.target in ('all',t)]
            run.dump(batch/'inventory.json',dict(schema=1,fingerprint=fingerprint,combinations=[dict(editor=v,backend=b,target=t,profile=args.profile,phase=phase,package_set=package_set) for v,b,h,t in selections]))
            for ver,backend,_,target in selections:
                out=batch/(f'{ver}-{backend}-{target}-{args.profile}'+('' if package_set=='all' else '-only-'+package_set));out.mkdir()
                request=dict(editor=ver,backend=backend,target=target,profile=args.profile,phase=phase,package_set=package_set,fingerprint=fingerprint,architecture='ARM64',scripting_backend='IL2CPP',commit=subprocess.check_output(['git','rev-parse','HEAD'],cwd=run.R,text=True).strip())
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
                        source,revision=git_source(out);host=out/'consumer';prepare_consumer(host,ver,backend,source,revision,args.profile,phase,package_set)
                        result.update(git_revision=revision,git_source=str(source))
                        env=os.environ.copy();env.update(UIKIT_BUILD_REQUEST=str((out/'request.json').resolve()),UIKIT_EVIDENCE_DIR=str(out.resolve()))
                        def addressable_stage(name,method):
                            log=out/(name+'.log')
                            cmd=[str(exe),'-batchmode','-quit',*(['-buildTarget',target] if phase=='uikit05' else []),'-projectPath',str(host),'-executeMethod','Wulato.UIKit.Samples.Addressables.Editor.LocalInventorySetup.'+method,'-logFile',str(log)]
                            rc=command(cmd,out/(name+'-stdout.log'),args.timeout,env)
                            body=log.read_text(errors='replace') if log.exists() else ''
                            body=re.sub(r'(Serial number assigned to: ).*',r'\1[REDACTED]',body);log.write_text(body)
                            if rc or re.search(r'error CS\d+|Scripts have compiler errors',body) or 'UIKIT_ADDRESSABLES_SAMPLE_OK' not in body:raise ValueError('addressables-stage-failed:'+name)
                        # Generate and complete run in distinct editor invocations; build only validates.
                        for step,method in [('01-import','PrepareSamples'),('02-generate','Generate'),('03-complete','Complete'),('04-wire','Wire'),('05-compile-validate','Validate'),('06-build','Build')]:
                            if args.prepare_only and method=='Build':continue
                            log=out/(step+'.log');cmd=[str(exe),'-batchmode','-quit',*(['-buildTarget',target] if phase=='uikit05' else []),'-projectPath',str(host),'-executeMethod','Wulato.UIKit.Editor.Verification.UIPlayerBuild.'+method,'-logFile',str(log)]
                            rc=command(cmd,out/(step+'-stdout.log'),args.timeout,env)
                            body=log.read_text(errors='replace') if log.exists() else ''
                            body=re.sub(r'(Serial number assigned to: ).*',r'\1[REDACTED]',body);log.write_text(body)
                            if rc or re.search(r'error CS\d+|Scripts have compiler errors',body) or 'UIKIT_BUILD_STAGE_OK' not in body:raise ValueError('stage-failed:'+step)
                            if phase in ('phase3','uikit05') and package_set!='main':
                                if method=='PrepareSamples':addressable_stage('01-addressables-author','Author')
                                if method=='Wire':addressable_stage('04-addressables-scene','FinalizeSample')
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
                        if not args.prepare_only and phase=='uikit05':
                            result['addressables_content']=addressables_payload(result['output'],target)
                            result['addressables_platform']=target
                        if not args.prepare_only and phase=='uikit05' and target=='iOS':
                            result['native_compiled']=compile_ios_native(result['output'],out,args.timeout)
                            result['native_signed']=False
                            if not result['native_compiled']:raise ValueError('ios-unsigned-native-compile-failed')
                        result['generation_compiled']=True
                        lock=host/'Packages/packages-lock.json';shutil.copy2(lock,out/'packages-lock.json')
                        dependencies=json.loads(lock.read_text())['dependencies']
                        for name in ('com.wulato.uikit',):
                            if dependencies[name].get('source')!='git' or revision not in dependencies[name].get('version',''):raise ValueError('not-pinned-git-install:'+name)
                        if phase in ('phase3','uikit05'):validate_phase3_install(out,ver,backend,package_set,revision)
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

def validate_build_scope(args):
    phase=getattr(args,'phase','phase1');scope=getattr(args,'package_set','all')
    if scope in ('navigation','dialogs','presentation','popup-queue'):return ['Package migration: business modules are included in main; remove old package references.']
    if phase=='uikit05':
        return [] if scope=='all' else ['uikit05 mobile contract requires single main package plus Addressables SDK (all scope)']
    if phase=='phase3':
        return [] if args.profile=='clean' and args.prepare_only and scope in ('main','addressables','all') else ['phase3 requires clean prepare-only and main/addressables/all']
    if scope=='addressables' or (scope!='all' and (phase!='phase2' or not args.prepare_only or args.profile!='clean')):return ['single-package scope requires phase2 clean prepare-only']
    return []

def selected_packages(phase,scope):
    from runtime import EXTENSIONS,FLOW_EXTENSIONS
    choices=EXTENSIONS if phase=='phase3' else {}
    return [name for name,suite in choices.items() if scope=='all' or scope==suite]

def validate_phase3_install(folder,editor,backend,scope,revision):
    from addressables_content import VERSIONS
    from runtime import LEGACY_ADAPTERS
    report=json.loads((folder/'dependency-snapshot.json').read_text())
    packages={p['name']:p for p in report['packages']}
    expected={'com.wulato.uikit'}
    if LEGACY_ADAPTERS&set(packages):raise ValueError('Package migration: remove legacy UIKit adapter packages')
    if report.get('editor')!=editor or packages['com.wulato.uikit']['version']!='0.5.0':raise ValueError('phase3-installed-baseline')
    for name in expected:
        if packages[name].get('source')!='Git':raise ValueError('phase3-non-git-package:'+name)
    if scope=='main':
        if 'com.unity.addressables' in packages:raise ValueError('main-pulled-addressables')
    else:
        for name,wanted in zip(('com.unity.addressables','com.unity.scriptablebuildpipeline'),VERSIONS[editor]):
            if packages.get(name,{}).get('version')!=wanted:raise ValueError('phase3-resolved-version:'+name)
    assemblies=runtime_dependency_closure(report)
    excluded=sorted(set(report['runtimeAssemblies'])-set(assemblies))
    from evidence import sha256
    for node in report['runtimeAssemblyGraph']:
        if node['name'] not in assemblies:continue
        path=folder/node.get('metadataFile','')
        if not path.resolve().is_relative_to((folder/'runtime-metadata').resolve()) or not path.is_file() or sha256(path)!=node.get('metadataSha256'):raise ValueError('phase3-runtime-il-evidence:'+node['name'])
    (folder/'runtime-closure.json').write_text(json.dumps(dict(scope='editor-compiled-runtime IL imports and Player compilation graph; no Player built',roots=sorted(n['name'] for n in report['runtimeAssemblyGraph'] if n['assetSource'] or (n['name']=='Wulato.UIKit' or n['name'].startswith('Wulato.UIKit.'))),reachable=assemblies,precompiled=sorted({b for n in report['runtimeAssemblyGraph'] if n['name'] in assemblies for b in n.get('precompiledReferences',[]) if b in n['metadataReferences']}),unreferenced_candidates=excluded),indent=2)+'\n')
    required={'Wulato.UIKit'}|({'Wulato.UIKit.Addressables','Wulato.UIKit.Samples.Addressables'} if scope!='main' else set())
    if not required.issubset(assemblies):raise ValueError('phase3-missing-runtime-assemblies')


def runtime_dependency_closure(report):
    # GetAssemblies(Player) enumerates compilation candidates, including autoReferenced=false SDK tests.
    # Follow actual compiler references from consumer sources and selected UIKit runtime assemblies.
    graph=report.get('runtimeAssemblyGraph',[]);nodes={n['name']:n for n in graph}
    if not nodes or len(nodes)!=len(graph) or set(nodes)!=set(report.get('runtimeAssemblies',[])):raise ValueError('phase3-missing-runtime-dependency-graph')
    roots={name for name,node in nodes.items() if node.get('assetSource') is True or (name=='Wulato.UIKit' or name.startswith('Wulato.UIKit.'))}
    if not roots:raise ValueError('phase3-missing-runtime-roots')
    pending=list(roots);visited=set()
    while pending:
        name=pending.pop()
        if name in visited:continue
        if name not in nodes:raise ValueError('phase3-unresolved-runtime-reference:'+name)
        metadata=nodes[name].get('metadataReferences')
        if metadata is None:raise ValueError('phase3-missing-runtime-il-metadata:'+name)
        binaries=[binary for binary in nodes[name].get('precompiledReferences',[]) if binary in metadata]
        if any(is_test_binary(binary) for binary in binaries):raise ValueError('phase3-precompiled-test-runtime-reference:'+name)
        visited.add(name);pending.extend(nodes[name]['references'])
    if any(any(word in name.lower() for word in ('test','acceptance','verification','nunit')) for name in visited):raise ValueError('phase3-runtime-assembly-isolation')
    return sorted(visited)

def is_test_binary(name):
    # UnityEngine.UnityTestProtocolModule is a built-in engine module in Unity 2022,
    # not NUnit/TestRunner. Match test/framework assembly naming, not arbitrary 'test' substrings.
    value=name.lower()
    return value.startswith(('test','nunit')) or value.endswith(('test','tests')) or any(token in value for token in ('.test','testrunner','nunit','acceptance','verification'))
