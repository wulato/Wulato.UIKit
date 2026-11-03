from editor_targets import PROFILES
"""Runtime host manifest and resolved dependency validation, including the T7 adapter."""
UNITASK='https://github.com/Cysharp/UniTask.git?path=src/UniTask/Assets/Plugins/UniTask#2.5.11'
MAIN='com.wulato.uikit'
ACCEPTANCE=MAIN+'.acceptance'

def runtime_manifest(ver,backend):
    from run import VERSIONS,COMMON
    _,ugui,utf=VERSIONS[ver]
    deps={'com.unity.ugui':ugui,'com.unity.test-framework':utf,COMMON:'file:../../../Packages/'+COMMON,
          **{'com.unity.modules.'+x:'1.0.0' for x in ['ui','imgui','physics','physics2d','jsonserialize']}}
    package=COMMON+('.legacy' if backend=='legacy' else '.input-system')
    deps[package]='file:../../../Packages/'+package
    tests=[COMMON,package,MAIN,ACCEPTANCE]
    if backend=='input':
        deps['com.unity.inputsystem']=PROFILES[ver]['inputSystem'];tests.append('com.unity.inputsystem')
        deps[MAIN+'.input-system']='file:../../../../Packages/'+MAIN+'.input-system';tests.append(MAIN+'.input-system')
    deps.update({MAIN:'file:../../../../Packages/'+MAIN,ACCEPTANCE:'file:../../../Packages/'+ACCEPTANCE,'com.cysharp.unitask':UNITASK})
    from pathlib import Path
    root=Path(__file__).resolve().parents[2]
    for name in EXTENSIONS:
        if (root/'Packages'/name/'package.json').is_file():
            deps[name]='file:../../../../Packages/'+name;tests.append(name)
    if MAIN in deps and (root/'Verification/Packages/com.wulato.uikit.acceptance.flows/package.json').is_file():
        name='com.wulato.uikit.acceptance.flows';deps[name]='file:../../../Packages/'+name;tests.append(name)
    probe=MAIN+'.verification.addressables'
    if (root/'Verification/Packages'/probe/'package.json').is_file():
        deps[probe]='file:../../../Packages/'+probe;tests.append(probe)
        deps['com.unity.addressables']=PROFILES[ver]['addressables']
        deps['com.unity.scriptablebuildpipeline']=PROFILES[ver]['sbp']
    return dict(dependencies=deps,testables=tests)

def validate_dependencies(ver,manifest,lock):
    from run import VERSIONS
    errors=[];deps=manifest.get('dependencies',{});resolved=lock.get('dependencies',{})
    ugui=VERSIONS[ver][1]
    if deps.get('com.unity.ugui')!=ugui or resolved.get('com.unity.ugui',{}).get('version')!=ugui:
        errors.append('Declare and lock com.unity.ugui '+ugui+' explicitly for Unity '+ver)
    task=resolved.get('com.cysharp.unitask',{})
    if deps.get('com.cysharp.unitask')!=UNITASK or task.get('version')!=UNITASK or task.get('source')!='git':
        errors.append('Install and lock UniTask 2.5.11 using '+UNITASK)
    if MAIN+'.addressables' in deps or MAIN+'.verification.addressables' in deps:
        from addressables_content import VERSIONS as ADDRESSABLES_VERSIONS
        for name,wanted in zip(('com.unity.addressables','com.unity.scriptablebuildpipeline'),ADDRESSABLES_VERSIONS[ver]):
            if deps.get(name)!=wanted or resolved.get(name,{}).get('version')!=wanted:errors.append('addressables-resolved-version:'+name)
    return errors

