"""T9 staged real-compiler acceptance. Uses the shared lock and matching-version runner."""
import datetime,json,re,shutil,uuid
from pathlib import Path

def assess(rc,timed_out,log):
    return rc==0 and not timed_out and 'UIKIT_CODEGEN_OK' in log and not re.search(r'error CS\d+|Scripts have compiler errors',log)

def validate_report(report,version,required):
    return report.get('status')=='PASS' and report.get('editor')==version and required.issubset(set(report.get('checks',[])))

def tree(folder):
    from evidence import sha256
    return {str(p.relative_to(folder)):sha256(p) for p in folder.rglob('*') if p.is_file()}

def fixture(host,source):
    root=host/'Assets/UIKitCodegenFixture'
    if root.exists():
        for name in ('ViewA','ViewB','DefaultView'):
            scripts=list(root.rglob(name+'.cs'))
            if len(scripts)!=1 or scripts[0].read_text()!=(source/(name+'.cs.txt')).read_text():
                raise ValueError('fixture-source-drift:'+name+' (existing business source preserved)')
        definition=json.loads(next(next(root.rglob('ViewB.cs')).parent.glob('*.asmdef')).read_text())
        if definition.get('autoReferenced',True) is not False:raise ValueError('fixture-must-test-autoReferenced-false')
        return root
    root.mkdir(parents=True)
    for name,folder,assembly in [('ViewA','A','UIKit.Codegen.A'),('ViewB','B','UIKit.Codegen.B'),('DefaultView','Default',None)]:
        target=root/folder;target.mkdir()
        (target/(name+'.cs')).write_text((source/(name+'.cs.txt')).read_text())
        if assembly:
            (target/(assembly+'.asmdef')).write_text(json.dumps(dict(name=assembly,references=['Wulato.UIKit','UniTask'],autoReferenced=name!='ViewB'),indent=2)+'\n')
    return root

def usage(root,source):
    body=(source/'BindingUsageProbe.cs.txt').read_text()
    for name,ns in [('ViewA','Codegen.A'),('ViewB','Codegen.B'),('DefaultView','')]:
        path=next(root.rglob(name+'.cs')).parent/(name+'.Usage.cs')
        text=('namespace '+ns+' {\n' if ns else '')+('internal' if name=='ViewB' else 'public')+' partial class '+name+' {\n'+body+'}\n'+('}\n' if ns else '')
        if not path.exists() or path.read_text()!=text:path.write_text(text)
        meta=path.with_name(path.name+'.meta')
        if not meta.exists():meta.write_text('fileFormatVersion: 2\nguid: '+uuid.uuid4().hex+'\n')

