using System;
using System.Collections.Generic;
namespace Wulato.UIKit.Dialogs
{
    internal static class DialogRegistry
    {
        internal sealed class Entry
        {
            internal readonly UIViewTypeKey Key;
            internal readonly Type RequestType,ResultType,ArgumentsType;
            internal readonly Func<object,object,UIArguments> Bind;
            internal Entry(UIViewTypeKey key,Type request,Type result,Type arguments,Func<object,object,UIArguments> bind)
            {Key=key;RequestType=request;ResultType=result;ArgumentsType=arguments;Bind=bind;}
        }
        internal static Dictionary<Type,Entry> Copy(UIRegistrySnapshot views)
        {
            var result=new Dictionary<Type,Entry>();
            foreach(var view in views.Entries)
            {
                var entry=view.Dialog;if(entry==null)continue;
                if(view.ArgumentType!=entry.ArgumentsType||view.Layer!=UILayer.Modal)throw new ArgumentException("Invalid dialog View mapping.");
                result.Add(entry.RequestType,entry);
            }
            return result;
        }
    }
}
