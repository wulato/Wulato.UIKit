using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;

namespace Wulato.UIKit
{
    internal static class UIFrozen
    {
        internal static IReadOnlyList<T> Copy<T>(IEnumerable<T> source) => Array.AsReadOnly(source == null ? Array.Empty<T>() : new List<T>(source).ToArray());
    }
    /// <summary>Immutable terminal result; cleanup errors never retain a View handle.</summary>
    public sealed class UICompletion
    {
        public UISessionId SessionId { get; }
        public UICloseReason Reason { get; }
        public IReadOnlyList<Exception> Errors { get; }
        public bool HasPendingCleanup { get; }
        public UICompletion(UISessionId sessionId,UICloseReason reason,IEnumerable<Exception> errors,bool hasPendingCleanup)
        { SessionId=sessionId;Reason=reason;Errors=UIFrozen.Copy(errors);HasPendingCleanup=hasPendingCleanup; }
    }
    public sealed class UIBatchCompletion
    {
        public IReadOnlyList<UICompletion> Completions { get; }
        public IReadOnlyList<Exception> Errors { get; }
        public UIBatchCompletion(IEnumerable<UICompletion> completions,IEnumerable<Exception> errors)
        { Completions=UIFrozen.Copy(completions);Errors=UIFrozen.Copy(errors); }
    }
    public sealed class UIShutdownCompletion
    {
        public IReadOnlyList<UICompletion> Completions { get; }
        public IReadOnlyList<Exception> Errors { get; }
        public int PendingCleanupCount { get; }
        public UIShutdownCompletion(IEnumerable<UICompletion> completions,IEnumerable<Exception> errors,int pendingCleanupCount)
        { if(pendingCleanupCount<0)throw new ArgumentOutOfRangeException(nameof(pendingCleanupCount));Completions=UIFrozen.Copy(completions);Errors=UIFrozen.Copy(errors);PendingCleanupCount=pendingCleanupCount; }
    }
}
