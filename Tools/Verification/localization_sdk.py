"""Stage the optional sample in an ignored host. SDK installation needs separate authorization."""
import argparse,json,re,shutil
from pathlib import Path
ROOT=Path(__file__).resolve().parents[2]
DEFINE='WULATO_UIKIT_LOCALIZATION'
def integration_enabled(host):
 text=(Path(host)/'ProjectSettings/ProjectSettings.asset').read_text()
 section=re.search(r'^  scriptingDefineSymbols:\n((?:    [^\n]*\n)+)',text,re.M)
 match=re.search(r'^    Standalone:([^\n]*)$',section.group(1),re.M) if section else None
 deps=json.loads((Path(host)/'Packages/manifest.json').read_text())['dependencies']
 return bool(match and DEFINE in match.group(1).strip().split(';') and 'com.unity.localization' in deps)
def stage(host,install_sdk=False,enabled=False):
 host=Path(host).resolve()
 if not host.is_relative_to((ROOT/'Artifacts/uikit/ci-selected/hosts').resolve()):raise ValueError('Localization staging is restricted to ignored verification hosts')
 manifest=host/'Packages/manifest.json';data=json.loads(manifest.read_text())
 if install_sdk:
  # Official registry and compatible pinned Addressables closure; no global installation.
  for name,version in {'com.unity.localization':'1.4.5','com.unity.addressables':'1.21.21','com.unity.scriptablebuildpipeline':'1.21.25'}.items():
   old=data['dependencies'].get(name)
   if old and old!=version:raise ValueError('refuse-overwriting-different-sdk:'+name)
   data['dependencies'][name]=version
 settings=host/'ProjectSettings/ProjectSettings.asset';text=settings.read_text()
 section=re.search(r'^  scriptingDefineSymbols:\n((?:    [^\n]*\n)+)',text,re.M)
 match=re.search(r'^(    Standalone:)([^\n]*)$',section.group(1),re.M) if section else None
 if not match:raise ValueError('missing-standalone-defines')
 if enabled and 'com.unity.localization' not in data['dependencies']:raise ValueError('enable-requires-localization-sdk')
 defines=[d for d in match.group(2).strip().split(';') if d and d!=DEFINE]
 if enabled:defines.append(DEFINE)
 shutil.copytree(ROOT/'Packages/com.wulato.uikit/Samples~/Localization',host/'Assets/UIKitLocalizationSample',dirs_exist_ok=True)
 manifest.write_text(json.dumps(data,indent=2)+'\n')
 start=section.start(1)+match.start();end=section.start(1)+match.end()
 settings.write_text(text[:start]+match.group(1)+' '+';'.join(defines)+text[end:])
if __name__=='__main__':
 p=argparse.ArgumentParser();p.add_argument('--host',required=True);p.add_argument('--install-sdk',action='store_true',help='Only after separate SDK installation authorization');p.add_argument('--enabled',action='store_true');a=p.parse_args();stage(a.host,a.install_sdk,a.enabled)
