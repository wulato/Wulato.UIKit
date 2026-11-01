using UnityEditor.Build;
using UnityEditor.Build.Reporting;
namespace Wulato.UIKit.Editor
{
    public sealed class UIBuildValidator : IPreprocessBuildWithReport
    {
        public int callbackOrder=>0;
        public void OnPreprocessBuild(BuildReport report)
        {
            try { UICodegenPipeline.Validate(); }
            catch(System.Exception e){throw new BuildFailedException(e.Message);}
        }
    }
}
