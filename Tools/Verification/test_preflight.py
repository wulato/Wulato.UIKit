import contextlib
import datetime
import io
import json
import tempfile
import unittest
from pathlib import Path
from unittest.mock import patch
import run
from evidence import seal,sha256
from gates import preflight
from results import evaluate

class PreflightTests(unittest.TestCase):
    def setUp(self):
        self.tmp=tempfile.TemporaryDirectory();self.addCleanup(self.tmp.cleanup);self.artifacts=Path(self.tmp.name)
        patcher=patch.object(run,'A',self.artifacts);patcher.start();self.addCleanup(patcher.stop)
        parent=self.artifacts/'complete';parent.mkdir()
        entries=[dict(run_id='complete/'+f'{short}-{backend}-{suite}',folder=f'{short}-{backend}-{suite}',editor=ver,backend=backend,mode='PlayMode',suite=suite)
                 for ver,(short,_,_) in run.VERSIONS.items() for backend in ('legacy','input') for suite in ('init','input-preflight')]
        (parent/'batch.json').write_text(json.dumps(dict(schema=1,serialization='exclusive-batch',batch_id='complete',command='tests',started_at='2026-10-31T01:00:00+00:00',attempts=entries)))
        batch_hash=sha256(parent/'batch.json')
        self.folders=[];matrix=json.loads((run.C/'matrix.json').read_text())
        for ver,(short,_,_) in run.VERSIONS.items():
            for backend in ('legacy','input'):
                host=run.V/'Projects'/f'{short}-{backend}';snapshot=run.snapshot(ver,backend,host)
                self.assertFalse(snapshot['errors'])
                for suite in ('init','input-preflight'):
                    required=matrix[ver][backend]['PlayMode'][suite];out=self.artifacts/'complete'/f'{short}-{backend}-{suite}';out.mkdir(parents=True);self.folders.append(out)
                    def write(name,obj): (out/name).write_text(json.dumps(obj))
                    log='Unity Editor version: '+ver+' (fixture)\n';(out/'editor.log').write_text(log)
                    xml='<test-run result="Passed">'+''.join('<test-case fullname="'+name+'" result="Passed"/>' for name in required)+'</test-run>'
                    (out/'results.xml').write_text(xml);write('host-snapshot.json',snapshot)
                    command=['/fixture/Unity','-batchmode','-runTests','-projectPath',str(host),'-testPlatform','PlayMode','-testCategory',suite.replace('-','_'),'-testResults',str(out/'results.xml'),'-logFile',str(out/'editor.log')];write('command.json',command)
                    metadata=dict(schema=3,batch_id='complete',batch_sha256=batch_hash,run_id='complete/'+out.name,state='completed',editor=ver,backend=backend,mode='PlayMode',suite=suite,started_at='2026-10-31T01:00:00+00:00',finished_at='2026-10-31T01:00:01+00:00',source_before=snapshot['fingerprint'],source_after=snapshot['fingerprint'],returncode=0,timed_out=False,actual_editor=ver);write('run.json',metadata)
                    traces={}
                    for fullname in required:
                        name=fullname.rsplit('.',1)[-1]
                        filenames=[name+'.trace.json'] if suite=='init' else [name+'-'+o+'.trace.json' for o in ('Owner0','Owner1')]
                        for filename in filenames:
                            traces[filename]={'events':[]};write(filename,traces[filename])
                    write('trace.json',traces)
                    result=evaluate(out/'results.xml',log,required)
                    result.update(schema=3,batch_id='complete',batch_sha256=batch_hash,run_id=metadata['run_id'],editor=ver,backend=backend,mode='PlayMode',suite=suite,fingerprint=snapshot['fingerprint'],evidence=str(out),evidence_index_sha256=seal(out));write('summary.json',result)
    def gate(self):
        with contextlib.redirect_stdout(io.StringIO()):return preflight(run.R)
    def test_all_intact_suites_pass(self): self.assertEqual(0,self.gate())
    def test_deleted_raw_xml_blocks_g0(self):
        (self.folders[0]/'results.xml').unlink();self.assertNotEqual(0,self.gate())
    def test_tampered_raw_log_blocks_g0(self):
        (self.folders[0]/'editor.log').write_text('edited');self.assertNotEqual(0,self.gate())
    def test_summary_only_is_not_valid_evidence(self):
        for folder in self.folders:
            for p in folder.iterdir():
                if p.name!='summary.json':p.unlink()
        self.assertNotEqual(0,self.gate())
    def register_new(self,out,metadata):
        parent=out.parent;parent.mkdir(parents=True,exist_ok=True)
        batch_id=parent.name;metadata.update(schema=3,run_id=batch_id+'/'+out.name,batch_id=batch_id)
        entry={key:metadata[key] for key in ('run_id','editor','backend','mode','suite')};entry['folder']=out.name
        inventory=dict(schema=1,serialization='exclusive-batch',batch_id=batch_id,command='tests',started_at=metadata['started_at'],attempts=[entry])
        (parent/'batch.json').write_text(json.dumps(inventory));metadata['batch_sha256']=sha256(parent/'batch.json')
        out.mkdir(parents=True,exist_ok=True);(out/'run.json').write_text(json.dumps(metadata))
    def test_new_unfinished_run_cannot_fall_back_to_old_green(self):
        metadata=json.loads((self.folders[0]/'run.json').read_text());metadata.update(run_id='new/attempt',state='running',started_at='2026-10-31T01:00:02+00:00')
        out=self.artifacts/'new'/'attempt';self.register_new(out,metadata)
        self.assertNotEqual(0,self.gate())
    def test_new_missing_summary_cannot_fall_back_to_old_green(self):
        metadata=json.loads((self.folders[0]/'run.json').read_text());metadata.update(run_id='new/attempt',state='completed',started_at='2026-10-31T01:00:02+00:00')
        out=self.artifacts/'new'/'attempt';self.register_new(out,metadata)
        self.assertNotEqual(0,self.gate())
    def test_missing_registration_cannot_restore_old_green(self):
        old=self.folders[0];out=self.artifacts/'new'/'failed';import shutil;shutil.copytree(old,out)
        metadata=json.loads((out/'run.json').read_text());metadata.update(run_id='new/failed',state='completed',started_at='2026-10-31T01:00:02+00:00',returncode=2)
        self.register_new(out,metadata);self.assertNotEqual(0,self.gate())
        (out/'run.json').unlink();self.assertNotEqual(0,self.gate())
    def test_unknown_registration_schema_cannot_restore_old_green(self):
        metadata=json.loads((self.folders[0]/'run.json').read_text());metadata.update(run_id='new/failed',schema=99,state='completed',started_at='2026-10-31T01:00:02+00:00',returncode=2)
        out=self.artifacts/'new'/'failed';self.register_new(out,metadata);metadata['schema']=99;(out/'run.json').write_text(json.dumps(metadata))
        self.assertNotEqual(0,self.gate())
    def test_inventory_declared_but_missing_attempt_blocks_old_green(self):
        out=self.artifacts/'inventory-only';out.mkdir()
        inventory=dict(schema=1,serialization='exclusive-batch',batch_id=out.name,command='tests',started_at='2026-10-31T01:00:02+00:00',attempts=[dict(run_id=out.name+'/u2022-legacy',folder='u2022-legacy',editor='2022.3.62f2',backend='legacy',mode='PlayMode',suite='init')])
        (out/'batch.json').write_text(json.dumps(inventory));self.assertNotEqual(0,self.gate())
    def test_deleted_new_attempt_directory_blocks_old_green(self):
        metadata=json.loads((self.folders[0]/'run.json').read_text());metadata.update(started_at='2026-10-31T01:00:02+00:00')
        out=self.artifacts/'new'/'attempt';self.register_new(out,metadata)
        import shutil;shutil.rmtree(out);self.assertNotEqual(0,self.gate())
    def test_unknown_inventory_schema_blocks_old_green(self):
        p=self.artifacts/'complete'/'batch.json';data=json.loads(p.read_text());data['schema']=99;p.write_text(json.dumps(data))
        self.assertNotEqual(0,self.gate())
    def test_deleted_inventory_blocks_old_green(self):
        (self.artifacts/'complete'/'batch.json').unlink();self.assertNotEqual(0,self.gate())
    def test_valid_retry_can_supersede_inventoried_missing_registration(self):
        import shutil
        metadata=json.loads((self.folders[0]/'run.json').read_text());metadata.update(started_at='2026-10-31T01:00:02+00:00')
        broken=self.artifacts/'broken'/'attempt';self.register_new(broken,metadata);(broken/'run.json').unlink()
        self.assertNotEqual(0,self.gate())
        recovery=self.artifacts/'recovery'/'attempt';shutil.copytree(self.folders[0],recovery)
        metadata.update(started_at='2026-10-31T01:00:03+00:00');self.register_new(recovery,metadata)
        command=json.loads((recovery/'command.json').read_text())
        for arg,name in [('-testResults','results.xml'),('-logFile','editor.log')]:command[command.index(arg)+1]=str(recovery/name)
        (recovery/'command.json').write_text(json.dumps(command))
        summary=json.loads((recovery/'summary.json').read_text());summary.update(run_id=metadata['run_id'],batch_id=metadata['batch_id'],batch_sha256=metadata['batch_sha256'],evidence=str(recovery),evidence_index_sha256=seal(recovery))
        (recovery/'summary.json').write_text(json.dumps(summary));self.assertEqual(0,self.gate())
