import contextlib
import io
import tempfile
import unittest
from pathlib import Path
from unittest.mock import patch
from locking import guard,VerificationBusy
import run
from gates import preflight
class BatchLockTests(unittest.TestCase):
    def test_second_batch_is_rejected(self):
        with tempfile.TemporaryDirectory() as d,guard(Path(d)):
            with self.assertRaises(VerificationBusy):
                with guard(Path(d)):pass
    def test_gate_cannot_run_during_active_batch(self):
        with tempfile.TemporaryDirectory() as d,guard(Path(d)):
            with self.assertRaises(VerificationBusy):
                with guard(Path(d),shared=True):pass
    def test_shared_gate_readers_can_coexist(self):
        with tempfile.TemporaryDirectory() as d,guard(Path(d),shared=True),guard(Path(d),shared=True):pass
    def test_exception_releases_batch_lock(self):
        with tempfile.TemporaryDirectory() as d:
            try:
                with guard(Path(d)):raise ValueError('fixture')
            except ValueError:pass
            with guard(Path(d)):pass
    def test_preflight_reports_fail_during_active_batch(self):
        with tempfile.TemporaryDirectory() as d,patch.object(run,'A',Path(d)),guard(Path(d)),contextlib.redirect_stdout(io.StringIO()):
            self.assertNotEqual(0,preflight(run.R))
    def test_other_process_cannot_start_overlapping_batch(self):
        import subprocess,sys
        script="import sys;from pathlib import Path;sys.path.insert(0,sys.argv[1]);from locking import guard,VerificationBusy\ntry:\n with guard(Path(sys.argv[2])):pass\nexcept VerificationBusy:sys.exit(23)"
        with tempfile.TemporaryDirectory() as d,guard(Path(d)):
            child=subprocess.run([sys.executable,'-c',script,str(run.C),d],capture_output=True,text=True)
            self.assertEqual(23,child.returncode,child.stderr)
