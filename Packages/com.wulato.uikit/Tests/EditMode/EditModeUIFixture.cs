using System;
using System.Reflection;
using UnityEngine;
using UnityEngine.EventSystems;
namespace Wulato.UIKit.Tests.EditMode
{
    internal static class EditModeUIFixture
    {
        internal static GameObject CreateEvents(string name,bool makeCurrent=true)
        {
            var events=new GameObject(name,typeof(EventSystem));
#if UIKIT_VERIFY_INPUT_SYSTEM
            var type=Type.GetType("Wulato.UIKit.InputSystem.InputSystemUIInputDriver, Wulato.UIKit.InputSystem",true);
#else
            var type=typeof(LegacyUIInputDriver);
#endif
            var module=(BaseInputModule)events.AddComponent(type);
            typeof(EventSystem).GetMethod("OnEnable",BindingFlags.Instance|BindingFlags.NonPublic).Invoke(events.GetComponent<EventSystem>(),null);
            typeof(BaseInputModule).GetMethod("OnEnable",BindingFlags.Instance|BindingFlags.NonPublic).Invoke(module,null);
            if(makeCurrent)EventSystem.current=events.GetComponent<EventSystem>();
            return events;
        }
        internal static void DestroyEvents(GameObject events)
        {
            if(!events)return;
            typeof(EventSystem).GetMethod("OnDisable",BindingFlags.Instance|BindingFlags.NonPublic).Invoke(events.GetComponent<EventSystem>(),null);
            UnityEngine.Object.DestroyImmediate(events);
        }
    }
}
