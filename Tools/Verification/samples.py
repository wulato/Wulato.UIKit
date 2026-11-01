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
                try:
                    for name in NAMES:import_sample(run.R/'Packages/com.wulato.uikit/Samples~'/name,host)
                    step('01-import-compile','CheckAssemblies')
                    install_tests(host)
                    step('02-validate' if args.check_only else '02-acceptance-red' if args.red else '02-author','Validate' if args.check_only else 'CheckAssemblies' if args.red else 'Author')
                    if not args.red and not args.check_only:
                        step('03-generate','Generate');step('04-writeback','Complete');step('05-usage-scenes','FinalizeSamples');step('06-validate','Validate')
                    snap=run.snapshot(ver,backend,host);run.dump(out/'host-snapshot.json',snap)
                    if snap['errors']:raise ValueError(str(snap['errors']))
                    result=dict(status='PASS',editor=ver,backend=backend,steps=steps,fingerprint=run.fingerprint(host),evidence=str(out),red=args.red)
                except (ValueError,OSError) as error:result=dict(status='FAIL',editor=ver,backend=backend,steps=steps,error=str(error))
                result['files']={str(p.relative_to(out)):sha256(p) for p in out.rglob('*') if p.is_file()};run.dump(out/'summary.json',result);reports.append(result)
                if result['status']=='FAIL':break
            run.dump(batch/'summary.json',reports);print('EVIDENCE',batch,flush=True)
            return int(not reports or any(r['status']!='PASS' for r in reports))
    except (VerificationBusy,ValueError,OSError) as e:print('FAIL',str(e),flush=True);return 1
