using System;
using System.Collections.Generic;
namespace Wulato.UIKit.Dialogs
{
    /// <summary>Explicit request mappings, copied when the dialog service is created.</summary>
    public sealed class DialogRegistry
    {
        internal sealed class Entry
        {
            internal UIViewTypeKey Key;internal Type ResultType,ArgumentsType;
            internal Func<object,object,UIArguments> Bind;
        }
        readonly Dictionary<Type,Entry> entries=new Dictionary<Type,Entry>();
        /// <inheritdoc/>
        public DialogRegistry Register<TRequest,TResult>(UIViewTypeKey viewKey) where TRequest:IDialogRequest<TResult>
        {
            entries.Add(typeof(TRequest),new Entry{Key=viewKey,ResultType=typeof(TResult),ArgumentsType=typeof(DialogArguments<TRequest,TResult>),
                Bind=(request,responder)=>UIArguments.From(new DialogArguments<TRequest,TResult>((TRequest)request,(IDialogResponder<TResult>)responder))});
            return this;
        }
        internal Dictionary<Type,Entry> Copy(UIRegistrySnapshot views)
        {
            var result=new Dictionary<Type,Entry>(entries);
            foreach(var entry in result.Values)
                if(views.Get(entry.Key).ArgumentType!=entry.ArgumentsType)throw new ArgumentException("Dialog View arguments do not match the explicit request mapping.");
            return result;
        }
    }
}
