import unittest
from ci_suites import select_suites
class ModuleSelectionTests(unittest.TestCase):
    def test_navigation_change_does_not_run_unrelated_modules_or_all(self):
        self.assertEqual([('EditMode','navigation','all'),('PlayMode','flow-integration','all')],select_suites(['Packages/com.wulato.uikit.navigation/Runtime/UINavigator.cs']))
    def test_input_adapter_uses_only_input_hosts(self):
        selected=select_suites(['Packages/com.wulato.uikit.input-system/Runtime/InputSystemUIInputDriver.cs'])
        self.assertTrue(selected);self.assertTrue(all(backend=='input' for mode,suite,backend in selected))
    def test_document_only_change_does_not_launch_unity(self):
        self.assertEqual([],select_suites(['Packages/com.wulato.uikit.navigation/Documentation~/index.md']))
    def test_transaction_change_requires_navigation_and_transition_regressions(self):
        self.assertEqual({'navigation','transitions','flow-integration'},{suite for mode,suite,backend in select_suites(['Packages/com.wulato.uikit/Runtime/Core/Transactions/UIContextEngine.Pages.cs'])})

    def test_input_adapter_metadata_change_includes_editmode_package_contract(self):
        self.assertIn(('EditMode','input-package','input'),select_suites(['Packages/com.wulato.uikit.input-system/package.json']))
