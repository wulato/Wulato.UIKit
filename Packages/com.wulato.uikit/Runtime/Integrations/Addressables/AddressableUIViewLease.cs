using System;
using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.ResourceManagement.AsyncOperations;
using Object = UnityEngine.Object;
namespace Wulato.UIKit
{
    internal sealed class AddressableUIViewLease : IUIViewLease
    {
        GameObject instance;
        UIViewBase view;
        readonly AsyncOperationHandle<GameObject> handle;
        readonly IAddressableOperations operations;
        bool disposed;
        internal AddressableUIViewLease(GameObject instance,UIViewBase view,AsyncOperationHandle<GameObject> handle,IAddressableOperations operations)
        {this.instance=instance;this.view=view;this.handle=handle;this.operations=operations;}
        public UIViewBase View => disposed?null:view;
        public void Dispose()
        {
            if(!PlayerLoopHelper.IsMainThread)throw new InvalidOperationException("UI leases must be released on the main thread.");
            if(disposed)return;
            disposed=true;var owned=instance;instance=null;view=null;
            Retire(owned,handle,operations);
        }
        internal static void Retire(GameObject instance,AsyncOperationHandle<GameObject> handle,IAddressableOperations operations)
        {
            if(!instance){if(handle.IsValid())operations.Release(handle);return;}
            try
            {
                instance.SetActive(false);instance.transform.SetParent(null,false);
                if(!Application.isPlaying)Object.DestroyImmediate(instance);
                else Object.Destroy(instance);
            }
            catch
            {
                // Cleanup still owns the resource if a Unity object was destroyed by a synchronous callback.
                if(instance)Object.Destroy(instance);
                ReleaseAfterDestruction(instance,handle,operations).Forget(Debug.LogException);
                throw;
            }
            // Resource release is terminal. A release failure must not enter the instance cleanup fallback.
            if(!Application.isPlaying){if(handle.IsValid())operations.Release(handle);}
            else ReleaseAfterDestruction(instance,handle,operations).Forget(Debug.LogException);
        }
        static async UniTask ReleaseAfterDestruction(GameObject instance,AsyncOperationHandle<GameObject> handle,IAddressableOperations operations)
        {
            while(instance)await UniTask.NextFrame();
            if(handle.IsValid())operations.Release(handle);
        }
    }
}
