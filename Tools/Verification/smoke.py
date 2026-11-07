"""Install and launch the exact smoke build; archive raw transport and Player report."""
import json,os,re,shutil,subprocess,time,uuid
from pathlib import Path
from build import command,source_fingerprint,file_index,verify_files
from release_gate import assess_mobile,assess_build
ADB=os.environ.get('UIKIT_ADB','/Users/tian/Library/Android/sdk/platform-tools/adb')

def ios_device_candidates(devices,identifier):
    return [d for d in devices if identifier in (d.get('identifier'),d.get('hardwareProperties',{}).get('udid')) and d.get('connectionProperties',{}).get('pairingState')=='paired' and d.get('connectionProperties',{}).get('tunnelState')!='unavailable']

def android_report_commands(prefix,remote):
    # adb shell has external-storage access even before the app creates its directory;
    # run-as can fail on that FUSE path despite the package being debuggable.
    return prefix+['shell','rm','-f',remote],prefix+['shell','cat',remote]

def android_report_path(package,user):
    # Unity resolves persistentDataPath using Android Context.getExternalFilesDir.
    if not re.fullmatch(r'[A-Za-z][A-Za-z0-9_.]+',package) or not user.isdigit():raise ValueError('invalid-Android-report-context')
    return '/storage/emulated/'+user+'/Android/data/'+package+'/files/uikit-smoke.json'

def validate_player_report(raw,summary):
    return ['raw-player-report:'+key for key in ('build_id','status','cases','phase','performance') if raw.get(key)!=summary.get(key)]

def ios_signing_project(export,out):
    # Xcode rewrites IL2CPP profiling output while compiling. Keep build evidence immutable.
    destination=out/('SigningProject-'+uuid.uuid4().hex)
    shutil.copytree(export,destination)
    return destination/'Unity-iPhone.xcodeproj'

def reset_ios_report(device,package,out):
    """Verify a unique pending marker before launch; old PASS must never be reused."""
    marker=dict(status='PENDING',nonce=uuid.uuid4().hex)
    pending=out/'pending-report.json';pending.write_text(json.dumps(marker))
    common=['xcrun','devicectl','device','copy']
    domain=['--device',device,'--domain-type','appDataContainer','--domain-identifier',package]
    remote='Documents/uikit-smoke.json';verified=out/'reset-verified.json'
    if command(common+['to',*domain,'--source',str(pending),'--destination',remote],out/'reset-write.log',30):raise ValueError('cannot-reset-iOS-device-report')
    if command(common+['from',*domain,'--source',remote,'--destination',str(verified)],out/'reset-read.log',30):raise ValueError('cannot-verify-iOS-device-report-reset')
    if json.loads(verified.read_text())!=marker:raise ValueError('iOS-device-report-reset-mismatch')

