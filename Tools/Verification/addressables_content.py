"""Real Addressables bundle evidence, bound to the source and exact editor dependency matrix."""
import hashlib,json
from pathlib import Path
from editor_targets import PROFILES
VERSIONS={v:(p['addressables'],p['sbp']) for v,p in PROFILES.items()}
def source_fingerprint(host):
    import run
    files={}
    for p in (host/'Assets').rglob('*'):
        if p.is_file() and not p.relative_to(host/'Assets').as_posix().startswith('AddressableAssetsData/OSX/'):
            files['Assets/'+p.relative_to(host/'Assets').as_posix()]=p
    for name,root in run.package_sources(host).items():
        for p in (root/'Runtime').rglob('*'):
            if p.is_file():files[name+'/'+p.relative_to(root).as_posix()]=p
        files[name+'/package.json']=root/'package.json'
    digest=hashlib.sha256()
    for name,p in sorted(files.items()):
        content=p.read_bytes();digest.update(name.encode()+b'\0'+hashlib.sha256(content).digest())
    return digest.hexdigest()
def seal_content(host,out,editor):
    from evidence import sha256
    actual=json.loads((out/'addressables-content.json').read_text())
    addressables,sbp=VERSIONS[editor]
    if (actual.get('editor'),actual.get('version'),actual.get('sbpVersion'),actual.get('target'),actual.get('builderType'))!=(editor,addressables,sbp,'StandaloneOSX','UnityEditor.AddressableAssets.Build.DataBuilders.BuildScriptPackedPlayMode'):
        raise ValueError('addressables-content-identity')
    folder=host/'Library/com.unity.addressables/aa'
    files={p.relative_to(host).as_posix():sha256(p) for p in folder.rglob('*') if p.is_file()}
    if not any(name.endswith('.bundle') for name in files):raise ValueError('addressables-no-real-bundle')
    record=dict(schema=1,editor=editor,addressables=addressables,sbp=sbp,target='StandaloneOSX',source=source_fingerprint(host),files=files)
    text=json.dumps(record,indent=2)+'\n';(host/'AddressablesContent.json').write_text(text);(out/'addressables-content-evidence.json').write_text(text)
    return record

def verify_content(host,editor):
    from evidence import sha256
    try:
        record=json.loads((host/'AddressablesContent.json').read_text());errors=[]
        addressables,sbp=VERSIONS[editor]
        if (record.get('schema'),record.get('editor'),record.get('addressables'),record.get('sbp'),record.get('target'))!=(1,editor,addressables,sbp,'StandaloneOSX'):errors.append('addressables-content-identity')
        if record.get('source')!=source_fingerprint(host):errors.append('addressables-stale-content-source')
        files=record.get('files',{})
        if not files or not any(n.endswith('.bundle') for n in files):errors.append('addressables-no-real-bundle')
        root=(host/'Library/com.unity.addressables/aa').resolve()
        for name,digest in files.items():
            p=host/name
            if not p.resolve().is_relative_to(root) or not p.is_file() or sha256(p)!=digest:errors.append('addressables-content-file:'+name)
        return errors
    except (OSError,ValueError,KeyError,TypeError) as error:return ['addressables-content-missing-or-invalid:'+str(error)]
