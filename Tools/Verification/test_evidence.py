import json
import tempfile
import unittest
from pathlib import Path
from evidence import seal,verify,sha256

class EvidenceTests(unittest.TestCase):
    def setUp(self):
        self.tmp=tempfile.TemporaryDirectory();self.addCleanup(self.tmp.cleanup);self.out=Path(self.tmp.name)/'host';self.out.mkdir()
        self.required=['Fixture.Case'];self.snapshot=dict(editor='2022.3.62f2',backend='legacy',host='/fixture/host',fingerprint='current',manifest={},lock={},settings='settings',sources={},errors=[])
        self.record=dict(schema=3,run_id=self.out.parent.name+'/host',status='PASS',editor='2022.3.62f2',backend='legacy',mode='PlayMode',suite='input-preflight',fingerprint='current',cases=[dict(name='Fixture.Case',result='Passed',message='')],count=1,evidence=str(self.out))
        batch=dict(schema=1,serialization='exclusive-batch',batch_id=self.out.parent.name,command='tests',started_at='2026-10-31T01:00:00+00:00',attempts=[dict(run_id=self.record['run_id'],folder='host',editor=self.record['editor'],backend='legacy',mode='PlayMode',suite='input-preflight')])
        (self.out.parent/'batch.json').write_text(json.dumps(batch))
        self.record.update(batch_id=batch['batch_id'],batch_sha256=sha256(self.out.parent/'batch.json'))
        self.write('results.xml','<test-run result="Passed"><test-case fullname="Fixture.Case" result="Passed"/></test-run>')
        self.write('editor.log','Unity Editor version: 2022.3.62f2 (fixture)\n')
        self.json('host-snapshot.json',self.snapshot)
        self.json('command.json',['/fixture/Unity','-batchmode','-runTests','-projectPath','/fixture/host','-testPlatform','PlayMode','-testCategory','input_preflight','-testResults',str(self.out/'results.xml'),'-logFile',str(self.out/'editor.log')])
        execution=dict(schema=3,run_id=self.out.parent.name+'/host',state='completed',editor='2022.3.62f2',backend='legacy',mode='PlayMode',suite='input-preflight',started_at='2026-10-31T01:00:00+00:00',finished_at='2026-10-31T01:00:01+00:00',source_before='current',source_after='current',returncode=0,timed_out=False,actual_editor='2022.3.62f2')
        execution.update(batch_id=self.record['batch_id'],batch_sha256=self.record['batch_sha256'])
        self.json('run.json',execution)
        traces={}
        for owner in ('Owner0','Owner1'):
            name='Case-'+owner+'.trace.json';data=dict(owner=owner,events=[]);traces[name]=data;self.json(name,data)
        self.json('trace.json',traces);self.reseal()
    def write(self,name,text): (self.out/name).write_text(text)
    def json(self,name,value): self.write(name,json.dumps(value))
    def reseal(self): self.record['evidence_index_sha256']=seal(self.out)
    def check(self): return verify(self.record,self.out,self.required,self.snapshot)['status']
    def test_intact_evidence_passes(self): self.assertEqual('PASS',self.check())
    def test_missing_xml_fails(self): (self.out/'results.xml').unlink();self.assertEqual('FAIL',self.check())
    def test_missing_log_fails(self): (self.out/'editor.log').unlink();self.assertEqual('FAIL',self.check())
    def test_missing_snapshot_fails(self): (self.out/'host-snapshot.json').unlink();self.assertEqual('FAIL',self.check())
    def test_missing_command_fails(self): (self.out/'command.json').unlink();self.assertEqual('FAIL',self.check())
    def test_missing_trace_fails(self): (self.out/'Case-Owner0.trace.json').unlink();self.assertEqual('FAIL',self.check())
    def test_missing_index_fails(self): (self.out/'evidence-index.json').unlink();self.assertEqual('FAIL',self.check())
    def test_changed_xml_hash_fails(self): self.write('results.xml','<test-run/>');self.assertEqual('FAIL',self.check())
    def test_resealed_failed_xml_fails(self):
        self.write('results.xml','<test-run result="Failed"><test-case fullname="Fixture.Case" result="Failed"/></test-run>');self.reseal();self.assertEqual('FAIL',self.check())
    def test_wrong_case_fullname_fails(self):
        self.write('results.xml','<test-run result="Passed"><test-case fullname="Other.Case" result="Passed"/></test-run>');self.reseal();self.assertEqual('FAIL',self.check())
    def test_resealed_compiler_error_fails(self):
        self.write('editor.log','Unity Editor version: 2022.3.62f2\nerror CS0246: missing');self.reseal();self.assertEqual('FAIL',self.check())
    def test_summary_case_mismatch_fails(self): self.record['cases']=[];self.assertEqual('FAIL',self.check())
    def test_current_snapshot_mismatch_fails(self): self.snapshot=dict(self.snapshot,settings='changed');self.assertEqual('FAIL',self.check())
    def test_incomplete_run_fails(self):
        r=json.loads((self.out/'run.json').read_text());r['state']='running';self.json('run.json',r);self.reseal();self.assertEqual('FAIL',self.check())
    def test_wrong_mode_fails(self): self.record['mode']='EditMode';self.assertEqual('FAIL',self.check())
    def test_resealed_missing_case_trace_fails(self):
        (self.out/'Case-Owner0.trace.json').unlink();self.json('trace.json',{'Case-Owner1.trace.json':dict(owner='Owner1',events=[])});self.reseal();self.assertEqual('FAIL',self.check())
    def test_missing_batch_inventory_fails(self):
        (self.out.parent/'batch.json').unlink();self.assertEqual('FAIL',self.check())
    def test_unknown_batch_inventory_schema_fails(self):
        p=self.out.parent/'batch.json';r=json.loads(p.read_text());r['schema']=99;p.write_text(json.dumps(r));self.assertEqual('FAIL',self.check())
    def test_changed_batch_inventory_hash_fails(self):
        p=self.out.parent/'batch.json';p.write_text(p.read_text()+'\n');self.assertEqual('FAIL',self.check())
