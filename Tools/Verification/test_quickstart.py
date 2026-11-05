import tempfile
import unittest
from pathlib import Path
import samples

class QuickStartConsumerTests(unittest.TestCase):
    def test_isolated_manifest_excludes_other_samples_and_extensions(self):
        self.assertTrue(hasattr(samples,'quickstart_manifest'),'QuickStart-only consumer preparation is missing')
        for backend in ('legacy','input'):
            manifest=samples.quickstart_manifest('2022.3.62f2',backend)
            deps=manifest['dependencies']
            self.assertIn('com.wulato.uikit.acceptance.quickstart',deps)
            self.assertNotIn('com.wulato.uikit.acceptance.samples',deps)
            self.assertNotIn('com.wulato.uikit.navigation',deps)
            self.assertNotIn('com.wulato.uikit.addressables',deps)
            self.assertEqual('com.wulato.uikit.input-system' in deps,backend=='input')
    def test_quickstart_consumer_hosts_do_not_replace_standard_selection(self):
        import argparse,run
        args=argparse.Namespace(editor='2022.3.62f2',backend='legacy')
        standard=list(run.selected(args))
        args.consumer_hosts=[('2022.3.62f2','legacy',Path('/tmp/quickstart-consumer'))]
        self.assertEqual(list(run.selected(args)),args.consumer_hosts)
        self.assertEqual(standard[0][2].name,'u2022-legacy')

    def test_check_only_cannot_dispatch_mutating_clean_consumer_preparation(self):
        import argparse
        from unittest.mock import patch
        args=argparse.Namespace(quickstart_only=True,check_only=True,red=False)
        with patch.object(samples,'prepare_quickstart',side_effect=AssertionError('mutating preparation was dispatched')):
            self.assertEqual(samples.prepare(args),1)
