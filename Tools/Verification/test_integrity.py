import json
import shutil
import tempfile
import unittest
from pathlib import Path
from unittest.mock import patch
import run

class FingerprintTests(unittest.TestCase):
    def setUp(self):
        self.tmp=tempfile.TemporaryDirectory();self.addCleanup(self.tmp.cleanup)
        root=Path(self.tmp.name);self.root=root
        shutil.copytree(run.C,root/'Tools/Verification',ignore=shutil.ignore_patterns('__pycache__'))
        shutil.copytree(run.V/'Packages',root/'Verification/Packages')
        source=run.V/'Projects/u2022-legacy';self.host=root/'Verification/Projects/u2022-legacy'
        for name in ('Assets','Packages','ProjectSettings'):shutil.copytree(source/name,self.host/name)
        for key,value in [('R',root),('V',root/'Verification'),('C',root/'Tools/Verification')]:
            p=patch.object(run,key,value);p.start();self.addCleanup(p.stop)
    def test_input_manager_change_invalidates_fingerprint(self):
        before=run.fingerprint(self.host);p=self.host/'ProjectSettings/InputManager.asset';p.write_text(p.read_text()+'\n# changed binding\n')
        self.assertNotEqual(before,run.fingerprint(self.host))
    def test_added_host_source_invalidates_fingerprint(self):
        before=run.fingerprint(self.host);(self.host/'Assets/Extra.cs').write_text('// compile input\n')
        self.assertNotEqual(before,run.fingerprint(self.host))
    def test_added_host_asmdef_invalidates_fingerprint(self):
        before=run.fingerprint(self.host);(self.host/'Assets/Extra.asmdef').write_text('{"name":"Extra"}')
        self.assertNotEqual(before,run.fingerprint(self.host))
    def test_same_named_package_at_alternate_path_is_rejected(self):
        other=self.root/'Verification/Alternate';shutil.copytree(run.V/'Packages'/run.COMMON,other)
        p=self.host/'Packages/manifest.json';m=json.loads(p.read_text());m['dependencies'][run.COMMON]='file:../../../Alternate';p.write_text(json.dumps(m))
        with self.assertRaises(ValueError):run.fingerprint(self.host)
    def test_lock_source_path_mismatch_is_rejected(self):
        p=self.host/'Packages/packages-lock.json';m=json.loads(p.read_text());m['dependencies'][run.COMMON]['version']='file:../../../Alternate';p.write_text(json.dumps(m))
        self.assertTrue(run.snapshot('2022.3.62f2','legacy',self.host)['errors'])
