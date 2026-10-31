import contextlib
import datetime
import io
import json
import tempfile
import unittest
from pathlib import Path
from unittest.mock import patch
import run
from evidence import seal
from gates import preflight
from results import evaluate

class PreflightTests(unittest.TestCase):
    def setUp(self):
        self.tmp=tempfile.TemporaryDirectory();self.addCleanup(self.tmp.cleanup);self.artifacts=Path(self.tmp.name)
        patcher=patch.object(run,'A',self.artifacts);patcher.start();self.addCleanup(patcher.stop)
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
                    metadata=dict(schema=2,run_id='complete/'+out.name,state='completed',editor=ver,backend=backend,mode='PlayMode',suite=suite,started_at='2026-10-31T01:00:00+00:00',finished_at='2026-10-31T01:00:01+00:00',source_before=snapshot['fingerprint'],source_after=snapshot['fingerprint'],returncode=0,timed_out=False,actual_editor=ver);write('run.json',metadata)
                    traces={}
                    for fullname in required:
                        name=fullname.rsplit('.',1)[-1]
                        filenames=[name+'.trace.json'] if suite=='init' else [name+'-'+o+'.trace.json' for o in ('Owner0','Owner1')]
                        for filename in filenames:
                            traces[filename]={'events':[]};write(filename,traces[filename])
                    write('trace.json',traces)
                    result=evaluate(out/'results.xml',log,required)
                    result.update(schema=2,run_id=metadata['run_id'],editor=ver,backend=backend,mode='PlayMode',suite=suite,fingerprint=snapshot['fingerprint'],evidence=str(out),evidence_index_sha256=seal(out));write('summary.json',result)
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
    def test_new_unfinished_run_cannot_fall_back_to_old_green(self):
        metadata=json.loads((self.folders[0]/'run.json').read_text());metadata.update(run_id='new/attempt',state='running',started_at='2026-10-31T01:00:02+00:00')
        out=self.artifacts/'new'/'attempt';out.mkdir(parents=True);(out/'run.json').write_text(json.dumps(metadata))
        self.assertNotEqual(0,self.gate())
    def test_new_missing_summary_cannot_fall_back_to_old_green(self):
        metadata=json.loads((self.folders[0]/'run.json').read_text());metadata.update(run_id='new/attempt',state='completed',started_at='2026-10-31T01:00:02+00:00')
        out=self.artifacts/'new'/'attempt';out.mkdir(parents=True);(out/'run.json').write_text(json.dumps(metadata))
        self.assertNotEqual(0,self.gate())
