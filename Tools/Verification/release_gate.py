"""T11 release acceptance; incomplete or stale evidence fails closed."""
import json
from pathlib import Path
MOBILE_CASES=('DirectReference','Resources','TypeErased','CacheReopen','TouchClose','Shutdown','StrippedGeneratedMapping')
PHASE2_CASES=('NavigationPushBack','NavigationRollback','DialogTokenCancel','PopupPauseResume','PausedQueueShutdown','PresenterCacheReopen')
PHASE2_ASSEMBLIES={'Wulato.UIKit.'+name+'.dll' for name in ('Navigation','Dialogs','Presentation','PopupQueue')}
def combinations():
    from run import VERSIONS
    return [(v,b,t) for v in VERSIONS for b in ('legacy','input') for t in ('Android','iOS')]
def assess_mobile(report,build_id,fingerprint,phase="phase1"):
    errors=[]
    for key,wanted,label in [('status','PASS','status'),('build_id',build_id,'build-id'),('fingerprint',fingerprint,'fingerprint'),('profile','smoke','profile'),('architecture','ARM64','architecture'),('scripting_backend','IL2CPP','scripting-backend'),('physical_device',True,'physical-device')]:
        if report.get(key)!=wanted:errors.append(label)
    for key in ('compiled','installed','launched'):
        if report.get(key) is not True:errors.append(key)
    if not report.get('device'):errors.append('device')
    if report.get('phase','phase1')!=phase:errors.append('phase')
    required=MOBILE_CASES+(PHASE2_CASES if phase=='phase2' else ())
    cases=report.get('cases',[])
    if len(cases)!=len(required) or {c.get('name') for c in cases}!=set(required) or any(c.get('status')!='PASS' for c in cases):errors.append('smoke-cases')
    return errors

def assess_build(report,fingerprint,phase="phase1"):
    errors=[]
    if report.get('status')!='PASS':errors.append('build-not-pass')
    if report.get('phase','phase1')!=phase:errors.append('phase')
    if phase=='phase2' and report.get('package_set','all')!='all':errors.append('incomplete-package-set')
    if report.get('fingerprint')!=fingerprint or report.get('source_after')!=fingerprint:errors.append('stale-build-fingerprint')
    if report.get('generation_compiled') is not True:errors.append('generation-not-compiled')
    if report.get('profile') not in ('smoke','clean'):errors.append('build-profile')
    if report.get('architecture')!='ARM64' or report.get('scripting_backend')!='IL2CPP':errors.append('build-platform')
    if not report.get('build_id'):errors.append('missing-build-id')
    if report.get('profile')=='clean':
        names=report.get('player_assemblies',[])
        if phase=='phase2' and not PHASE2_ASSEMBLIES.issubset(set(names)):errors.append('phase2-runtime-assemblies')
        if not names or any(any(s in n.lower() for s in ('test','acceptance','verification','nunit')) for n in names):errors.append('clean-player-assemblies')
    return errors

def assess_matrix(rows):
    errors=[]
    for v,b,t in combinations():
        matches=[r for r in rows if (r.get('editor'),r.get('backend'),r.get('target'))==(v,b,t)]
        if len(matches)!=1 or matches[0].get('status')!='PASS':errors.append('missing-or-failed:'+v+'/'+b+'/'+t)
    return errors

