using System;
using System.Collections.Generic;
using System.Linq;

namespace Wulato.UIKit
{
    internal sealed class UICachedInstance
    {
        internal UIRegistration Registration;
        internal UISessionId LastSessionId;
        internal IUIViewHost Host;
        internal IUIViewLease Lease;
        internal UIViewBase View;
        internal UIInstanceScope Instance;
    }
    internal sealed class UIViewCache
    {
        readonly int capacity;
        readonly LinkedList<UICachedInstance> lru = new LinkedList<UICachedInstance>();
        readonly Dictionary<UIViewTypeKey, LinkedListNode<UICachedInstance>> entries = new Dictionary<UIViewTypeKey, LinkedListNode<UICachedInstance>>();
        internal int Capacity => capacity;
        internal UIViewCache(int capacity) { this.capacity = capacity; }
        internal IReadOnlyList<UIViewTypeKey> Keys => lru.Select(e => e.Registration.Key).ToArray();
        internal UICachedInstance Take(UIViewTypeKey key)
        {
            if (!entries.TryGetValue(key, out var node)) return null;
            entries.Remove(key); lru.Remove(node); return node.Value;
        }
        internal void Put(UICachedInstance item, Action<UICachedInstance> evict)
        {
            var previous = Take(item.Registration.Key); if (previous != null) evict(previous);
            if (capacity == 0) { evict(item); return; }
            entries.Add(item.Registration.Key, lru.AddLast(item));
            while (entries.Count > capacity)
            {
                var oldest = lru.First.Value; Take(oldest.Registration.Key); evict(oldest);
            }
        }
        internal void Clear(Action<UICachedInstance> release)
        {
            var snapshot = lru.ToArray(); lru.Clear(); entries.Clear();
            foreach (var item in snapshot) release(item);
        }
    }
}
