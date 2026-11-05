using System;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
namespace Wulato.UIKit.Editor
{
    public static class UIBindingGenerator
    {
        public static void CreateUserPartial(string path,string ns,string className)
        {
            if(File.Exists(path))return;
            if(!UICodegenPipeline.Identifier(className) || (!string.IsNullOrEmpty(ns) && ns.Split('.').Any(x=>!UICodegenPipeline.Identifier(x))))throw new ArgumentException("Invalid C# identifier.");
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            File.WriteAllText(path,"using Wulato.UIKit;\n"+(string.IsNullOrEmpty(ns)?"":"namespace "+ns+" {\n")+"public partial class "+className+" : UIView { }\n"+(string.IsNullOrEmpty(ns)?"":"}\n"));
        }
        internal static string Generate(ViewInput view)
        {
            var text=new StringBuilder(UICodegenPipeline.Header);
            text.Append("// input: ").Append(view.Fingerprint).Append("\n");
            if(!string.IsNullOrEmpty(view.Type.Namespace))text.Append("namespace ").Append(view.Type.Namespace).Append(" {\n");
            text.Append(view.Type.IsPublic?"public ":"internal ").Append("partial class ").Append(view.Type.Name).Append(" {\n");
            foreach(var binding in view.Marker.bindings)text.Append("[global::UnityEngine.SerializeField] private ").Append(UICodegenPipeline.TypeName(binding.target.GetType())).Append(' ').Append(binding.fieldName).Append(";\n");
            text.Append("}\n");if(!string.IsNullOrEmpty(view.Type.Namespace))text.Append("}\n");return text.ToString();
        }
    }
}
