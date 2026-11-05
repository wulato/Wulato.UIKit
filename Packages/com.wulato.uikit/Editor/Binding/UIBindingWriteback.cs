using System;
using UnityEditor;
using UnityEngine;
namespace Wulato.UIKit.Editor
{
    internal static class UIBindingWriteback
    {
        internal static void Apply(ViewInput input)
        {
            var root=PrefabUtility.LoadPrefabContents(input.Path);
            try
            {
                var marker=root.GetComponent<UIBindingMarker>();
                var view=root.GetComponent(input.Type)??root.AddComponent(input.Type);
                var so=new SerializedObject(view);
                foreach(var binding in marker.bindings)
                {
                    var field=so.FindProperty(binding.fieldName);
                    if(field==null || field.propertyType!=SerializedPropertyType.ObjectReference)throw new InvalidOperationException(input.Path+": Compile binding field before writeback: "+binding.fieldName);
                    field.objectReferenceValue=binding.target;
                }
                so.ApplyModifiedPropertiesWithoutUndo();
                PrefabUtility.SaveAsPrefabAsset(root,input.Path);
            }
            finally {PrefabUtility.UnloadPrefabContents(root);}
        }
    }
}