def smoke(args):
    import run
    from locking import guard,VerificationBusy
    try:
        with guard(run.A):
            folder=Path(args.build).resolve();build=json.loads((folder/'summary.json').read_text())
            errors=assess_build(build,source_fingerprint(),build.get('phase','phase1'))+verify_files(folder,build)
            if build.get('profile')!='smoke':errors.append('requires-smoke-build')
            if errors:print('FAIL',errors);return 1
            out=folder/'device';out.mkdir(exist_ok=True)
            result=dict(phase=build.get('phase','phase1'),build_id=build['build_id'],fingerprint=build['fingerprint'],editor=build['editor'],backend=build['backend'],target=build['target'],profile='smoke',architecture='ARM64',scripting_backend='IL2CPP',compiled=False,installed=False,launched=False,physical_device=False,device=args.device,cases=[],status='FAIL',errors=[])
            try:
                if build['target']=='Android':
                    prefix=[ADB,'-s',args.device];listing=subprocess.check_output([ADB,'devices','-l'],text=True);(out/'devices.txt').write_text(listing)
                    result['physical_device']=any(line.split()[0]==args.device and ' device ' in (' '+line+' ') and not args.device.startswith('emulator-') for line in listing.splitlines()[1:] if line.split())
                    if not result['physical_device']:
                        result['status']='BLOCKED';raise ValueError('physical-Android-device-not-connected')
                    result['compiled']=True
                    apk=Path(build['output']);result['installed']=command(prefix+['install','-r',str(apk)],out/'install.log',args.timeout)==0
                    if not result['installed']:raise ValueError('install-failed')
                    package=build['application_id'];user=subprocess.check_output(prefix+['shell','am','get-current-user'],text=True).strip();remote=android_report_path(package,user)
                    clear,read=android_report_commands(prefix,remote)
                    command(prefix+['shell','am','force-stop',package],out/'force-stop.log')
                    if command(clear,out/'clear-old-report.log')!=0:raise ValueError('cannot-clear-old-device-report')
                    result['launched']=command(prefix+['shell','monkey','-p',package,'-c','android.intent.category.LAUNCHER','1'],out/'launch.log')==0
                    deadline=time.monotonic()+args.timeout
                    while time.monotonic()<deadline:
                        raw=subprocess.run(read,capture_output=True,text=True)
                        if raw.returncode==0:
                            try:
                                report=json.loads(raw.stdout);(out/'player-report.json').write_text(raw.stdout)
                                if report.get('build_id')!=build['build_id']:raise ValueError('player-build-id-mismatch')
                                result.update(cases=report.get('cases',[]),status=report.get('status','FAIL'),performance=report.get('performance'));break
                            except json.JSONDecodeError:pass
                        time.sleep(1)
                    else:raise ValueError('device-report-timeout (TouchClose requires a physical tap)')
                    command(prefix+['logcat','-d','-s','Unity'],out/'player.log',30)
                else:
                    team=os.environ.get('UIKIT_IOS_TEAM')
                    if not team:
                        result['status']='BLOCKED';raise ValueError('missing-UIKIT_IOS_TEAM (signing is supplied by executor)')
                    if not team.isalnum():raise ValueError('invalid-team-id')
                    rc=command(['xcrun','devicectl','list','devices','--json-output',str(out/'devices.json')],out/'devices.log',60)
                    if rc:raise ValueError('devicectl-list-failed')
                    devices=json.loads((out/'devices.json').read_text()).get('result',{}).get('devices',[])
                    matched=ios_device_candidates(devices,args.device)
                    if not matched:
                        result['status']='BLOCKED';raise ValueError('physical-iOS-device-not-connected')
                    rc=command(['xcrun','devicectl','device','info','details','--device',args.device,'--json-output',str(out/'device-details.json')],out/'device-details.log',60)
                    if rc:
                        result['status']='BLOCKED';raise ValueError('iOS-device-details-probe-failed')
                    result['physical_device']=True;result['signing_team_id']=team
                    derived=out/'DerivedData'
                    project=ios_signing_project(Path(build['output']),out)
                    result['signing_project']=str(project.parent)
                    result['compiled']=command(['xcodebuild','-project',str(project),'-scheme','Unity-iPhone','-configuration','Release','-destination','id='+args.device,'-derivedDataPath',str(derived),'DEVELOPMENT_TEAM='+team,'CODE_SIGN_STYLE=Automatic','build'],out/'xcodebuild.log',args.timeout)==0
                    if not result['compiled']:raise ValueError('xcode-compile-sign-failed')
                    app=derived/'Build/Products/Release-iphoneos/UIKit.app'
                    apps=list((derived/'Build/Products/Release-iphoneos').glob('*.app'))
                    if len(apps)!=1:raise ValueError('missing-or-ambiguous-signed-app')
                    app=apps[0];package=build['application_id']
                    result['installed']=command(['xcrun','devicectl','device','install','app','--device',args.device,str(app)],out/'install.log',args.timeout)==0
                    if not result['installed']:raise ValueError('iOS-install-failed')
                    reset_ios_report(args.device,package,out)
                    result['launched']=command(['xcrun','devicectl','device','process','launch','--device',args.device,'--terminate-existing',package],out/'launch.log',60)==0
                    deadline=time.monotonic()+args.timeout
                    while time.monotonic()<deadline:
                        dest=out/'player-report.json'
                        rc=command(['xcrun','devicectl','device','copy','from','--device',args.device,'--domain-type','appDataContainer','--domain-identifier',package,'--source','Documents/uikit-smoke.json','--destination',str(dest)],out/'copy.log',30)
                        if rc==0 and dest.is_file():
                            try:raw=json.loads(dest.read_text())
                            except json.JSONDecodeError:
                                time.sleep(1);continue
                            if raw.get('status')=='PENDING':
                                time.sleep(1);continue
                            if raw.get('build_id')!=build['build_id']:raise ValueError('player-build-id-mismatch')
                            result.update(status=raw.get('status','FAIL'),cases=raw.get('cases',[]),performance=raw.get('performance'));break
                        time.sleep(1)
                    else:raise ValueError('iOS-device-report-timeout (TouchClose requires physical tap)')
                result['errors']+=assess_mobile(result,build['build_id'],source_fingerprint(),build.get('phase','phase1'))
                result['errors']+=validate_player_report(json.loads((out/'player-report.json').read_text()),result)
            except (OSError,ValueError,subprocess.SubprocessError) as e:
                if isinstance(e,FileNotFoundError):result['status']='BLOCKED'
                result['errors'].append(str(e))
            if result['errors'] and result['status']!='BLOCKED':result['status']='FAIL'
            result['device-files']={'device/'+k:v for k,v in file_index(out).items()};run.dump(folder/'device-summary.json',result)
            print(result['status'],result['errors'],'EVIDENCE',folder,flush=True);return int(result['status']!='PASS')
    except (VerificationBusy,OSError,ValueError,KeyError) as e:print('FAIL',e);return 1
