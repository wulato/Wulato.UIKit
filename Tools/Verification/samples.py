"""Import consumer samples before mounting their optional acceptance assembly."""
import datetime,json,re,shutil,uuid
from pathlib import Path
NAMES=('QuickStart','Modals','Inventory')
PACKAGE='com.wulato.uikit.acceptance.samples'
def import_sample(source,host):
    target=host/'Assets/Samples/Wulato UIKit'/source.name
    for p in source.rglob('*'):
        if not p.is_file():continue
        dest=target/p.relative_to(source)
        if dest.exists() and dest.read_bytes()!=p.read_bytes() and p.suffix not in ('.prefab','.unity','.meta'):raise ValueError('sample-consumer-drift:'+str(dest))
    for p in source.rglob('*'):
        if p.is_file():
            dest=target/p.relative_to(source);dest.parent.mkdir(parents=True,exist_ok=True)
            if not dest.exists():shutil.copy2(p,dest)
def install_tests(host):
    assemblies={json.loads(p.read_text())['name'] for p in (host/'Assets/Samples/Wulato UIKit').rglob('*.asmdef')}
    if not {'Wulato.UIKit.Samples.'+n for n in NAMES}.issubset(assemblies):raise ValueError('missing-sample-assemblies')
    p=host/'Packages/manifest.json';data=json.loads(p.read_text());data['dependencies'][PACKAGE]='file:../../../Packages/'+PACKAGE
    if PACKAGE not in data.setdefault('testables',[]):data['testables'].append(PACKAGE)
    p.write_text(json.dumps(data,indent=2)+'\n')
def prepare(args):
    if getattr(args,"quickstart_only",False):
        if getattr(args,"check_only",False) or getattr(args,"red",False):
            print('FAIL --quickstart-only cannot be combined with --check-only or --red',flush=True);return 1
        return prepare_quickstart(args)
    import run
    from locking import guard,VerificationBusy
    from evidence import sha256
    try:
        with guard(run.A):
            batch=run.A/(datetime.datetime.now(datetime.timezone.utc).strftime('%Y%m%dT%H%M%S%f')+'-prepare-samples-'+uuid.uuid4().hex[:6]);batch.mkdir(parents=True)
            reports=[]
            for ver,backend,host in run.selected(args):
                out=batch/host.name;out.mkdir();steps=[]
                def step(name,method):
                    folder=out/name;folder.mkdir();args.execute_method='Wulato.UIKit.Acceptance.Editor.SampleAssetBuilder.'+method
                    before=run.fingerprint(host);rc,to,log,actual=run.unity(args,ver,host,folder)
                    report_path=folder/'samples-report.json';report=json.loads(report_path.read_text()) if report_path.exists() else {}
                    ok=rc==0 and not to and actual==ver and report.get('status')=='PASS' and report.get('editor')==ver and (method!='Validate' or before==run.fingerprint(host)) and not re.search(r'error CS\d+|Scripts have compiler errors',log)
                    steps.append(dict(stage=name,status='PASS' if ok else 'FAIL',source_before=before,source_after=run.fingerprint(host),returncode=rc,timed_out=to,actual_editor=actual,report=report))
                    print(host.name,name,steps[-1]['status'],flush=True)
                    if not ok:raise ValueError('sample-stage-failed:'+str(folder))
                def addressable_step(name,method):
                    if not (host/'Assets/Samples/Wulato UIKit/LocalInventory').exists():return
                    folder=out/name;folder.mkdir();args.execute_method='Wulato.UIKit.Samples.Addressables.Editor.LocalInventorySetup.'+method
                    before=run.fingerprint(host);rc,to,log,actual=run.unity(args,ver,host,folder)
                    passed=rc==0 and not to and actual==ver and 'UIKIT_ADDRESSABLES_SAMPLE_OK' in log and not re.search(r'error CS\d+|Scripts have compiler errors',log)
                    if method=='Validate' and before!=run.fingerprint(host):passed=False
                    print(host.name,name,'PASS' if passed else 'FAIL',flush=True)
                    steps.append(dict(stage=name,status='PASS' if passed else 'FAIL',source_before=before,source_after=run.fingerprint(host)))
                    if not passed:raise ValueError('addressables-sample-stage:'+str(folder))
                try:
                    for source in sample_sources(run.R,host):import_sample(source,host)
                    step('01-import-compile','CheckAssemblies')
                    install_tests(host)
                    step('02-validate' if args.check_only else '02-acceptance-red' if args.red else '02-author','Validate' if args.check_only else 'CheckAssemblies' if args.red else 'Author')
                    if args.check_only:addressable_step('02-addressables-validate','Validate')
                    if not args.red and not args.check_only:
                        addressable_step('02-addressables-author','Author')
                        step('03-generate','Generate');step('04-writeback','Complete');step('05-usage-scenes','FinalizeSamples');addressable_step('05-addressables-scene','FinalizeSample');step('06-validate','Validate');addressable_step('06-addressables-validate','Validate')
                    snap=run.snapshot(ver,backend,host);run.dump(out/'host-snapshot.json',snap)
                    if snap['errors']:raise ValueError(str(snap['errors']))
                    result=dict(status='PASS',editor=ver,backend=backend,steps=steps,fingerprint=run.fingerprint(host),evidence=str(out),red=args.red)
                except (ValueError,OSError) as error:result=dict(status='FAIL',editor=ver,backend=backend,steps=steps,error=str(error))
                result['files']={str(p.relative_to(out)):sha256(p) for p in out.rglob('*') if p.is_file()};run.dump(out/'summary.json',result);reports.append(result)
                if result['status']=='FAIL':break
            run.dump(batch/'summary.json',reports);print('EVIDENCE',batch,flush=True)
            return int(not reports or any(r['status']!='PASS' for r in reports))
    except (VerificationBusy,ValueError,OSError) as e:print('FAIL',str(e),flush=True);return 1

