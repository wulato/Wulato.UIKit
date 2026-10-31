"""Serialize verification batches across CLI processes; G0 cannot observe an active batch as green."""
import contextlib
import fcntl

class VerificationBusy(RuntimeError):
    pass

@contextlib.contextmanager
def guard(artifacts,shared=False):
    artifacts.mkdir(parents=True,exist_ok=True)
    with (artifacts/'.verification.lock').open('a+b') as handle:
        mode=fcntl.LOCK_SH if shared else fcntl.LOCK_EX
        try:fcntl.flock(handle.fileno(),mode|fcntl.LOCK_NB)
        except BlockingIOError as e:raise VerificationBusy('verification-batch-in-progress') from e
        try:yield
        finally:fcntl.flock(handle.fileno(),fcntl.LOCK_UN)
