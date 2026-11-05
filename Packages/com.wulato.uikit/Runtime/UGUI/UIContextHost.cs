using System;
using Cysharp.Threading.Tasks;
using UnityEngine;
namespace Wulato.UIKit
{
    /// <summary>Scene-owned composition of an existing Context. Injected dependencies remain caller-owned.</summary>
    [DisallowMultipleComponent]
    public sealed class UIContextHost : MonoBehaviour
    {
        [SerializeField] UIRoot root;
        [SerializeField] UIRegistry registry;
        [SerializeField] bool initializeOnStart=true;
        [SerializeField] UIContextHostSettings settings=new UIContextHostSettings();
        bool shutdownRequested,starting,autoAttempted,shutdownStarted;
        static int playGeneration;
        UniTaskCompletionSource<UIShutdownCompletion> shutdown;
        public IUIContext Context { get; private set; }
        public UIShutdownCompletion ShutdownResult { get; private set; }
        public Exception LastError { get; private set; }
        public IUIContext Initialize(Action<UIContextBuilder> configure=null)
        {
            if(!PlayerLoopHelper.IsMainThread||!Application.isPlaying)throw new InvalidOperationException("Initialize requires PlayMode on the main thread.");
            if(shutdownRequested)throw new InvalidOperationException("This host has already requested Shutdown.");
            if(Context!=null)
            {
                if(configure!=null)throw new InvalidOperationException("Configure is only accepted before the first successful Build.");
                return Context;
            }
            if(starting)throw new InvalidOperationException("Context initialization is already in progress.");
            autoAttempted=true;starting=true;
            try
            {
                var builder=new UIContextBuilder(root,registry).WithSettings((settings??new UIContextHostSettings()).ToSettings());
                configure?.Invoke(builder);
                if(shutdownRequested)throw new InvalidOperationException("Shutdown was requested during initialization.");
                Context=builder.Build();
                if(shutdownRequested)throw new InvalidOperationException("Shutdown was requested during initialization.");
                root.Destroyed+=RootDestroyed;LastError=null;return Context;
            }
            catch(Exception error){LastError=error;throw;}
            finally{starting=false;if(shutdownRequested&&shutdown!=null&&!shutdownStarted)BeginShutdown();}
        }
        public UniTask<UIShutdownCompletion> ShutdownAsync()
        {
            if(!PlayerLoopHelper.IsMainThread)return UniTask.FromException<UIShutdownCompletion>(new InvalidOperationException("Shutdown requires the main thread."));
            if(shutdown!=null)return shutdown.Task;
            shutdownRequested=true;shutdown=new UniTaskCompletionSource<UIShutdownCompletion>();if(!starting)BeginShutdown();return shutdown.Task;
        }
        void BeginShutdown()
        {
            shutdownStarted=true;CompleteShutdown(Context,shutdown).Forget();
        }
        async UniTaskVoid CompleteShutdown(IUIContext context,UniTaskCompletionSource<UIShutdownCompletion> completion)
        {
            try
            {
                var result=context==null?new UIShutdownCompletion(null,null,0):await context.ShutdownAsync();
                if(ReferenceEquals(shutdown,completion))ShutdownResult=result;completion.TrySetResult(result);
            }
            catch(Exception error){if(ReferenceEquals(shutdown,completion))LastError=error;completion.TrySetException(error);}
        }
        void Start()=>StartAutomatically();
        void OnEnable(){if(Application.isPlaying)StartAfterEnable(playGeneration).Forget();}
        async UniTaskVoid StartAfterEnable(int generation)
        {
            await UniTask.Yield(PlayerLoopTiming.Update);
            if(this&&isActiveAndEnabled&&Application.isPlaying&&generation==playGeneration)StartAutomatically();
        }
        void StartAutomatically()
        {
            if(!initializeOnStart||shutdownRequested||Context!=null||autoAttempted)return;
            try{Initialize();}catch(Exception error){Debug.LogException(error,this);}
        }
        void RootDestroyed()=>ShutdownAsync().Forget(Debug.LogException);
        void OnApplicationQuit()=>ShutdownAsync().Forget(Debug.LogException);
        void OnDestroy()
        {
            if(!ReferenceEquals(root,null))root.Destroyed-=RootDestroyed;
            if(Application.isPlaying)ShutdownAsync().Forget(Debug.LogException);
        }
        // Both reloads may be disabled: Unity does not reconstruct this component or call Start again.
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetPlaySession()
        {
            playGeneration++;
            foreach(var host in Resources.FindObjectsOfTypeAll<UIContextHost>())
            {
                if(!host.gameObject.scene.IsValid())continue;
                if(host.Context!=null)host.ShutdownAsync().Forget(Debug.LogException);
                if(!ReferenceEquals(host.root,null))host.root.Destroyed-=host.RootDestroyed;
                host.Context=null;host.shutdown=null;host.shutdownRequested=false;host.starting=false;host.autoAttempted=false;host.shutdownStarted=false;host.ShutdownResult=null;host.LastError=null;
            }
        }
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void RestartRetainedHosts()=>StartRetainedHosts(playGeneration).Forget();
        static async UniTaskVoid StartRetainedHosts(int generation)
        {
            // Fresh components initialize from Start. This fallback runs after Start and also
            // covers retained scene components when both Editor reloads are disabled.
            await UniTask.Yield(PlayerLoopTiming.Update);
            if(!Application.isPlaying||generation!=playGeneration)return;
            foreach(var host in Resources.FindObjectsOfTypeAll<UIContextHost>())
                if(host.gameObject.scene.IsValid()&&host.isActiveAndEnabled)host.StartAutomatically();
        }
    }
}
