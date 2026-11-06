"""Stage approved, pinned R3 SDK inputs in ignored verification hosts only; no downloader or global installation."""
import argparse,hashlib,json,re,shutil,tarfile,uuid,zipfile
from pathlib import Path,PurePosixPath
ROOT=Path(__file__).resolve().parents[2]
DEFINE='WULATO_UIKIT_R3'
SOURCE='R3-1.3.1.tar.gz'
SOURCE_SHA='698a352293ffafa923f37566645446e48a99167c2ff7862a86b7307d45f790c4'
# Prefer netstandard2.1 matching Unity 2022.3; its built-in ValueTask shims satisfy platform references.
FILES={
 'r3.1.3.1.nupkg':('1bfc054cb16e86e78c847ad0ef9154c285a9bfb15331d9c2fa9e76ab58d3fa9a','lib/netstandard2.1/R3.dll'),
 'microsoft.bcl.timeprovider.8.0.0.nupkg':('7c1bc34970d22183334daf3e03ef7c2aa8445d83fa135ef02e8f943709727f85','lib/netstandard2.0/Microsoft.Bcl.TimeProvider.dll'),
 'microsoft.bcl.asyncinterfaces.6.0.0.nupkg':('e3df87fe2170a7e01f0880af59caa8f6eb380b3c40a4f282dfb43912aaf0f895','lib/netstandard2.1/Microsoft.Bcl.AsyncInterfaces.dll'),
 'system.threading.channels.8.0.0.nupkg':('7394d8a0b3570cbae82c83e795fc8c1e4ee767bd1001c91cfdced5d7df580a18','lib/netstandard2.1/System.Threading.Channels.dll'),
 'system.runtime.compilerservices.unsafe.6.0.0.nupkg':('6c41b53e70e9eee298cff3a02ce5acdd15b04125589be0273f0566026720a762','lib/netstandard2.0/System.Runtime.CompilerServices.Unsafe.dll'),
 'system.componentmodel.annotations.5.0.0.nupkg':('d29493d541e0a5e13ac49ad87f947e53b0302251f7ad50b74a982e8a523f3008','lib/netstandard2.1/System.ComponentModel.Annotations.dll')}
def sha(data):return hashlib.sha256(data).hexdigest()
def integration_enabled(host):
 text=(Path(host)/'ProjectSettings/ProjectSettings.asset').read_text();section=re.search(r'^  scriptingDefineSymbols:\n((?:    [^\n]*\n)+)',text,re.M)
 match=re.search(r'^    Standalone:([^\n]*)$',section.group(1),re.M) if section else None
 return bool(match and DEFINE in match.group(1).strip().split(';'))
def stage(directory,host,enabled=True):
 directory=Path(directory);host=Path(host).resolve()
 if not host.is_relative_to((ROOT/'Artifacts/uikit/ci-selected/hosts').resolve()):raise ValueError('R3 staging is restricted to ignored verification hosts')
 settings=host/'ProjectSettings/ProjectSettings.asset';text=settings.read_text();section=re.search(r'^  scriptingDefineSymbols:\n((?:    [^\n]*\n)+)',text,re.M)
 match=re.search(r'^(    Standalone:)([^\n]*)$',section.group(1),re.M) if section else None
 if not match:raise ValueError('missing-standalone-defines')
 source=directory/SOURCE
 if sha(source.read_bytes())!=SOURCE_SHA:raise ValueError('unexpected-r3-source-hash')
 payload={};packages={}
 for filename,(digest,member) in FILES.items():
  path=directory/filename
  if sha(path.read_bytes())!=digest:raise ValueError('unexpected-nuget-hash:'+filename)
  with zipfile.ZipFile(path) as package:payload['Plugins/'+PurePosixPath(member).name]=package.read(member)
  packages[filename]=dict(sha256=digest,asset=member)
 prefix='R3-1.3.1/src/R3.Unity/Assets/R3.Unity/Runtime/'
 with tarfile.open(source) as package:
  for entry in package.getmembers():
   if not entry.isfile() or not entry.name.startswith(prefix):continue
   relative=PurePosixPath(entry.name[len(prefix):])
   if relative.is_absolute() or '..' in relative.parts:raise ValueError('unsafe-sdk-member')
   payload['R3.Unity/Runtime/'+str(relative)]=package.extractfile(entry).read()
 target=host/'Assets/UIKitR3SDK'
 for name,data in payload.items():
  path=target/name
  if path.exists() and path.read_bytes()!=data:raise ValueError('refuse-overwriting-different-sdk:'+name)
 for name,data in payload.items():
  path=target/name;path.parent.mkdir(parents=True,exist_ok=True);path.write_bytes(data)
  if path.suffix=='.dll' and not Path(str(path)+'.meta').exists():
   Path(str(path)+'.meta').write_text('fileFormatVersion: 2\nguid: '+uuid.uuid5(uuid.NAMESPACE_URL,'wulato-uikit-r3/'+name).hex+'\nPluginImporter:\n  serializedVersion: 2\n  iconMap: {}\n  executionOrder: {}\n  isPreloaded: 0\n  isOverridable: 0\n  isExplicitlyReferenced: 0\n  validateReferences: 1\n  platformData:\n  - first:\n      Any:\n    second:\n      enabled: 1\n      settings: {}\n  userData:\n  assetBundleName:\n  assetBundleVariant:\n')
 sample=ROOT/'Packages/com.wulato.uikit/Samples~/R3'
 if sample.exists():shutil.copytree(sample,host/'Assets/UIKitR3Sample',dirs_exist_ok=True)
 defines=[s for s in match.group(2).strip().split(';') if s and s!=DEFINE]
 if enabled:defines.append(DEFINE)
 start=section.start(1)+match.start();end=section.start(1)+match.end();settings.write_text(text[:start]+match.group(1)+' '+';'.join(defines)+text[end:])
 receipt=dict(version='1.3.1',source_sha256=SOURCE_SHA,source_commit='f6eed2dd4208dc4ae171c601e799e85f82aca25e',enabled=enabled,packages=packages,files={n:sha(d) for n,d in payload.items()})
 (target/'receipt.json').write_text(json.dumps(receipt,indent=2)+'\n');return receipt
if __name__=='__main__':
 p=argparse.ArgumentParser();p.add_argument('--directory',required=True);p.add_argument('--host',required=True);p.add_argument('--disabled',action='store_true');a=p.parse_args();r=stage(a.directory,a.host,not a.disabled);print(json.dumps(dict(version=r['version'],enabled=r['enabled'],packages=len(r['packages']),files=len(r['files'])),indent=2))
