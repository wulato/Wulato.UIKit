using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using Wulato.UIKit.Tests.Support;
namespace Wulato.UIKit.Tests.Fixtures
{
    public static class T4FixtureBuilder
    {
        public static void Create()
        {
            const string dir = "Packages/com.wulato.uikit/Tests/Fixtures/Resources/T4";
            var package = UnityEditor.PackageManager.PackageInfo.FindForAssembly(typeof(T4ProbeView).Assembly);
            Directory.CreateDirectory(Path.Combine(package.resolvedPath, "Tests/Fixtures/Resources/T4"));
            AssetDatabase.Refresh();
            var go = new GameObject("T4Probe", typeof(RectTransform), typeof(T4ProbeView), typeof(Image));
            var rect = go.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(.15f, .2f); rect.anchorMax = new Vector2(.65f, .7f);
            rect.pivot = new Vector2(.23f, .81f); rect.sizeDelta = new Vector2(123, 77);
            rect.anchoredPosition = new Vector2(17, -13); rect.localScale = new Vector3(.9f, 1.1f, 1);
            var content = new GameObject("Content", typeof(RectTransform), typeof(VerticalLayoutGroup), typeof(ContentSizeFitter));
            content.transform.SetParent(go.transform, false);
            var group = content.GetComponent<VerticalLayoutGroup>();
            group.padding = new RectOffset(11, 13, 5, 9); group.spacing = 7;
            group.childControlWidth = group.childControlHeight = true;
            group.childForceExpandWidth = group.childForceExpandHeight = false;
            var fitter = content.GetComponent<ContentSizeFitter>();
            fitter.horizontalFit = fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            for (int i = 0; i < 2; i++)
            {
                var child = new GameObject("Item" + i, typeof(RectTransform), typeof(LayoutElement));
                child.transform.SetParent(content.transform, false);
                var element = child.GetComponent<LayoutElement>();
                element.preferredWidth = i == 0 ? 90 : 120; element.preferredHeight = i == 0 ? 30 : 40;
            }
            go.GetComponent<T4ProbeView>().SerializedChild = content.GetComponent<RectTransform>();
            PrefabUtility.SaveAsPrefabAsset(go, dir + "/Probe.prefab");
            var nestedCanvas = go.AddComponent<Canvas>();
            var serializedCanvas = new SerializedObject(nestedCanvas);
            serializedCanvas.FindProperty("m_OverrideSorting").boolValue = true;
            serializedCanvas.ApplyModifiedPropertiesWithoutUndo();
            PrefabUtility.SaveAsPrefabAsset(go, dir + "/OverrideCanvas.prefab");
            Object.DestroyImmediate(go); AssetDatabase.SaveAssets();
        }
    }
}
