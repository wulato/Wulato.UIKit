using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Compilation;
namespace Wulato.UIKit.Editor
{
    public sealed class UIBindingRoute
    {
        public string Assembly, AsmdefGuid, Directory;
    }
    public static class UIBindingAssemblyRouter
    {
        public static UIBindingRoute Resolve(string scriptPath)
        {
            var assembly=CompilationPipeline.GetAssemblyNameFromScriptPath(scriptPath);
            assembly=Path.GetFileNameWithoutExtension(assembly);
            if(string.IsNullOrEmpty(assembly) || !CompilationPipeline.GetAssemblies(AssembliesType.Player).Any(a=>a.name==assembly && (a.flags&AssemblyFlags.EditorAssembly)==0))
                throw new InvalidOperationException(scriptPath+": View must belong to a runtime assembly.");
            var definition=CompilationPipeline.GetAssemblyDefinitionFilePathFromAssemblyName(assembly);
            var guid=string.IsNullOrEmpty(definition)?"":AssetDatabase.AssetPathToGUID(definition);
            if(string.IsNullOrEmpty(guid) && assembly!="Assembly-CSharp")throw new InvalidOperationException(scriptPath+": Missing assembly definition.");
            return new UIBindingRoute{Assembly=assembly,AsmdefGuid=guid,Directory=UICodegenPipeline.Root+(guid==""?"/Predefined/Assembly-CSharp":"/Assemblies/"+guid)};
        }
        [Serializable] sealed class AssemblyReference { public string reference; }
        internal static void ValidateLayout(UIBindingRoute route)
        {
            var folder=route.Directory;
            while(folder!="Assets" && !string.IsNullOrEmpty(folder))
            {
                if(System.IO.Directory.Exists(folder))foreach(var path in System.IO.Directory.GetFiles(folder))
                {
                    if(path.EndsWith(".asmdef") || path.EndsWith(".asmref") && (folder!=route.Directory || route.AsmdefGuid=="" || UnityEngine.JsonUtility.FromJson<AssemblyReference>(File.ReadAllText(path)).reference!="GUID:"+route.AsmdefGuid))
                        throw new InvalidOperationException(path+": Wrong assembly route configuration.");
                }
                folder=Path.GetDirectoryName(folder)?.Replace('\\','/');
            }
        }
        public static void Validate(string path,UIBindingRoute route)
        {
            var actual=Path.GetFileNameWithoutExtension(CompilationPipeline.GetAssemblyNameFromScriptPath(path));
            if(actual!=route.Assembly)throw new InvalidOperationException(path+": Wrong assembly route: expected "+route.Assembly+", actual "+actual);
        }
    }
}
