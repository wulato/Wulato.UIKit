"""Stage an already-authorized official SDK only in ignored verification hosts. Never downloads or edits consumers."""
import argparse,hashlib,json,re,zipfile
from pathlib import Path
ROOT=Path(__file__).resolve().parents[2]
VERSION='1.3.045'
ARCHIVE_SHA256='32eba5c48520a541775d00cb396e18975b83587ba79175dbb878d753725ba5d9'
URL='https://dotween.demigiant.com/downloads/DOTween_1_3_045.zip'
DEFINE='WULATO_UIKIT_DOTWEEN'
def integration_enabled(host):
    text=(Path(host)/'ProjectSettings/ProjectSettings.asset').read_text()
    section=re.search(r'^  scriptingDefineSymbols:\n((?:    [^\n]*\n)+)',text,re.M)
    match=re.search(r'^    Standalone:([^\n]*)$',section.group(1),re.M) if section else None
    return bool(match and DEFINE in match.group(1).strip().split(';'))
def stage(archive,host,enabled=True):
    host=Path(host).resolve();allowed=(ROOT/'Artifacts/uikit/ci-selected/hosts').resolve()
    if not host.is_relative_to(allowed):raise ValueError('SDK staging is restricted to ignored verification hosts')
    settings=host/'ProjectSettings/ProjectSettings.asset'
    text=settings.read_text();section=re.search(r'^  scriptingDefineSymbols:\n((?:    [^\n]*\n)+)',text,re.M)
    match=re.search(r'^(    Standalone:)([^\n]*)$',section.group(1),re.M) if section else None
    if not match:raise ValueError('missing-standalone-defines')
    if hashlib.sha256(Path(archive).read_bytes()).hexdigest()!=ARCHIVE_SHA256:raise ValueError('unexpected-sdk-archive-hash')
    with zipfile.ZipFile(archive) as sdk:
        data=sdk.read('DOTween/DOTween.dll')
        module_bytes={name:sdk.read('DOTween/Modules/'+name) for name in ['DOTweenModuleUtils.cs','DOTweenModulePhysics.cs','DOTweenModulePhysics2D.cs']}
    folder=host/'Assets/UIKitDOTweenSDK';folder.mkdir(parents=True,exist_ok=True)
    dll=folder/'DOTween.dll'
    if dll.exists() and dll.read_bytes()!=data:raise ValueError('refuse-overwriting-different-sdk')
    dll.write_bytes(data)
    modules=folder/'Modules';modules.mkdir(exist_ok=True)
    for name,data_module in module_bytes.items():(modules/name).write_bytes(data_module)
    (modules/'DOTween.Modules.asmdef').write_text(json.dumps(dict(name='DOTween.Modules',references=[],overrideReferences=True,precompiledReferences=['DOTween.dll']),indent=2)+'\n')
    # Only asmdefs explicitly referencing this DLL see it. No SDK editor setup hooks or modules are imported.
    Path(str(dll)+'.meta').write_text('''fileFormatVersion: 2
guid: 62a26ca449b34f2c80fc6cb3c47d3f11
PluginImporter:
  serializedVersion: 2
  iconMap: {}
  executionOrder: {}
  defineConstraints: []
  isPreloaded: 0
  isOverridable: 0
  isExplicitlyReferenced: 1
  validateReferences: 1
  platformData:
  - first:
      Any:
    second:
      enabled: 1
      settings: {}
  userData:
  assetBundleName:
  assetBundleVariant:
''')
    defines=[s for s in match.group(2).strip().split(';') if s and s!=DEFINE]
    if enabled:defines.append(DEFINE)
    start=section.start(1)+match.start();end=section.start(1)+match.end()
    settings.write_text(text[:start]+match.group(1)+' '+';'.join(defines)+text[end:])
    receipt=dict(version=VERSION,url=URL,archive_sha256=hashlib.sha256(Path(archive).read_bytes()).hexdigest(),dll_sha256=hashlib.sha256(data).hexdigest(),enabled=enabled,modules={name:hashlib.sha256(blob).hexdigest() for name,blob in module_bytes.items()})
    import shutil
    sample=ROOT/'Packages/com.wulato.uikit/Samples~/DOTween'
    if sample.is_dir():shutil.copytree(sample,host/'Assets/UIKitDOTweenSample',dirs_exist_ok=True)
    (folder/'receipt.json').write_text(json.dumps(receipt,indent=2)+'\n');return receipt
if __name__=='__main__':
    p=argparse.ArgumentParser();p.add_argument('--archive',required=True);p.add_argument('--host',required=True);p.add_argument('--disabled',action='store_true');a=p.parse_args()
    print(json.dumps(stage(a.archive,a.host,not a.disabled),indent=2))
