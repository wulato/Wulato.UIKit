using UnityEditor;
using UnityEngine;
namespace Wulato.UIKit.Verification.Editor
{
    public static class FixtureBuilder
    {
        public static void Create()
        {
            System.IO.Directory.CreateDirectory("Assets/Resources");
            var root=new GameObject("LifecycleFixture");
            var dependency=new GameObject("SerializedDependency"); dependency.transform.SetParent(root.transform);
            root.AddComponent<LifecycleProbe>().SerializedDependency=dependency;
            PrefabUtility.SaveAsPrefabAsset(root,"Assets/Resources/LifecycleFixture.prefab");
            Object.DestroyImmediate(root);
            AssetDatabase.SaveAssets();
        }
    }
}
