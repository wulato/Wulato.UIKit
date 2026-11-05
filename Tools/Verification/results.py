"""Fail-closed NUnit result evaluation; process exit zero is insufficient."""
import re
import xml.etree.ElementTree as ET
from pathlib import Path

def evaluate(xml_path, log, required, returncode=0, timed_out=False):
    errors=[]; cases=[]
    if timed_out: errors.append('timeout')
    if returncode: errors.append('editor-exit:'+str(returncode))
    if re.search(r'error CS\d+|Scripts have compiler errors|Compilation failed',log): errors.append('compilation-error')
    try:
        root=ET.parse(xml_path).getroot()
        cases=[{'name':n.get('fullname',n.get('name','')),'result':n.get('result','Unknown'),'message':n.findtext('failure/message','')} for n in root.iter('test-case')]
        for node in root.iter():
            if node.tag in ('test-run','test-suite'):
                state=node.get('result','')
                if state.startswith(('Failed','Skipped','Inconclusive')): errors.append('test-suite:'+state)
                if int(node.get('failed','0'))>0: errors.append('test-suite-failed-count')
    except (OSError,ET.ParseError) as e: errors.append('xml:'+str(e))
    if not cases: errors.append('zero-tests')
    for c in cases:
        if c['result']!='Passed': errors.append(c['name']+':'+c['result'])
    names={c['name'] for c in cases}
    for name in required:
        if name not in names: errors.append('missing-required:'+name)
    return {'status':'FAIL' if errors else 'PASS','errors':errors,'cases':cases,'count':len(cases)}
