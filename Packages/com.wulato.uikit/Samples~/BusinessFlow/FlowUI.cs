using System;
using UnityEngine;
using UnityEngine.UI;
namespace Wulato.UIKit.Samples.BusinessFlow
{
    public readonly struct FlowActions
    {
        public Action Settings{get;}public Action Back{get;}public Action Confirm{get;}public Action Rewards{get;}
        public FlowActions(Action settings,Action back,Action confirm,Action rewards){Settings=settings;Back=back;Confirm=confirm;Rewards=rewards;}
    }
    public static class FlowUI
    {
        static GameObject Node(Transform parent,string name,float y)
        {
            var go=new GameObject(name,typeof(RectTransform));go.transform.SetParent(parent,false);
            var rect=(RectTransform)go.transform;rect.sizeDelta=new Vector2(420,52);rect.anchoredPosition=new Vector2(0,y);return go;
        }
        public static Text Label(Transform parent,string text,float y)
        {
            var label=Node(parent,text,y).AddComponent<Text>();label.font=Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");label.fontSize=22;label.alignment=TextAnchor.MiddleCenter;label.text=text;label.color=Color.white;return label;
        }
        public static void Button(Transform parent,string title,float y,Action action)
        {
            var node=Node(parent,title,y);var image=node.AddComponent<Image>();image.color=new Color(.16f,.35f,.50f);
            var button=node.AddComponent<UnityEngine.UI.Button>();button.targetGraphic=image;button.onClick.AddListener(()=>action());Label(node.transform,title,0);
        }
    }
}