def sample_sources(root,host):
    from runtime import FLOW_EXTENSIONS
    sources=[root/"Packages/com.wulato.uikit/Samples~"/name for name in NAMES]
    dependencies=json.loads((host/'Packages/manifest.json').read_text())['dependencies']
    if all(name in dependencies for name in FLOW_EXTENSIONS):sources.append(root/'Packages/com.wulato.uikit.navigation/Samples~/BusinessFlow')
    if 'com.wulato.uikit.addressables' in dependencies:sources.append(root/'Packages/com.wulato.uikit.addressables/Samples~/LocalInventory')
    return sources

def quickstart_manifest(ver,backend):
    import run
    from runtime import UNITASK
    package='com.wulato.uikit.acceptance.quickstart'
    deps={'com.wulato.uikit':'file:'+str(run.R/'Packages/com.wulato.uikit'),'com.cysharp.unitask':UNITASK,
          'com.unity.ugui':run.VERSIONS[ver][1],'com.unity.test-framework':run.VERSIONS[ver][2],
          package:'file:'+str(run.V/'Packages'/package),
          **{'com.unity.modules.'+n:'1.0.0' for n in ('ui','imgui','physics','physics2d','jsonserialize')}}
    if backend=='input':deps.update({'com.unity.inputsystem':'1.17.0','com.wulato.uikit.input-system':'file:'+str(run.R/'Packages/com.wulato.uikit.input-system')})
    return dict(dependencies=deps,testables=['com.wulato.uikit',package]+(['com.wulato.uikit.input-system'] if backend=='input' else []))

def prepare_quickstart(args):
    """Orchestrate clean imports through the existing Unity executor and registered test runner."""
    import argparse,run
    from locking import guard
    from evidence import sha256,verify
    try:
        with guard(run.A):
            batch=run.A/(datetime.datetime.now(datetime.timezone.utc).strftime('%Y%m%dT%H%M%S%f')+'-quickstart-consumers-'+uuid.uuid4().hex[:6]);batch.mkdir()
            hosts=[];reports=[]
            for ver,backend,original in run.selected(args):
                host=batch/original.name;host.mkdir();(host/'Assets').mkdir();(host/'Packages').mkdir()
                shutil.copytree(original/'ProjectSettings',host/'ProjectSettings')
                run.dump(host/'Packages/manifest.json',quickstart_manifest(ver,backend))
                version=json.loads((run.R/'Packages/com.wulato.uikit/package.json').read_text())['version']
                target=host/'Assets/Samples/Wulato UIKit'/version/'QuickStart';target.parent.mkdir(parents=True)
                shutil.copytree(run.R/'Packages/com.wulato.uikit/Samples~/QuickStart',target)
                report={'editor':ver,'backend':backend,'host':str(host),'stages':[]}
                # Re-launches cross real compilation boundaries; second pass proves idempotence.
                for index,method in enumerate(('Begin','Continue','Continue','Validate','BeginInOpenScene','ContinueInOpenScene','ContinueInOpenScene','ValidateInOpenScene')):
                    out=batch/(original.name+'-stage-'+str(index));out.mkdir();args.execute_method='Wulato.UIKit.Acceptance.QuickStart.Editor.QuickStartPreparation.'+method
                    before=run.fingerprint(host)
                    rc,to,log,actual=run.unity(args,ver,host,out)
                    status=rc==0 and not to and actual==ver and (out/'quickstart-preparation.txt').is_file() and not re.search(r'error CS\d+|Scripts have compiler errors',log)
                    if method.startswith('Validate') and before!=run.fingerprint(host):status=False
                    report['stages'].append(dict(method=method,status='PASS' if status else 'FAIL',returncode=rc,actual_editor=actual,source_before=before,source_after=run.fingerprint(host)))
                    run.dump(batch/'preparation.json',reports+[report]);print(original.name,index,method,report['stages'][-1]['status'],flush=True)
                    if not status:raise ValueError('quickstart-stage-failed:'+str(out))
                if report['stages'][3]['source_after']!=report['stages'][7]['source_after']:raise ValueError('quickstart-repeat-not-idempotent:'+str(host))
                hosts.append((ver,backend,host));reports.append(report)
            test_args=argparse.Namespace(**vars(args));test_args.command='tests';test_args.mode='PlayMode';test_args.suite='quickstart-only';test_args.consumer_hosts=hosts
            before=set(run.A.glob('*-tests-*'));rc=run._run_locked(test_args)
            created=set(run.A.glob('*-tests-*'))-before
            checks=[]
            for evidence in created:
                for record in json.loads((evidence/'summary.json').read_text()):
                    host=next(h for v,b,h in hosts if v==record['editor'] and b==record['backend'])
                    required=json.loads((run.C/'matrix.json').read_text())[record['editor']][record['backend']]['PlayMode']['quickstart-only']
                    checked=verify(record,Path(record['evidence']),required,run.snapshot(record['editor'],record['backend'],host),acceptance=True)
                    checks.append(dict(evidence=record['evidence'],**checked))
            if len(checks)!=len(hosts) or any(c['status']!='PASS' for c in checks):rc=1
            run.dump(batch/'acceptance.json',dict(status='PASS' if rc==0 else 'FAIL',preparation=reports,raw_checks=checks))
            print('QUICKSTART EVIDENCE',batch,flush=True);return rc
    except (ValueError,OSError) as e:print('FAIL',str(e),flush=True);return 1
