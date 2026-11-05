using System;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
namespace Wulato.UIKit
{
    internal sealed class UIModalMask : MonoBehaviour, IPointerClickHandler
    {
        internal Action Click;
        public void OnPointerClick(PointerEventData e) {if(e.button==PointerEventData.InputButton.Left)Click?.Invoke();}
        internal static UIModalMask Create(Transform parent)
        {
            var rect=UIRoot.CreateRect("ModalMask",parent);
            rect.gameObject.AddComponent<Image>().color=new Color(0,0,0,.45f);
            return rect.gameObject.AddComponent<UIModalMask>();
        }
        internal void Place(UIViewBase owner,bool visible,Action click)
        {
            Click=click;
            if(owner&&visible)
            {
                if(transform.parent!=owner.transform.parent)transform.SetParent(owner.transform.parent,false);
                int index=owner.transform.GetSiblingIndex();
                if(transform.GetSiblingIndex()!=Math.Max(0,index-1))transform.SetSiblingIndex(Math.Max(0,index-1));
            }
            if(gameObject.activeSelf!=visible)gameObject.SetActive(visible);
        }
    }
}
