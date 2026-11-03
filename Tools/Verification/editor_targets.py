"""Explicit editor profiles; known profiles do not imply compatibility acceptance."""
import json
from pathlib import Path
CONFIG_PATH=Path(__file__).with_name('editors.json')
CONFIG=json.loads(CONFIG_PATH.read_text())
PROFILES=CONFIG['profiles']
DEFAULT=CONFIG['default']
TARGETS=CONFIG['targets']
for version in (DEFAULT,*TARGETS):
    if version not in PROFILES:raise ValueError('missing-editor-target:'+version)
for version,profile in PROFILES.items():
    for field in ('host','ugui','testFramework','inputSystem','addressables','sbp'):
        if not profile.get(field):raise ValueError('incomplete-editor-target:'+version+':'+field)
if len({p['host'] for p in PROFILES.values()})!=len(PROFILES):raise ValueError('duplicate-editor-host')
def editors(value):
    result=list(TARGETS) if value=='all' else [value]
    if not result:raise ValueError('empty-editor-targets')
    for version in result:
        if version not in PROFILES:raise ValueError('unknown-editor-target:'+version)
    return result
