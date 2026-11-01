using System;
using System.Linq;
using UnityEditor;
using UnityEngine;
namespace Wulato.UIKit.Editor
{
    internal static class UIRegistryGenerator
    {
        internal static void Write(ViewInput[] views)
        {
            var providers=views.GroupBy(v=>v.Route.Directory).OrderBy(g=>g.Key,StringComparer.Ordinal).Select(group=>
            {
                var first=group.First();var name=UICodegenPipeline.ProviderName(first.Route);
                var script=AssetDatabase.LoadAssetAtPath<MonoScript>(first.Route.Directory+"/"+name+".cs");
                var type=script==null?null:script.GetClass();
                if(type==null || !typeof(UIRegistryProvider).IsAssignableFrom(type))throw new InvalidOperationException("Compile generated provider before writeback: "+group.Key);
                var path=first.Route.Directory+"/"+name+".asset";
                var provider=AssetDatabase.LoadAssetAtPath<UIRegistryProvider>(path);
                if(provider==null){provider=(UIRegistryProvider)ScriptableObject.CreateInstance(type);AssetDatabase.CreateAsset(provider,path);}
                if(provider.GetType()!=type)throw new InvalidOperationException(path+": Provider type mismatch.");
                var so=new SerializedObject(provider);int i=0;
                foreach(var v in group)so.FindProperty("prefab"+i++).objectReferenceValue=v.Prefab;
                so.ApplyModifiedPropertiesWithoutUndo();return provider;
            }).ToArray();
            var registry=AssetDatabase.LoadAssetAtPath<UIRegistry>(UICodegenPipeline.RegistryPath);
            if(registry==null){registry=ScriptableObject.CreateInstance<UIRegistry>();AssetDatabase.CreateAsset(registry,UICodegenPipeline.RegistryPath);}
            var serialized=new SerializedObject(registry);var list=serialized.FindProperty("providers");list.arraySize=providers.Length;
            for(int i=0;i<providers.Length;i++)list.GetArrayElementAtIndex(i).objectReferenceValue=providers[i];
            serialized.ApplyModifiedPropertiesWithoutUndo();registry.CopyValidated();
        }
    }
}
