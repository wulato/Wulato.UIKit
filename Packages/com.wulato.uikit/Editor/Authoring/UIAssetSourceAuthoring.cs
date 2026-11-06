using System;
using System.Reflection;
using UnityEditor;
using UnityEngine;
namespace Wulato.UIKit.Editor.Authoring
{
    internal static class UIAssetSourceAuthoring
    {
        // 0 = prefab reference, 1 = existing Addressables entry, 2 = explicit custom backend.
        public static void Apply(string path,int source,string provider,string id)
        {
            UIViewAuthoring.ValidatePrefab(path);
            var prefab=AssetDatabase.LoadAssetAtPath<GameObject>(path);var marker=prefab.GetComponent<UIBindingMarker>();
            if(!marker)throw new ArgumentException("先配置 View 与 Marker。");
            if(source<0||source>2)throw new ArgumentOutOfRangeException(nameof(source));
            if(source==0){provider="";id="";}
            else
            {
                if(source==1)provider="addressables";
                UIViewAsset.External(provider,id);
                if(provider=="addressables")
                {
                    var validator=Type.GetType("Wulato.UIKit.Editor.AddressablesUIValidator, Wulato.UIKit.Addressables.Editor",false);
                    if(validator==null)throw new InvalidOperationException("Addressables SDK / UIKit Editor integration is not installed; no source was changed.");
                    try{validator.GetMethod("ValidateSource",BindingFlags.Public|BindingFlags.Static).Invoke(null,new object[]{prefab,id});}
                    catch(TargetInvocationException error){throw error.InnerException;}
                }
            }
            if(marker.externalProviderId==provider&&marker.externalAssetId==id)return;
            Undo.IncrementCurrentGroup();int group=Undo.GetCurrentGroup();Undo.SetCurrentGroupName("Configure UIKit asset source");
            Undo.RecordObject(marker,"Configure UIKit asset source");
            var serialized=new SerializedObject(marker);serialized.FindProperty("externalProviderId").stringValue=provider;serialized.FindProperty("externalAssetId").stringValue=id;serialized.ApplyModifiedProperties();
            EditorUtility.SetDirty(marker);AssetDatabase.SaveAssetIfDirty(marker);Undo.CollapseUndoOperations(group);
        }
    }
}
