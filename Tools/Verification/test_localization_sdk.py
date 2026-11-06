import json,tempfile,unittest
from pathlib import Path
from unittest.mock import patch
import localization_sdk as sdk
import run
class LocalizationSdkTests(unittest.TestCase):
 def test_stage_is_explicit_and_gated(self):
  with tempfile.TemporaryDirectory() as folder:
   root=Path(folder);host=root/'Artifacts/uikit/ci-selected/hosts/test'
   (host/'Packages').mkdir(parents=True);(host/'ProjectSettings').mkdir()
   (host/'Packages/manifest.json').write_text('{"dependencies":{}}')
   (host/'ProjectSettings/ProjectSettings.asset').write_text('  otherSettings:\n    Standalone: 0\n  scriptingDefineSymbols:\n    Standalone: EXISTING\n')
   with patch.object(sdk,'ROOT',root),patch.object(sdk.shutil,'copytree'):
    sdk.stage(host);self.assertFalse(sdk.integration_enabled(host))
    with self.assertRaisesRegex(ValueError,'enable-requires'):sdk.stage(host,enabled=True)
    sdk.stage(host,install_sdk=True);self.assertFalse(sdk.integration_enabled(host))
    sdk.stage(host,enabled=True);self.assertTrue(sdk.integration_enabled(host))
    self.assertIn('otherSettings:\n    Standalone: 0\n',(host/'ProjectSettings/ProjectSettings.asset').read_text())
    deps=json.loads((host/'Packages/manifest.json').read_text())['dependencies'];self.assertEqual('1.4.5',deps['com.unity.localization'])
    sdk.stage(host);self.assertFalse(sdk.integration_enabled(host))
 def test_rejects_unapproved_location(self):
  with self.assertRaisesRegex(ValueError,'restricted'):sdk.stage('/tmp/not-a-uikit-host',True)
 def test_suite_is_opt_in(self):
  matrix={'localization':['case'],'contracts':['core']};manifest={'dependencies':{'com.unity.localization':'1.4.5'}}
  self.assertNotIn('localization',run.applicable_suites(matrix,manifest))
  self.assertIn('localization',run.applicable_suites(matrix,manifest,localization=True))
  self.assertNotIn('localization',run.applicable_suites(matrix,{'dependencies':{}},localization=True))