def run_codegen(args):
    import run
    from locking import guard,VerificationBusy
    from evidence import sha256
    try:
        with guard(run.A):
            batch=run.A/(datetime.datetime.now(datetime.timezone.utc).strftime('%Y%m%dT%H%M%S%f')+'-'+args.command+'-'+uuid.uuid4().hex[:6]);batch.mkdir(parents=True)
            summaries=[]
            for ver,backend,host in run.selected(args):
                out=batch/host.name;out.mkdir();steps=[];checks=set();before=run.fingerprint(host)
                snap=run.snapshot(ver,backend,host);run.dump(out/'host-before.json',snap)
                if snap['errors']:raise ValueError(str(snap['errors']))
                source=run.V/'Packages/com.wulato.uikit.acceptance/Fixtures/Codegen'
                generated=host/'Assets/Wulato.UIKit.Generated'
                def step(name,method,required):
                    folder=out/name;folder.mkdir();args.execute_method='Wulato.UIKit.Acceptance.Editor.GeneratedAssemblyProbe.'+method
                    original=run.fingerprint(host)
                    rc,to,log,actual=run.unity(args,ver,host,folder)
                    report=json.loads((folder/'codegen-report.json').read_text()) if (folder/'codegen-report.json').is_file() else {}
                    passed=assess(rc,to,log) and actual==ver and validate_report(report,ver,set(required))
                    if method=='Verify' and original!=run.fingerprint(host):passed=False
                    steps.append(dict(name=name,status='PASS' if passed else 'FAIL',returncode=rc,timed_out=to,actual_editor=actual,source_before=original,source_after=run.fingerprint(host)))
                    print(host.name,name,steps[-1]['status'],flush=True)
                    if not passed:raise ValueError('codegen-stage-failed:'+str(folder))
                    checks.update(report['checks'])
                try:
                    if args.command=='codegen':
                        root=fixture(host,source)
                        step('01-base','Prepare',['FixtureBaseCompilation'])
                        step('02-generate','Generate',['GenerationRequested'])
                        step('03-writeback','Complete',['BindingsPartial_CompilesInOriginalAssembly','SerializedBindings_NonNull'])
                        usage(root,source)
                        step('04-use','Verify',['BindingUsageProbe_CompilesAndRuns','MixedAssemblyProviders_PopulateRegistryWithoutReverseReferences','WrongAssemblyRoute_PreventsBuild','StaleFingerprint_PreventsBuild','RenameAndVariant_PreserveBindings'])
                        stable=tree(generated);prefabs=tree(root)
                        step('05-repeat','Generate',['GenerationRequested']);step('06-repeat-writeback','Complete',['SerializedBindings_NonNull'])
                        if stable!=tree(generated) or prefabs!=tree(root):raise ValueError('second-generation-produced-diff:'+str(out))
                        checks.add('SecondGeneration_ProducesNoDiff')
                        old_manifest=json.loads((generated/'manifest.json').read_text())
                        old_files={f['path'] for f in old_manifest['files']}
                        business=next(root.rglob('ViewA.cs'));definition=next(business.parent.glob('*.asmdef'))
                        original_business=business.read_bytes();definition_data=json.loads(definition.read_text())
                        definition_data['name']='UIKit.Codegen.A.Renamed' if definition_data['name']=='UIKit.Codegen.A' else 'UIKit.Codegen.A'
                        definition.write_text(json.dumps(definition_data,indent=2)+'\n')
                        moved=root/('MovedA' if business.parent.name=='A' else 'A')
                        old_dir=business.parent;shutil.move(str(old_dir),str(moved))
                        if old_dir.with_suffix('.meta').exists():shutil.move(str(old_dir.with_suffix('.meta')),str(moved.with_suffix('.meta')))
                        sentinel=host/next(f for f in old_files if f.endswith('.Bindings.cs') and '/Assemblies/' in f)
                        sentinel=sentinel.parent/'UserOwned.cs';sentinel.write_text('// user-owned sentinel\n');
                        step('07-reroute','Generate',['GenerationRequested']);step('08-reroute-writeback','Complete',['SerializedBindings_NonNull'])
                        step('09-reroute-use','Verify',['BindingUsageProbe_CompilesAndRuns'])
                        new_files={f['path'] for f in json.loads((generated/'manifest.json').read_text())['files']}
                        if any((host/f).exists() for f in old_files-new_files) or sentinel.read_text()!='// user-owned sentinel\n' or (moved/'ViewA.cs').read_bytes()!=original_business:raise ValueError('reroute-did-not-preserve-user-or-remove-owned')
                        checks.add('AsmdefRenameOrMove_ReroutesAndRemovesOnlyOwnedFiles')
                    else:
                        step('validate','Verify',['BindingUsageProbe_CompilesAndRuns','MixedAssemblyProviders_PopulateRegistryWithoutReverseReferences','WrongAssemblyRoute_PreventsBuild'])
                        if before!=run.fingerprint(host):raise ValueError('check-generated-modified-inputs')
                    result=dict(status='PASS',editor=ver,backend=backend,gate='GEN-01',checks=sorted(checks),steps=steps,source_before=before,source_after=run.fingerprint(host))
                except (ValueError,OSError) as error:
                    result=dict(status='FAIL',editor=ver,backend=backend,error=str(error),steps=steps)
                result['files']={str(p.relative_to(out)):sha256(p) for p in out.rglob('*') if p.is_file()}
                run.dump(out/'summary.json',result);summaries.append(result)
                if result['status']=='FAIL':break
            run.dump(batch/'summary.json',summaries);print('EVIDENCE',batch,flush=True)
            return int(not summaries or any(r['status']!='PASS' for r in summaries))
    except (VerificationBusy,ValueError,OSError) as error:
        print('FAIL',error,flush=True);return 1
