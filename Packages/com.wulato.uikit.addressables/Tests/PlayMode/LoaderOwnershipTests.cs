using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using Cysharp.Threading.Tasks;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.ResourceManagement;
using UnityEngine.ResourceManagement.AsyncOperations;
using UnityEngine.ResourceManagement.ResourceLocations;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;
namespace Wulato.UIKit.AddressablesTests
{
    public sealed class LeaseProbeView : UIView
    {
        public static int Awakes;
        void Awake() => Awakes++;
    }
    [Category("addressables_runtime")]
    public sealed class LoaderOwnershipTests
    {
        static readonly List<string> Measurements=new List<string>();
        [SetUp] public void BeginTrace()=>Measurements.Clear();
        sealed class Pending<T> : AsyncOperationBase<T>
        {
            protected override void Execute() { }
            internal void Succeed(T value) => Complete(value,true,(string)null);
            internal void Fail() => Complete(default,false,"controlled load failure");
        }
        sealed class Operations : IAddressableOperations, IDisposable
        {
            readonly ResourceManager manager = new ResourceManager();
            internal readonly Pending<IList<IResourceLocation>> locations = new Pending<IList<IResourceLocation>>();
            internal readonly Pending<GameObject> asset = new Pending<GameObject>();
            internal AsyncOperationHandle<IList<IResourceLocation>> query;
            internal AsyncOperationHandle<GameObject> load;
            internal int queries,loads,queryReleases,assetReleases;
            internal string key;
            internal GameObject instanceAtRelease;
            internal bool aliveAtRelease, failFirstAssetRelease;
            public AsyncOperationHandle<IList<IResourceLocation>> Locate(string id) { key=id;queries++;return query=manager.StartOperation(locations,default); }
            public AsyncOperationHandle<GameObject> Load(IResourceLocation location) { loads++;return load=manager.StartOperation(asset,default); }
            public void Release<T>(AsyncOperationHandle<T> operation)
            {
                if(typeof(T)==typeof(GameObject)){assetReleases++;aliveAtRelease=instanceAtRelease!=null;Measurements.Add("asset-release:alive="+aliveAtRelease);}
                else {queryReleases++;Measurements.Add("query-release");}
                if(typeof(T)==typeof(GameObject) && failFirstAssetRelease && assetReleases==1)throw new InvalidOperationException("controlled release failure");
                manager.Release(operation);
            }
            internal void Resolve(int count=1,string primaryKey="asset-id")
            {
                var values=new List<IResourceLocation>();
                for(int i=0;i<count;i++)values.Add(new ResourceLocationBase(primaryKey,"internal"+i,"provider",typeof(GameObject)));
                locations.Succeed(values);
            }
            public void Dispose() => manager.Dispose();
        }
        sealed class Fixture : IDisposable
        {
            internal readonly Operations ops=new Operations();
            internal readonly GameObject parent,sourceParent,prefab;
            internal readonly AddressablesUIViewLoader loader;
            internal Fixture(bool valid=true)
            {
                LeaseProbeView.Awakes=0;
                parent=new GameObject("lease-parent");parent.SetActive(false);
                sourceParent=new GameObject("source-parent");sourceParent.SetActive(false);
                prefab=new GameObject("lease-prefab",typeof(RectTransform));prefab.transform.SetParent(sourceParent.transform,false);
                if(valid)prefab.AddComponent<LeaseProbeView>();
                loader=new AddressablesUIViewLoader(ops);
            }
            internal UniTask<IUIViewLease> Start(CancellationToken ct=default,string id="asset-id") => loader.CreateAsync(UIViewAsset.External("addressables",id),parent.transform,ct);
            internal async UniTask<IUIViewLease> Loaded()
            {
                var operation=Start();ops.Resolve();await UniTask.NextFrame();ops.asset.Succeed(prefab);return await operation;
            }
            public void Dispose(){Object.DestroyImmediate(parent);Object.DestroyImmediate(sourceParent);ops.Dispose();}
        }
        // Invoked from the Editor suite to exercise the synchronous DestroyImmediate branch.
        public static void AssertEditorReleaseFailureIsNotRetried()
        {
            Assert.That(Application.isPlaying,Is.False);
            using(var f=new Fixture())
            {
                var handle=f.ops.Load(null);f.ops.asset.Succeed(f.prefab);f.ops.failFirstAssetRelease=true;
                var lease=new AddressableUIViewLease(f.prefab,f.prefab.GetComponent<LeaseProbeView>(),handle,f.ops);
                Assert.That(Assert.Throws<InvalidOperationException>(()=>lease.Dispose()).Message,Is.EqualTo("controlled release failure"));
                lease.Dispose();Assert.That(lease.View,Is.Null);
                Assert.That(f.prefab==null,Is.True);
                Assert.That(f.ops.assetReleases,Is.EqualTo(1),"A failed release must not schedule a second release attempt.");
            }
        }
        static async UniTask Settle() { for(int i=0;i<4;i++)await UniTask.NextFrame(); }
        [UnityTest] public IEnumerator Lease_StaysInactiveAndReleasesAfterActualDestruction() => UniTask.ToCoroutine(async()=>
        {
            using(var f=new Fixture())
            {
                var lease=await f.Loaded();var instance=lease.View.gameObject;f.ops.instanceAtRelease=instance;
                Assert.That(instance.transform.parent,Is.SameAs(f.parent.transform));Assert.That(instance.activeSelf,Is.True);
                Assert.That(instance.activeInHierarchy,Is.False);Assert.That(LeaseProbeView.Awakes,Is.Zero);
                Assert.That(f.ops.key,Is.EqualTo("asset-id"));Assert.That(f.ops.queryReleases,Is.EqualTo(1));Assert.That(f.ops.assetReleases,Is.Zero);
                lease.Dispose();lease.Dispose();Assert.That(lease.View,Is.Null);
                Assert.That(f.ops.assetReleases,Is.Zero,"Destroy is deferred; dependencies must survive until destruction");
                await Settle();Assert.That(instance==null,Is.True);Assert.That(f.ops.assetReleases,Is.EqualTo(1));Assert.That(f.ops.aliveAtRelease,Is.False);
            }
        });
        [UnityTest] public IEnumerator ExternalDestroy_StillReleasesAssetExactlyOnce() => UniTask.ToCoroutine(async()=>
        {
            using(var f=new Fixture()) { var lease=await f.Loaded();Object.Destroy(lease.View.gameObject);await Settle();lease.Dispose();lease.Dispose();Assert.That(f.ops.assetReleases,Is.EqualTo(1)); }
        });
        [UnityTest] public IEnumerator CancelDuringQuery_DrainsQueryWithoutStartingAssetLoad() => UniTask.ToCoroutine(async()=>
        {
            using(var f=new Fixture())using(var ct=new CancellationTokenSource())
            {
                var op=f.Start(ct.Token);ct.Cancel();Assert.That(op.Status,Is.EqualTo(UniTaskStatus.Pending));f.ops.Resolve();
                bool canceled=false;try{await op;}catch(OperationCanceledException){canceled=true;}
                Assert.That(canceled,Is.True);Assert.That(f.ops.loads,Is.Zero);Assert.That(f.ops.queryReleases,Is.EqualTo(1));Assert.That(f.parent.transform.childCount,Is.Zero);
            }
        });
        [UnityTest] public IEnumerator CancelDuringAssetLoad_DrainsAndReleasesWithoutInstantiation() => UniTask.ToCoroutine(async()=>
        {
            using(var f=new Fixture())using(var ct=new CancellationTokenSource())
            {
                var op=f.Start(ct.Token);f.ops.Resolve();await UniTask.NextFrame();Assert.That(f.ops.loads,Is.EqualTo(1));
                ct.Cancel();f.ops.asset.Succeed(f.prefab);bool canceled=false;try{await op;}catch(OperationCanceledException){canceled=true;}
                Assert.That(canceled,Is.True);Assert.That(f.ops.assetReleases,Is.EqualTo(1));Assert.That(f.parent.transform.childCount,Is.Zero);Assert.That(LeaseProbeView.Awakes,Is.Zero);
            }
        });
        [UnityTest] public IEnumerator DestroyedOrActivatedParent_PreventsLateInstantiation() => UniTask.ToCoroutine(async()=>
        {
            for(int mode=0;mode<2;mode++)using(var f=new Fixture())
            {
                var op=f.Start();f.ops.Resolve();await UniTask.NextFrame();
                if(mode==0)Object.DestroyImmediate(f.parent);else f.parent.SetActive(true);
                f.ops.asset.Succeed(f.prefab);Exception failure=null;try{await op;}catch(Exception e){failure=e;}
                Assert.That(failure,Is.TypeOf<ArgumentException>());Assert.That(f.ops.assetReleases,Is.EqualTo(1));Assert.That(LeaseProbeView.Awakes,Is.Zero);
            }
        });
        [UnityTest] public IEnumerator InvalidPrefab_RollsBackCloneAndReleasesAfterDestruction() => UniTask.ToCoroutine(async()=>
        {
            using(var f=new Fixture(false))
            {
                var op=f.Start();f.ops.Resolve();await UniTask.NextFrame();f.ops.asset.Succeed(f.prefab);
                Exception failure=null;try{await op;}catch(Exception e){failure=e;}
                Assert.That(failure,Is.TypeOf<InvalidOperationException>());await Settle();Assert.That(f.ops.assetReleases,Is.EqualTo(1));Assert.That(f.parent.transform.childCount,Is.Zero);
            }
        });
        [UnityTest] public IEnumerator MissingAmbiguousAndFailedLocations_ReleaseQuery() => UniTask.ToCoroutine(async()=>
        {
            for(int count=0;count<3;count++)using(var f=new Fixture())
            {
                var op=f.Start();if(count==1)f.ops.locations.Fail();else f.ops.Resolve(count);
                Exception failure=null;try{await op;}catch(Exception e){failure=e;}
                Assert.That(failure,Is.Not.Null);Assert.That(f.ops.queryReleases,Is.EqualTo(1));Assert.That(f.ops.loads,Is.Zero);
            }
        });
        [UnityTest] public IEnumerator FailedAssetLoad_ReleasesBothHandles() => UniTask.ToCoroutine(async()=>
        {
            using(var f=new Fixture())
            {
                var op=f.Start();f.ops.Resolve();await UniTask.NextFrame();f.ops.asset.Fail();Exception failure=null;
                try{await op;}catch(Exception e){failure=e;}
                Assert.That(failure,Is.Not.Null);Assert.That(f.ops.queryReleases,Is.EqualTo(1));Assert.That(f.ops.assetReleases,Is.EqualTo(1));
            }
        });
        [UnityTest] public IEnumerator PreCancelUnknownProviderAndActiveParent_DoNotStartBackend() => UniTask.ToCoroutine(async()=>
        {
            using(var f=new Fixture())using(var ct=new CancellationTokenSource())
            {
                ct.Cancel();try{await f.Start(ct.Token);Assert.Fail();}catch(OperationCanceledException){}
                try{await f.loader.CreateAsync(UIViewAsset.External("Addressables","id"),f.parent.transform,default);Assert.Fail();}catch(ArgumentException){}
                f.parent.SetActive(true);try{await f.Start();Assert.Fail();}catch(ArgumentException){}
                Assert.That(f.ops.queries,Is.Zero);
            }
        });
        [UnityTest] public IEnumerator CanceledThenFailedLoad_DrainsAndPreservesOriginalFailure() => UniTask.ToCoroutine(async()=>
        {
            using(var f=new Fixture())using(var ct=new CancellationTokenSource())
            {
                var op=f.Start(ct.Token);f.ops.Resolve();await UniTask.NextFrame();ct.Cancel();
                Assert.That(f.ops.assetReleases,Is.Zero);f.ops.asset.Fail();Exception failure=null;try{await op;}catch(Exception e){failure=e;}
                Assert.That(failure,Is.Not.Null.And.Not.TypeOf<OperationCanceledException>());Assert.That(f.ops.assetReleases,Is.EqualTo(1));Assert.That(f.parent.transform.childCount,Is.Zero);
            }
        });
        [UnityTest] public IEnumerator SingleLabelLocation_IsRejectedBeforeAssetLoad() => UniTask.ToCoroutine(async()=>
        {
            using(var f=new Fixture())
            {
                var op=f.Start();f.ops.Resolve(primaryKey:"another-address");Exception failure=null;try{await op;}catch(Exception e){failure=e;}
                Assert.That(failure,Is.TypeOf<ArgumentException>());Assert.That(f.ops.loads,Is.Zero);Assert.That(f.ops.queryReleases,Is.EqualTo(1));
            }
        });
        [UnityTest] public IEnumerator ExactAddressWithBrackets_IsNotMistakenForSubAsset() => UniTask.ToCoroutine(async()=>
        {
            using(var f=new Fixture())
            {
                var op=f.Start(id:"ui/[menu]");f.ops.Resolve(primaryKey:"ui/[menu]");await UniTask.NextFrame();f.ops.asset.Succeed(f.prefab);
                var lease=await op;Assert.That(lease.View,Is.Not.Null);lease.Dispose();await Settle();Assert.That(f.ops.assetReleases,Is.EqualTo(1));
            }
        });
        [TearDown] public void Trace()
        {
            var dir=Environment.GetEnvironmentVariable("UIKIT_EVIDENCE_DIR");
            if(!string.IsNullOrEmpty(dir))File.WriteAllText(Path.Combine(dir,TestContext.CurrentContext.Test.Name+".trace.json"),JsonUtility.ToJson(new Report{frame=Time.frameCount,awakes=LeaseProbeView.Awakes,measurements=Measurements.ToArray()}));
        }
        [Serializable] sealed class Report {public int frame,awakes;public string[] measurements;}
    }
}
