using System;
using Wulato.UIKit.Editor.Authoring;
namespace Wulato.UIKit.Tests.Preview
{
    public sealed class PreviewTestAdapter : UIPreviewAdapter
    {
        public int mode;
        public static int Configures,Releases;
        public override void Configure(UIContextBuilder builder)
        {Configures++;if(mode==2)throw new ArgumentException("configure failed");}
        public override UIArguments CreateArguments()=>mode==1?UIArguments.From<int>(123):UIArguments.From<string>(null);
        public override void Release(){Releases++;}
    }
}