def release(root,phase="phase1"):
    import run
    from locking import guard,VerificationBusy
    from registry import registered
    from evidence import verify
    from build import source_fingerprint,verify_files,latest_attempt,build_identity,validate_build_report
    from smoke import validate_player_report
    from datetime import datetime,timezone
    items=[]
    try:
        with guard(run.A,shared=True):
            records,registration_errors=registered(run.A)
            matrix=json.loads((run.C/'matrix.json').read_text())
            for ver,(short,_,_) in run.VERSIONS.items():
                for backend in ('legacy','input'):
                    snap=run.snapshot(ver,backend,run.V/'Projects'/f'{short}-{backend}')
                    for mode in ('EditMode','PlayMode'):
                        found=[r for r in records if (r[2].get('editor'),r[2].get('backend'),r[2].get('mode'),r[2].get('suite'))==(ver,backend,mode,'all')]
                        errors=list(registration_errors)+snap['errors'];folder=None
                        if phase=='phase2':
                            from runtime import EXTENSIONS
                            if not set(EXTENSIONS).issubset(snap['manifest']['dependencies']):errors.append('missing-phase2-packages')
                            expected=set(EXTENSIONS.values()) if mode=='EditMode' else {'flow-integration','flow-samples'}
                            if any(not matrix[ver][backend][mode].get(suite) for suite in expected):errors.append('missing-phase2-suites')
                        if not found:errors.append('missing-full-suite')
                        else:
                            _,_,registration,folder=max(found,key=lambda r:(r[0],r[1]))
                            errors+=registration.get('registration_errors',[])
                            try:
                                record=json.loads((folder/'summary.json').read_text())
                                required=set(sum(run.applicable_suites(matrix[ver][backend][mode],snap['manifest']).values(),[]))
                                errors+=verify(record,folder,required,snap,acceptance=True)['errors']
                            except (OSError,ValueError,KeyError) as e:errors.append(str(e))
                        items.append(dict(kind='Editor',editor=ver,backend=backend,mode=mode,status='FAIL' if errors else 'PASS',errors=errors,evidence=str(folder)))
            fingerprint=source_fingerprint()
            for ver,backend,target in combinations():
                for profile in (('smoke','clean') if target=='Android' and backend=='input' else ('smoke',)):
                    folder=latest_attempt(run.A/'mobile',ver,backend,target,profile,phase)
                    errors=[]
                    if folder is None:errors=['missing-build']
                    else:
                        try:
                            build=json.loads((folder/'summary.json').read_text());errors+=assess_build(build,fingerprint,phase);errors+=verify_files(folder,build)
                            request=json.loads((folder/'request.json').read_text())
                            if request.get('build_id')!=build.get('build_id') or build_identity(request)!=build.get('build_id'):errors.append('request-build-id')
                            if (build.get('editor'),build.get('backend'),build.get('target'),build.get('profile'))!=(ver,backend,target,profile):errors.append('build-context')
                            from evidence import sha256
                            if sha256(folder.parent/'inventory.json')!=build.get('inventory_sha256'):errors.append('build-inventory-hash')
                            if build.get('status')=='PASS':errors+=validate_build_report(json.loads((folder/'build-report.json').read_text()),build)
                            if profile=='smoke':
                                mobile=json.loads((folder/'device-summary.json').read_text())
                                if any(mobile.get(k)!=build.get(k) for k in ('editor','backend','target','profile')):errors.append('device-context')
                                errors+=assess_mobile(mobile,build['build_id'],fingerprint,phase);errors+=verify_files(folder,mobile,'device-files');errors+=validate_player_report(json.loads((folder/'device/player-report.json').read_text()),mobile)
                        except (OSError,ValueError,KeyError,TypeError) as e:errors.append('missing-or-invalid-evidence:'+str(e))
                    items.append(dict(kind=profile,editor=ver,backend=backend,target=target,status='FAIL' if errors else 'PASS',errors=errors,evidence=str(folder)))
            # Independent G0 re-parses raw prototype runs at the current host fingerprints.
            from gates import _preflight_locked
            g0code=_preflight_locked(root)
            items.append(dict(kind='G0',status='PASS' if g0code==0 else 'FAIL',errors=[] if g0code==0 else ['G0-not-pass']))
            from play_reset import validate_report,validate_automatic_reports
            from evidence import sha256
            paths=sorted(run.A.glob('*-play-reset-*/summary.json'));reload_errors=[]
            if not paths:reload_errors=['missing-reload-matrix']
            else:
                try:
                    rows=json.loads(paths[-1].read_text())
                    expected={(v,b,o) for v in run.VERSIONS for b in ('legacy','input') for o in range(4)}
                    if len(rows)!=16 or {(r['editor'],r['backend'],r['option']) for r in rows}!=expected:reload_errors.append('incomplete-reload-matrix')
                    for row in rows:
                        folder=Path(row['evidence']);ver=row['editor'];backend=row['backend'];snap=run.snapshot(ver,backend,run.V/'Projects'/f'{run.VERSIONS[ver][0]}-{backend}')
                        if row['status']!='PASS' or row['fingerprint']!=snap['fingerprint']:reload_errors.append('stale-or-failed-reload')
                        if sha256(folder/'evidence-index.json')!=row['evidence_index_sha256']:reload_errors.append('reload-index-hash')
                        index=json.loads((folder/'evidence-index.json').read_text())
                        reload_errors+=verify_files(folder,{'files':index['files']})
                        reload_errors+=validate_report(json.loads((folder/'play-reset.json').read_text()),row['option'])+validate_automatic_reports(folder)
                except (OSError,ValueError,KeyError,TypeError) as e:reload_errors.append('invalid-reload:'+str(e))
            items.append(dict(kind='Reload',status='FAIL' if reload_errors else 'PASS',errors=reload_errors,evidence=str(paths[-1]) if paths else None))
            for ver in run.VERSIONS:
                for backend in ('legacy','input'):
                    folder=latest_attempt(run.A/'install-checks',ver,backend,'Android','smoke',phase);errors=[]
                    if folder is None:errors=['missing-clean-git-install']
                    else:
                        try:
                            r=json.loads((folder/'summary.json').read_text());errors+=verify_files(folder,r)
                            if r.get('install_status')!='PASS' or not r.get('generation_compiled') or r.get('fingerprint')!=fingerprint or r.get('source_after')!=fingerprint:errors.append('stale-or-uncompiled-git-install')
                            dependencies=json.loads((folder/'packages-lock.json').read_text())['dependencies']
                            names=['com.wulato.uikit']+(['com.wulato.uikit.input-system'] if backend=='input' else [])+(list(__import__('runtime').EXTENSIONS) if phase=='phase2' else [])
                            for name in names:
                                if dependencies[name].get('source')!='git' or r['git_revision'] not in dependencies[name].get('version',''):errors.append('unpinned-install:'+name)
                        except (OSError,ValueError,KeyError,TypeError) as e:errors.append('invalid-install:'+str(e))
                    items.append(dict(kind='GitInstall',editor=ver,backend=backend,status='FAIL' if errors else 'PASS',errors=errors,evidence=str(folder)))
            status='PASS' if items and all(x['status']=='PASS' for x in items) else 'FAIL'
            out=run.A/('release-'+datetime.now(timezone.utc).strftime('%Y%m%dT%H%M%S%f'))/'release-report.json'
            run.dump(out,dict(schema=1,phase=phase,status=status,items=items,fingerprint=fingerprint))
            print('RELEASE',status,'EVIDENCE',out,flush=True)
            return int(status!='PASS')
    except VerificationBusy as e:print('FAIL',e);return 1
