"""Stage separately authorized official FancyScrollView in ignored hosts; never download automatically."""
import argparse,hashlib,json,re,shutil,tarfile
from pathlib import Path
ROOT=Path(__file__).resolve().parents[2]
DEFINE='WULATO_UIKIT_FANCY_SCROLL'
ARCHIVE_SHA='2f3fa1a69f6bbcd8b91a7a864cfad99df4fde5a3e1e1669b418ebe99fbe79247'
COMMIT='e5d10d3e5961848f93be43c2911344750b509a88'
def sha(data):return hashlib.sha256(data).hexdigest()
def integration_enabled(host):
 text=(Path(host)/'ProjectSettings/ProjectSettings.asset').read_text();section=re.search(r'^  scriptingDefineSymbols:\n((?:    [^\n]*\n)+)',text,re.M)
 match=re.search(r'^    Standalone:([^\n]*)$',section.group(1),re.M) if section else None
 return bool(match and DEFINE in match.group(1).strip().split(';'))
def stage(archive,host,enabled=True):
 host=Path(host).resolve();archive=Path(archive)
 if not host.is_relative_to((ROOT/'Artifacts/uikit/ci-selected/hosts').resolve()):raise ValueError('Fancy staging is restricted to ignored verification hosts')
 if sha(archive.read_bytes())!=ARCHIVE_SHA:raise ValueError('unexpected-fancy-archive-hash')
 target=host/'Assets/UIKitFancySDK';payload={};prefix='FancyScrollView-1.9.0/Sources/Runtime/'
 with tarfile.open(archive) as sdk:
  for entry in sdk.getmembers():
   if not entry.isfile() or not entry.name.startswith(prefix):continue
   name=entry.name[len(prefix):]
   if '..' in Path(name).parts or Path(name).is_absolute():raise ValueError('unsafe-sdk-member')
   payload[name]=sdk.extractfile(entry).read()
 for name,data in payload.items():
  path=target/name
  if path.exists() and path.read_bytes()!=data:raise ValueError('refuse-overwriting-different-sdk:'+name)
 for name,data in payload.items():
  path=target/name;path.parent.mkdir(parents=True,exist_ok=True);path.write_bytes(data)
 sample=ROOT/'Packages/com.wulato.uikit/Samples~/FancyScrollView'
 shutil.copytree(sample,host/'Assets/UIKitFancySample',dirs_exist_ok=True)
 settings=host/'ProjectSettings/ProjectSettings.asset';text=settings.read_text();section=re.search(r'^  scriptingDefineSymbols:\n((?:    [^\n]*\n)+)',text,re.M)
 match=re.search(r'^(    Standalone:)([^\n]*)$',section.group(1),re.M) if section else None
 if not match:raise ValueError('missing-standalone-defines')
 defines=[v for v in match.group(2).strip().split(';') if v and v!=DEFINE]
 if enabled:defines.append(DEFINE)
 start=section.start(1)+match.start();end=section.start(1)+match.end();settings.write_text(text[:start]+match.group(1)+' '+';'.join(defines)+text[end:])
 receipt=dict(version='1.9.0',commit=COMMIT,archive_sha256=ARCHIVE_SHA,enabled=enabled,files={n:sha(v) for n,v in payload.items()})
 (target/'receipt.json').write_text(json.dumps(receipt,indent=2)+'\n');return receipt
if __name__=='__main__':
 p=argparse.ArgumentParser();p.add_argument('--archive',required=True);p.add_argument('--host',required=True);p.add_argument('--disabled',action='store_true');a=p.parse_args();r=stage(a.archive,a.host,not a.disabled);print(json.dumps(dict(version=r['version'],enabled=r['enabled'],files=len(r['files']))))
