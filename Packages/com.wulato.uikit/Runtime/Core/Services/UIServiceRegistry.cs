using System;
using System.Collections.Generic;
namespace Wulato.UIKit
{
    internal sealed class UIServiceRegistry : IDisposable
    {
        internal readonly Dictionary<Type, object> Values = new Dictionary<Type, object>();
        readonly List<object> owned = new List<object>();
        internal void Add(Type type, object value)
        {
            if(value == null) throw new InvalidOperationException("A service factory returned null.");
            Values.Add(type,value);
            if(!owned.Exists(item=>ReferenceEquals(item,value))) owned.Add(value);
        }
        internal void Release(Action<Exception> error)
        {
            for(int i=owned.Count-1;i>=0;i--)
                try { (owned[i] as IDisposable)?.Dispose(); } catch(Exception e) { error(e); }
            owned.Clear();Values.Clear();
        }
        public void Dispose()=>Release(_=>{});
    }
}
