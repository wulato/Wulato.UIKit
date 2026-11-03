"""Independent batch inventory keeps an attempt discoverable when its directory/run.json disappears."""
import datetime
import re
import json
from pathlib import Path
from evidence import sha256

RUN_SCHEMA=3

def read_object(path):
    data=json.loads(path.read_text())
    if not isinstance(data,dict):raise ValueError('not-an-object:'+str(path))
    return data

def timestamp(value):
    date=datetime.datetime.fromisoformat(value)
    if date.tzinfo is None:raise ValueError('missing-timezone')
    return date.timestamp()

def validate_inventory(path):
    inventory=read_object(path)
    if inventory.get('schema')!=1:raise ValueError('unknown-inventory-schema')
    if inventory.get('batch_id')!=path.parent.name:raise ValueError('inventory-batch-id')
    timestamp(inventory['started_at'])
    if inventory.get('command') not in ('tests','prepare'):raise ValueError('inventory-command')
    attempts=inventory.get('attempts')
    if not isinstance(attempts,list) or not attempts:raise ValueError('empty-inventory')
    seen=set()
    for item in attempts:
        if not isinstance(item,dict):raise ValueError('invalid-attempt-entry')
        folder=item['folder']
        if not isinstance(folder,str) or folder in ('','.','..') or Path(folder).name!=folder:raise ValueError('unsafe-attempt-folder')
        if folder in seen:raise ValueError('duplicate-attempt-folder')
        seen.add(folder)
        if item['run_id']!=inventory['batch_id']+'/'+folder:raise ValueError('inventory-run-id')
        if not re.fullmatch(r'\d+\.\d+\.\d+[abfp]\d+(?:c\d+)?',str(item.get('editor',''))) or item.get('backend') not in ('legacy','input'):raise ValueError('inventory-host')
        if inventory['command']=='tests' and (item.get('mode') not in ('PlayMode','EditMode') or not isinstance(item.get('suite'),str)):raise ValueError('inventory-test-selector')
    return inventory

def registered(artifacts):
    records=[];errors=[];covered=set()
    # The inventory lives outside individual attempt directories and is written first.
    for path in artifacts.glob('*/batch.json'):
        try:
            batch=validate_inventory(path);digest=sha256(path)
            for entry in batch['attempts']:
                folder=path.parent/entry['folder'];covered.add(folder)
                if batch['command']!='tests':continue
                order=timestamp(batch['started_at'])
                context=dict(entry,started_at=batch['started_at'],state='registration-missing',registration_errors=[])
                try:
                    execution=read_object(folder/'run.json')
                    if execution.get('schema')!=RUN_SCHEMA:raise ValueError('unknown-run-schema')
                    for key in ('run_id','editor','backend','mode','suite'):
                        if execution.get(key)!=entry.get(key):raise ValueError('inventory-run-mismatch:'+key)
                    if execution.get('batch_id')!=batch['batch_id'] or execution.get('batch_sha256')!=digest:raise ValueError('inventory-hash-mismatch')
                    actual_start=timestamp(execution['started_at'])
                    if actual_start<order:raise ValueError('attempt-starts-before-batch')
                    order=actual_start;context['state']=execution.get('state')
                except (OSError,ValueError,KeyError,TypeError) as e:
                    context['registration_errors'].append('invalid-attempt-registration:'+str(folder)+':'+str(e))
                records.append((order,entry['run_id'],context,folder))
        except (OSError,ValueError,KeyError,TypeError) as e:
            errors.append('invalid-batch-inventory:'+str(path)+':'+str(e))
    # Keep schema-2 history readable, but never silently ignore unknown/orphaned records.
    # Schema-3 runs require an inventory even when run.json itself is otherwise valid.
    folders={p.parent for p in artifacts.glob('*/*/run.json')}|{p.parent for p in artifacts.glob('*/*/summary.json')}
    for folder in sorted(folders-covered):
        try:
            registration=folder/'run.json'
            if not registration.exists():
                summary=read_object(folder/'summary.json')
                if summary.get('schema') in (2,3):raise ValueError('orphaned-summary-missing-registration')
                continue  # Pre-registration historical reports cannot satisfy a current gate.
            data=read_object(registration)
            if data.get('schema')==RUN_SCHEMA:raise ValueError('missing-batch-inventory')
            if data.get('schema')!=2:raise ValueError('unknown-run-schema')
            if data.get('suite') is None:continue
            records.append((timestamp(data['started_at']),data['run_id'],data,folder))
        except (OSError,ValueError,KeyError,TypeError) as e:
            errors.append('invalid-run-registration:'+str(folder)+':'+str(e))
    return records,errors
