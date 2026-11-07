"""Revalidate raw evidence; cached summary status is never sufficient for G0."""
import hashlib
import json
import re
from pathlib import Path
from results import evaluate

RAW={'results.xml','editor.log','host-snapshot.json','command.json','trace.json','run.json'}

def sha256(path): return hashlib.sha256(path.read_bytes()).hexdigest()
def seal(folder):
    names=RAW|{p.name for p in folder.glob('*.trace.json')}|({'dependency-snapshot.json'} if (folder/'dependency-snapshot.json').is_file() else set())
    data={'schema':1,'files':{name:sha256(folder/name) for name in sorted(names) if (folder/name).is_file()}}
    index=folder/'evidence-index.json';index.write_text(json.dumps(data,indent=2)+'\n')
    return sha256(index)

def verify(record,folder,required,current_snapshot,acceptance=False,recorded_folder=None):
    origin=Path(recorded_folder) if recorded_folder is not None else folder
    errors=[]
    def check(condition,message):
        if not condition: errors.append(message)
    def read(name): return json.loads((folder/name).read_text())
    try:
        check(record.get('schema')==3,'unsupported-evidence-schema')
        check(record.get('status')=='PASS','summary-not-pass')
        check(record.get('mode') in ('PlayMode','EditMode') if acceptance else record.get('mode')=='PlayMode','wrong-test-mode')
        check(record.get('fingerprint')==current_snapshot['fingerprint'],'stale-fingerprint')
        check(Path(record['evidence']).resolve()==origin.resolve(),'evidence-path-mismatch')
        index=folder/'evidence-index.json'
        check(sha256(index)==record.get('evidence_index_sha256'),'index-hash-mismatch')
        manifest=read('evidence-index.json');check(manifest.get('schema')==1,'index-schema')
        names=RAW|{p.name for p in folder.glob('*.trace.json')}|({'dependency-snapshot.json'} if (folder/'dependency-snapshot.json').is_file() else set())
        check(set(manifest['files'])==names,'artifact-set-mismatch')
        for name,expected_hash in manifest['files'].items():
            if Path(name).name!=name:errors.append('unsafe-artifact-name');continue
            check(sha256(folder/name)==expected_hash,'artifact-hash:'+name)
        snapshot=read('host-snapshot.json')
        for key in ('editor','backend','host','fingerprint','manifest','lock','settings','sources'):
            check(snapshot.get(key)==current_snapshot.get(key),'snapshot-mismatch:'+key)
        check(not snapshot.get('errors'),'snapshot-errors')
        execution=read('run.json')
        for key in ('run_id','editor','backend','mode','suite','batch_id','batch_sha256'):
            check(execution.get(key)==record.get(key),'execution-mismatch:'+key)
        check(execution.get('schema')==3 and execution.get('state')=='completed','incomplete-run')
        check(execution.get('returncode')==0 and execution.get('timed_out') is False,'execution-failed')
        check(execution.get('source_before')==record['fingerprint']==execution.get('source_after'),'run-source-changed')
        check(execution.get('actual_editor')==record['editor'],'execution-editor-mismatch')
        from registry import validate_inventory
        batch_path=folder.parent/'batch.json';batch=validate_inventory(batch_path)
        check(batch.get('serialization')=='exclusive-batch','unserialized-verification-batch')
        check(batch['batch_id']==record.get('batch_id'),'evidence-batch-id')
        check(sha256(batch_path)==record.get('batch_sha256'),'evidence-batch-hash')
        entries=[e for e in batch['attempts'] if e['run_id']==record['run_id']]
        check(len(entries)==1,'missing-or-duplicate-inventory-entry')
        for entry in entries:
            check(entry['folder']==folder.name,'inventory-folder-mismatch')
            for key in ('editor','backend','mode','suite'):check(entry[key]==record[key],'inventory-context:'+key)
        command=read('command.json')
        def arg(name): return command[command.index(name)+1]
        check('-runTests' in command and '-batchmode' in command and '-quit' not in command,'not-a-test-command')
        check(arg('-projectPath')==current_snapshot['host'],'command-host-mismatch')
        check(arg('-testPlatform')==record['mode'],'command-mode-mismatch')
        check(Path(arg('-testResults')).resolve()==(origin/'results.xml').resolve(),'command-xml-mismatch')
        check(Path(arg('-logFile')).resolve()==(origin/'editor.log').resolve(),'command-log-mismatch')
        if record['suite']=='all':check('-testCategory' not in command,'unexpected-category')
        else:check(arg('-testCategory')==record['suite'].replace('-','_'),'command-suite-mismatch')
        log=(folder/'editor.log').read_text(errors='replace')
        actual=re.search(r'(?:Unity Editor version:|Initialize engine version:)\s*(\d+\.\d+\.\w+)',log)
        check(actual is not None and actual.group(1)==record['editor'],'raw-editor-mismatch')
        parsed=evaluate(folder/'results.xml',log,required,execution.get('returncode',-1),execution.get('timed_out',True))
        errors.extend(parsed['errors'])
        check(parsed['cases']==record.get('cases'),'summary-cases-mismatch')
        check(parsed['count']==record.get('count'),'summary-count-mismatch')
        traces=read('trace.json');actual_traces={p.name:json.loads(p.read_text()) for p in folder.glob('*.trace.json')}
        check(traces==actual_traces,'trace-aggregate-mismatch')
        if acceptance and record.get('mode')=='PlayMode':
            for fullname in required:
                name=fullname.rsplit('.',1)[-1]
                check(any(k==name+'.trace.json' or k.startswith(name+'-') for k in traces),'missing-case-trace:'+name)
        for fullname in (required if not acceptance else []):
            name=fullname.rsplit('.',1)[-1]
            files=[name+'.trace.json'] if '.InitializationOrderTests.' in fullname else [name+'-'+owner+'.trace.json' for owner in ('Owner0','Owner1')]
            for filename in files:check(filename in traces,'missing-case-trace:'+filename)
    except (OSError,ValueError,KeyError,TypeError,IndexError) as e:
        errors.append('unreadable-evidence:'+str(e))
    return {'status':'FAIL' if errors else 'PASS','errors':errors}