def prepare_runtime_manifest(ver,backend,manifest,lock):
    """Reject incompatible existing input before preparing; never silently repair it."""
    from run import VERSIONS
    deps=manifest.get('dependencies',{});resolved=lock.get('dependencies',{})
    ugui=VERSIONS[ver][1]
    if deps.get('com.unity.ugui')!=ugui or ('com.unity.ugui' in resolved and resolved['com.unity.ugui'].get('version')!=ugui):
        raise ValueError('Declare and lock com.unity.ugui '+ugui+' explicitly before runtime preparation')
    name='com.cysharp.unitask'
    if name in deps or name in resolved:
        if deps.get(name)!=UNITASK or (name in resolved and (resolved[name].get('version')!=UNITASK or resolved[name].get('source')!='git')):
            raise ValueError('Existing dependency incompatible: install and lock UniTask 2.5.11 using '+UNITASK)
    result=runtime_manifest(ver,backend)
    samples=MAIN+'.acceptance.samples'
    if samples in deps:
        canonical='file:../../../Packages/'+samples
        if deps[samples]!=canonical:raise ValueError('noncanonical-sample-acceptance-source')
        result['dependencies'][samples]=canonical
        if samples not in result['testables']:result['testables'].append(samples)
    return result

def validate_main_package(package,assemblies):
    errors=[]
    expected={'com.unity.ugui':'1.0.0','com.cysharp.unitask':'2.5.11'}
    if package.get('name')!=MAIN or package.get('version')!='0.4.0' or package.get('unity')!='2022.3' or package.get('unityRelease')!='62f2':
        errors.append('main-package-identity-or-unity-baseline')
    if package.get('dependencies')!=expected:errors.append('main-package-runtime-dependencies')
    graph={'Core':['UniTask'],'Loading':['Wulato.UIKit.Core','UniTask'],'UGUI':['Wulato.UIKit.Core','Wulato.UIKit.Loading','UniTask','UnityEngine.UI']}
    for name,refs in graph.items():
        if set(assemblies.get(name,[]))!=set(refs):errors.append('runtime-assembly-boundary:'+name)
    return errors

def validate_installed_report(ver,folder):
    import json
    from run import VERSIONS
    path=folder/'dependency-snapshot.json'
    if not path.is_file():return ['missing-actual-package-metadata']
    data=json.loads(path.read_text())
    expected={'com.wulato.uikit':'0.4.0','com.cysharp.unitask':'2.5.11','com.unity.ugui':VERSIONS[ver][1]}
    errors=[]
    if data.get('editor')!=ver:errors.append('resolved-report-editor')
    actual={item['name']:item for item in data.get('packages',[])}
    for name,version in expected.items():
        if actual.get(name,{}).get('version')!=version:errors.append('actual-package-version:'+name)
    return errors

# Explicit optional package identities, never a permissive namespace prefix.
FLOW_EXTENSIONS={MAIN+'.'+name:name for name in ('navigation','presentation','dialogs','popup-queue')}
EXTENSIONS={MAIN+'.addressables':'addressables'}
def is_extension_package(name):
    return name in EXTENSIONS

def validate_extension_package(package,references):
    errors=[]
    if package.get('name')==MAIN+'.addressables':
        expected={'com.wulato.uikit':'0.4.0','com.cysharp.unitask':'2.5.11','com.unity.addressables':'1.21.21'}
        if package.get('version')!='0.2.0' or package.get('unity')!='2022.3' or package.get('unityRelease')!='62f2':errors.append('addressables-identity')
        if package.get('dependencies')!=expected:errors.append('addressables-dependencies')
        if set(references)!={'Wulato.UIKit.Core','Wulato.UIKit.Loading','UniTask','Unity.Addressables','Unity.ResourceManager'}:errors.append('addressables-assembly-boundary')
        return errors
    if not is_extension_package(package.get('name')) or package.get('version')!='0.1.1' or package.get('unity')!='2022.3' or package.get('unityRelease')!='62f2':errors.append('extension-identity-or-unity-baseline')
    if package.get('dependencies')!={'com.wulato.uikit':'0.4.0','com.cysharp.unitask':'2.5.11'}:errors.append('extension-core-or-optional-dependency')
    if set(references)!={'Wulato.UIKit.Core','Wulato.UIKit.UGUI','UniTask'}:errors.append('extension-runtime-assembly-boundary')
    return errors
