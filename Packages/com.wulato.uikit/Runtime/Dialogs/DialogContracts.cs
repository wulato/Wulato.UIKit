using System;
using System.Threading;
using Cysharp.Threading.Tasks;
namespace Wulato.UIKit.Dialogs
{
    /// <summary>Explicitly registered immutable request with a business result type.</summary>
    public interface IDialogRequest<TResult>{}
    /// <summary>One request/Session result channel; stale responders return false.</summary>
    public interface IDialogResponder<TResult>
    {
        /// <summary>Accept one submitted value, including false, null or default.</summary>
        bool TrySubmit(TResult value);
        /// <summary>Accept a user cancellation intent.</summary>
        bool TryCancel();
    }
    /// <summary>Dialog business results; caller cancellation covers the whole waiting period.</summary>
    public interface IUIDialogs
    {
        /// <summary>Wait for successful opening and the Closed barrier; caller cancellation throws after cleanup.</summary>
        UniTask<DialogResult<TResult>> ShowAsync<TResult>(IDialogRequest<TResult> request,CancellationToken cancellationToken=default);
    }
    /// <summary>Business termination; caller cancellation is an OperationCanceledException.</summary>
    public enum DialogStatus
    {
        /// <summary>A responder submitted a value.</summary>
        Submitted,
        /// <summary>The user or ordinary owner closed without a submitted value.</summary>
        UserCanceled,
        /// <summary>The Context or Root stopped before result publication.</summary>
        ContextStopped
    }
    /// <summary>Business result with the actual close errors and pending-cleanup information.</summary>
    public sealed class DialogResult<TResult>
    {
        readonly TResult value;
        /// <summary>Terminal business status.</summary>
        public DialogStatus Status{get;}
        /// <summary>Submitted value; throws when Status is not Submitted.</summary>
        public TResult Value=>Status==DialogStatus.Submitted?value:throw new InvalidOperationException("No submitted value.");
        /// <summary>Closed result, or null when Context exit prevented opening a Session.</summary>
        public UICompletion Completion{get;}
        internal DialogResult(DialogStatus status,TResult value,UICompletion completion){Status=status;this.value=value;Completion=completion;}
    }
    /// <summary>Session-specific typed parameters passed to the registered dialog View.</summary>
    public readonly struct DialogArguments<TRequest,TResult> where TRequest:IDialogRequest<TResult>
    {
        /// <summary>Caller-owned request data.</summary>
        public TRequest Request{get;}
        /// <summary>Result channel for this opening only.</summary>
        public IDialogResponder<TResult> Responder{get;}
        /// <summary>Construct typed parameters without transferring request ownership.</summary>
        public DialogArguments(TRequest request,IDialogResponder<TResult> responder){Request=request;Responder=responder;}
    }
}
