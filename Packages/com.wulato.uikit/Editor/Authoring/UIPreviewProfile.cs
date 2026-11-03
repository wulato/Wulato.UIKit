using UnityEditor;
using UnityEngine;
namespace Wulato.UIKit.Editor.Authoring
{
    [CreateAssetMenu(menuName="Wulato/UIKit/Preview Profile")]
    public sealed class UIPreviewProfile : ScriptableObject
    {
        public UIRegistry registry;
        public UIViewTypeKey view;
        public MonoScript inputDriver;
        public GameObject rootTemplate;
        public UIPreviewAdapter adapter;
    }
}
