import json,tempfile,unittest
from pathlib import Path
import samples

class SamplesTests(unittest.TestCase):
    def test_missing_assembly_does_not_install_test_package(self):
        with tempfile.TemporaryDirectory() as d:
            host=Path(d);(host/'Packages').mkdir();p=host/'Packages/manifest.json'
            p.write_text('{"dependencies":{},"testables":[]}');before=p.read_bytes()
            with self.assertRaises(ValueError):samples.install_tests(host)
            self.assertEqual(before,p.read_bytes())
    def test_all_three_assemblies_required_before_install(self):
        with tempfile.TemporaryDirectory() as d:
            host=Path(d);(host/'Packages').mkdir();p=host/'Packages/manifest.json'
            p.write_text('{"dependencies":{},"testables":[]}')
            for name in ('QuickStart','Modals','Inventory'):
                folder=host/'Assets/Samples/Wulato UIKit'/name;folder.mkdir(parents=True)
                (folder/(name+'.asmdef')).write_text(json.dumps({'name':'Wulato.UIKit.Samples.'+name}))
            samples.install_tests(host);samples.install_tests(host)
            data=json.loads(p.read_text());self.assertEqual(data['dependencies']['com.wulato.uikit.acceptance.samples'],'file:../../../Packages/com.wulato.uikit.acceptance.samples')
            self.assertEqual(data['testables'],['com.wulato.uikit.acceptance.samples'])
    def test_import_preserves_modified_consumer_files(self):
        with tempfile.TemporaryDirectory() as d:
            root=Path(d);source=root/'source';source.mkdir();(source/'View.cs').write_text('upstream')
            host=root/'host';samples.import_sample(source,host);target=host/'Assets/Samples/Wulato UIKit/source/View.cs';target.write_text('business')
            with self.assertRaises(ValueError):samples.import_sample(source,host)
            self.assertEqual(target.read_text(),'business')

    def test_optional_suites_not_required_for_unimported_hosts(self):
        import run
        matrix={'sessions':['session'],'samples':['sample'],'performance':['perf'],'performance-markers':['marker']}
        self.assertEqual(run.applicable_suites(matrix,{'dependencies':{}}),{'sessions':['session']})
        self.assertEqual(run.applicable_suites(matrix,{'dependencies':{'com.wulato.uikit.acceptance.samples':'file:x'}}),matrix)
    def test_local_asset_guid_and_consumer_scene_preserved(self):
        with tempfile.TemporaryDirectory() as d:
            root=Path(d);source=root/'source';source.mkdir();(source/'Scene.unity').write_text('upstream');(source/'Scene.unity.meta').write_text('guid: upstream')
            host=root/'host';samples.import_sample(source,host);target=host/'Assets/Samples/Wulato UIKit/source';(target/'Scene.unity').write_text('consumer');(target/'Scene.unity.meta').write_text('guid: original')
            samples.import_sample(source,host);self.assertEqual((target/'Scene.unity').read_text(),'consumer');self.assertEqual((target/'Scene.unity.meta').read_text(),'guid: original')

    def test_business_flow_is_imported_with_integrated_main_package(self):
        with tempfile.TemporaryDirectory() as folder:
            root=Path(folder);host=root/'host';(host/'Packages').mkdir(parents=True)
            manifest=host/'Packages/manifest.json'
            manifest.write_text(json.dumps({'dependencies':{'com.wulato.uikit.navigation':'file:nav'}}))
            self.assertNotIn('BusinessFlow',[p.name for p in samples.sample_sources(root,host)])
            deps={'com.wulato.uikit':'file:main'}
            manifest.write_text(json.dumps({'dependencies':deps}))
            self.assertIn('BusinessFlow',[p.name for p in samples.sample_sources(root,host)])
