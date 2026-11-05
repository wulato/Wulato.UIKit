using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;
namespace Wulato.UIKit.Editor.Authoring
{
    internal sealed class UIPreviewRuntime
    {
        readonly UIPreviewProfile profile;
        readonly UIContextHost host;
        readonly CancellationTokenSource cancellation=new CancellationTokenSource();
        UIPreviewAdapter adapter;
        bool configured,busy,released,stopping;
        UniTaskCompletionSource<UIShutdownCompletion> stopped;
        public bool Busy=>busy;
        public IUISession Session { get; private set; }
        public IUIContext Context=>host.Context;
        public UIPreviewRuntime(UIPreviewProfile profile,UIContextHost host)
        {
            this.profile=profile;this.host=host;
            if(profile.adapter)adapter=UnityEngine.Object.Instantiate(profile.adapter);
        }
        public async UniTask OpenAsync()
        {
            if(busy||stopping)throw new InvalidOperationException("A preview command is already running or stopping.");
            if(Session!=null&&Session.State!=UIState.Closed)throw new InvalidOperationException("Close the current Session before reopening.");
            busy=true;
            try
            {
                if(!configured){host.Initialize(b=>adapter?.Configure(b));configured=true;}
                var args=adapter==null?UIArguments.None:adapter.CreateArguments();
                if(!profile.registry.CopyValidated().Get(profile.view).Accepts(args))throw new ArgumentException("Preview arguments do not match the registered View. Provide an explicit adapter; typed null requires UIArguments.From<T>.");
                Session=await Context.OpenAsync(profile.view,args,cancellation.Token);
            }
            finally{busy=false;}
        }
        public async UniTask CloseAsync()
        {
            if(busy||stopping)throw new InvalidOperationException("A preview command is already running or stopping.");
            if(Session==null||Session.State==UIState.Closed)return;
            busy=true;try{await Session.CloseAsync();}finally{busy=false;}
        }
        public UniTask<UIShutdownCompletion> ShutdownAsync()
        {
            if(stopped!=null)return stopped.Task;
            stopping=true;stopped=new UniTaskCompletionSource<UIShutdownCompletion>();Stop().Forget();return stopped.Task;
        }
        async UniTaskVoid Stop()
        {
            UIShutdownCompletion result=null;Exception primary=null;
            try{cancellation.Cancel();}catch(Exception error){primary=error;}
            try{result=await host.ShutdownAsync();}catch(Exception error){primary=primary==null?error:new AggregateException(primary,error);}
            finally
            {
                cancellation.Dispose();
                if(!released)
                {
                    released=true;
                    try
                    {
                        if(adapter){adapter.ShutdownResult=result;adapter.Release();}
                    }
                    catch(Exception error){primary=primary==null?error:new AggregateException(primary,error);}
                    // A pending adapter may still own late tasks; its Release hook owns that lifetime.
                    if(adapter&&(result==null||result.PendingCleanupCount==0))UnityEngine.Object.DestroyImmediate(adapter);
                    adapter=null;
                }
            }
            if(primary!=null)stopped.TrySetException(primary);else stopped.TrySetResult(result);
        }
    }
}
