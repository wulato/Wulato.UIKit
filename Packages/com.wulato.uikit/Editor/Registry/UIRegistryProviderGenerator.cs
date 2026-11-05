using System.Text;
namespace Wulato.UIKit.Editor
{
    internal static class UIRegistryProviderGenerator
    {
        internal static string Generate(ViewInput[] views,string className)
        {
            var text=new StringBuilder(UICodegenPipeline.Header);
            text.Append("namespace Wulato.UIKit.Generated { public sealed class ").Append(className).Append(" : global::Wulato.UIKit.UIRegistryProvider {\n");
            for(int i=0;i<views.Length;i++)if(string.IsNullOrEmpty(views[i].Marker.externalProviderId))text.Append("[global::UnityEngine.SerializeField] private global::UnityEngine.GameObject prefab").Append(i).Append(";\n");
            text.Append("public override void Populate(global::Wulato.UIKit.UIRegistryBuilder builder) {\n");
            for(int i=0;i<views.Length;i++)
            {
                var v=views[i];text.Append("builder.Add<").Append(UICodegenPipeline.TypeName(v.Type));
                if(v.Args!=null)text.Append(',').Append(UICodegenPipeline.TypeName(v.Args));
                text.Append(">(new global::Wulato.UIKit.UIViewTypeKey(").Append(UICodegenPipeline.Literal(v.Key)).Append("), global::Wulato.UIKit.UIViewAsset.");
                if(string.IsNullOrEmpty(v.Marker.externalProviderId))text.Append("Direct(prefab").Append(i).Append("), ");
                else text.Append("External(").Append(UICodegenPipeline.Literal(v.Marker.externalProviderId)).Append(',').Append(UICodegenPipeline.Literal(v.Marker.externalAssetId)).Append("), ");
                text.Append("(global::Wulato.UIKit.UILayer)").Append((int)v.Marker.layer).Append(", (global::Wulato.UIKit.UIMultiplicity)").Append((int)v.Marker.multiplicity).Append(", (global::Wulato.UIKit.UIClosePolicy)").Append((int)v.Marker.closePolicy).Append(", (global::Wulato.UIKit.UIBackAction)").Append((int)v.Marker.backAction).Append(");\n");
            }
            return text.Append("} } }\n").ToString();
        }
    }
}
