"""Independent Editor entry/exit verification: all four reload settings, two actual plays each."""
import datetime,json,os,re,subprocess,uuid
from pathlib import Path
from evidence import seal,sha256
from locking import guard

def validate_report(data,option):
    if not isinstance(data,dict):return ['play-reset:not-object']
    errors=[]
    expected={'schema':1,'status':'PASS','completed':2,'domainReload':not bool(option&1),'sceneReload':not bool(option&2),'activeContexts':0,'oldLocks':0,'subscriptions':0}
    for key,value in expected.items():
        if type(data.get(key)) is not type(value) or data.get(key)!=value:errors.append('play-reset:'+key)
    return errors

def validate_automatic_reports(out):
    errors=[]
    for cycle in range(2):
        try:
            data=json.loads((out/('automatic-exit-'+str(cycle)+'.json')).read_text())
            if not isinstance(data,dict):raise ValueError('not-object')
            for key,value in dict(status='PASS',activeContexts=0,oldLocks=0,subscriptions=0).items():
                if type(data.get(key)) is not type(value) or data.get(key)!=value:errors.append('automatic-exit:'+str(cycle)+':'+key)
        except (OSError,ValueError,TypeError):errors.append('automatic-exit:'+str(cycle)+':missing-or-invalid')
    return errors

def seal_reset(out):
    seal(out)
    index=out/'evidence-index.json'
    manifest=json.loads(index.read_text())
    for name in ('play-reset.json','automatic-exit-0.json','automatic-exit-1.json','save-cancel.json'):
        if (out/name).is_file():manifest['files'][name]=sha256(out/name)
    index.write_text(json.dumps(manifest,indent=2)+'\n')
    return sha256(index)

def reload_options(scope,backend):
    return [0,3] if scope in ('context-host','preview') and backend=='input' else [0,1,2,3]

def run_reset(args):
    import run as runner
    scope=getattr(args,'scope','integration')
    option_override=getattr(args,'reload_option',None)
    options=lambda backend:reload_options(scope,backend) if option_override is None else [option_override]
    kind='play-reset' if scope=='integration' else 'host-reset' if scope=='context-host' else 'preview-reset'
    batch=runner.A/(datetime.datetime.now(datetime.timezone.utc).strftime('%Y%m%dT%H%M%S%f')+'-'+kind+'-'+uuid.uuid4().hex[:6])
    with guard(runner.A):
        batch.mkdir(parents=True);summaries=[]
        for ver,backend,host in runner.selected(args):
            before=runner.fingerprint(host)
            for option in options(backend):
                out=batch/(host.name+'-reload-'+str(option));out.mkdir();errors=[]
                exe=Path(args.editor_path) if args.editor_path else Path('/Applications/Unity/Hub/Editor')/ver/'Unity.app/Contents/MacOS/Unity'
                runner.verify_editor(exe,ver)
                cmd=[str(exe),* ([] if getattr(args,'interactive',False) else ['-batchmode']),'-projectPath',str(host),'-logFile',str(out/'editor.log'),'-executeMethod','Wulato.UIKit.Verification.Editor.UIPlayModeResetCheck.Run']
                runner.dump(out/'command.json',cmd);env=os.environ.copy();env['UIKIT_EVIDENCE_DIR']=str(out);env['UIKIT_RESET_OPTIONS']=str(option);env['UIKIT_RESET_SCOPE']=scope;env['UIKIT_PREVIEW_ACTION']=getattr(args,'preview_action','window')
                print('START play-reset',host.name,option,flush=True)
                try:
                    result=subprocess.run(cmd,env=env,timeout=args.timeout,stdout=subprocess.PIPE,stderr=subprocess.STDOUT,text=True)
                    if result.returncode:errors.append('editor-exit:'+str(result.returncode))
                except subprocess.TimeoutExpired:errors.append('timeout')
                log=(out/'editor.log').read_text(errors='replace') if (out/'editor.log').exists() else ''
                log=re.sub(r'(Serial number assigned to: ).*',r'\1[REDACTED]',log);(out/'editor.log').write_text(log)
                actual=re.search(r'(?:Unity Editor version:|Initialize engine version:)\s*(\d+\.\d+\.\w+)',log)
                if not actual or actual.group(1)!=ver:errors.append('actual-editor')
                if re.search(r'error CS\d+|Scripts have compiler errors|AssertionException|TargetInvocationException',log):errors.append('editor-error')
                try:
                    errors+=validate_report(json.loads((out/'play-reset.json').read_text()),option)
                    errors+=validate_automatic_reports(out)
                except (OSError,ValueError,TypeError):errors.append('missing-or-invalid-report')
                if scope=='preview' and getattr(args,'preview_action','window')=='cancel':
                    try:
                        cancel=json.loads((out/'save-cancel.json').read_text())
                        if cancel!={'status':'PASS','dirtyScenePreserved':True,'playModeEntered':False,'previewCreated':False}:errors.append('save-cancel-invalid')
                    except (OSError,ValueError):errors.append('save-cancel-missing')
                snap=runner.snapshot(ver,backend,host);runner.dump(out/'host-snapshot.json',snap);errors+=snap['errors']
                if before!=snap['fingerprint']:errors.append('inputs-changed-during-run')
                summary=dict(scope=scope,status='FAIL' if errors else 'PASS',editor=ver,backend=backend,option=option,errors=errors,fingerprint=snap['fingerprint'],evidence=str(out))
                summary['evidence_index_sha256']=seal_reset(out);runner.dump(out/'summary.json',summary);summaries.append(summary)
                print(summary['status'],host.name,option,errors,flush=True)
                if errors:break
        runner.dump(batch/'summary.json',summaries);print('EVIDENCE',batch,flush=True)
        return int(len(summaries)!=sum(len(options(b)) for v,b,h in runner.selected(args)) or any(s['status']!='PASS' for s in summaries))
