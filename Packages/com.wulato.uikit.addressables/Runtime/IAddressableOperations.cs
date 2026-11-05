using System.Collections.Generic;
using UnityEngine;
using UnityEngine.ResourceManagement.AsyncOperations;
using UnityEngine.ResourceManagement.ResourceLocations;
namespace Wulato.UIKit
{
    // Internal boundary around the external resource system, not a public backend registry.
    internal interface IAddressableOperations
    {
        AsyncOperationHandle<IList<IResourceLocation>> Locate(string key);
        AsyncOperationHandle<GameObject> Load(IResourceLocation location);
        void Release<T>(AsyncOperationHandle<T> operation);
    }
}
