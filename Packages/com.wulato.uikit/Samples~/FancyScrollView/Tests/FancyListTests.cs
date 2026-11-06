using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading;
using Cysharp.Threading.Tasks;
using NUnit.Framework;
using FancyScrollView;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.TestTools;
using UnityEngine.UI;
namespace Wulato.UIKit.Samples.FancyList.Tests
{
    public sealed class CatalogModal:UIView { }
    public sealed class CatalogRegistry:UIRegistryProvider
    {
        public GameObject Catalog,Modal;
        public override void Populate(UIRegistryBuilder b)
        {
            b.Add<CatalogView,CatalogArgs>(new UIViewTypeKey("catalog"),UIViewAsset.Direct(Catalog),UILayer.Modal,closePolicy:UIClosePolicy.CacheOne);
            b.Add<CatalogModal>(new UIViewTypeKey("modal"),UIViewAsset.Direct(Modal),UILayer.Modal);
        }
    }
    [Category("fancy_list")]
    public sealed class FancyListTests
    {
        sealed class Fixture:IUIErrorSink
        {
            internal readonly IUIContext UI;
            internal readonly List<UIException> Errors=new List<UIException>();
            readonly GameObject sources,root,events;readonly UIRegistry registry;readonly CatalogRegistry provider;readonly EventSystem previous;
            internal Fixture()
            {
                previous=EventSystem.current;sources=new GameObject("Catalog sources");sources.SetActive(false);
                provider=ScriptableObject.CreateInstance<CatalogRegistry>();provider.Catalog=Resources.Load<GameObject>("UIKitFancyCatalogDemo");Assert.That(provider.Catalog,Is.Not.Null);provider.Modal=Prefab<CatalogModal>();
                registry=ScriptableObject.CreateInstance<UIRegistry>();typeof(UIRegistry).GetField("providers",BindingFlags.Instance|BindingFlags.NonPublic).SetValue(registry,new List<UIRegistryProvider>{provider});
                events=new GameObject("Catalog events",typeof(EventSystem));
#if ENABLE_INPUT_SYSTEM && !ENABLE_LEGACY_INPUT_MANAGER
                events.AddComponent(Type.GetType("Wulato.UIKit.InputSystem.InputSystemUIInputDriver, Wulato.UIKit.InputSystem",true));
#else
                events.AddComponent<LegacyUIInputDriver>();
#endif
                EventSystem.current=events.GetComponent<EventSystem>();root=new GameObject("Catalog root",typeof(RectTransform),typeof(UIRoot));root.GetComponent<Canvas>().renderMode=RenderMode.ScreenSpaceOverlay;
                UI=new UIContextBuilder(root.GetComponent<UIRoot>(),registry).WithErrorSink(this).Build();
            }
            GameObject Prefab<T>() where T:UIViewBase
            {var go=new GameObject(typeof(T).Name,typeof(RectTransform));go.transform.SetParent(sources.transform,false);go.AddComponent<T>();return go;}
            internal CatalogView View=>root.GetComponentInChildren<CatalogView>(true);
            internal CatalogList List=>root.GetComponentInChildren<CatalogList>(true);
            internal CatalogCell[] Cells=>List.GetComponentsInChildren<CatalogCell>(true).Where(c=>c.name!="Cell template").ToArray();
            public void Report(UIException error)=>Errors.Add(error);
            internal async UniTask DisposeAsync()
            {
                await UI.ShutdownAsync();Assert.That(UI.GetDiagnostics().ActiveLeaseCount,Is.Zero);Assert.That(UI.GetDiagnostics().InputLockCount,Is.Zero);
                UnityEngine.Object.DestroyImmediate(root);UnityEngine.Object.DestroyImmediate(events);UnityEngine.Object.DestroyImmediate(sources);UnityEngine.Object.DestroyImmediate(registry);UnityEngine.Object.DestroyImmediate(provider);
                if(previous)EventSystem.current=previous;
            }
        }
        sealed class Request
        {
            internal int Id;internal CancellationToken Token;internal UniTaskCompletionSource<string> Completion=new UniTaskCompletionSource<string>();
        }
        readonly List<Request> requests=new List<Request>();
        int poolSmall,poolLarge;double jumpMilliseconds;long memoryDelta;
        CatalogArgs Pending(int count=10000)=>new CatalogArgs(Enumerable.Range(0,count).ToArray(),(id,ct)=>
        {var r=new Request{Id=id,Token=ct};requests.Add(r);return r.Completion.Task;});
        static CatalogArgs Immediate(int count=10000,string prefix="Item ")=>new CatalogArgs(Enumerable.Range(0,count).ToArray(),(id,ct)=>UniTask.FromResult(prefix+id));
        [SetUp] public void Reset(){requests.Clear();poolSmall=poolLarge=0;jumpMilliseconds=0;memoryDelta=0;}
        [Serializable] sealed class Trace {public string caseName,status,sdkAssembly,assemblyVersion;public int poolSmall,poolLarge;public double jumpMilliseconds;public long memoryDelta;}
        [TearDown] public void SaveTrace()
        {
            var dir=Environment.GetEnvironmentVariable("UIKIT_EVIDENCE_DIR");if(string.IsNullOrEmpty(dir))return;
            var sdk=typeof(Scroller).Assembly.GetName();
            File.WriteAllText(Path.Combine(dir,TestContext.CurrentContext.Test.Name+".trace.json"),JsonUtility.ToJson(new Trace{caseName=TestContext.CurrentContext.Test.FullName,status=TestContext.CurrentContext.Result.Outcome.Status.ToString(),sdkAssembly=sdk.Name,assemblyVersion=sdk.Version.ToString(),poolSmall=poolSmall,poolLarge=poolLarge,jumpMilliseconds=jumpMilliseconds,memoryDelta=memoryDelta}));
        }
        static UniTask Frames()=>UniTask.DelayFrame(2);
        [Test] public void CoreDoesNotReferenceTheSdk()
        {Assert.That(typeof(Scroller).Assembly.GetName().Name,Is.EqualTo("FancyScrollView"));Assert.That(typeof(IUIContext).Assembly.GetReferencedAssemblies().Select(a=>a.Name),Does.Not.Contain("FancyScrollView"));}
        [UnityTest] public IEnumerator PoolDependsOnViewportInsteadOfItemCount()=>UniTask.ToCoroutine(async()=>
        {
            var f=new Fixture();try
            {
                var session=await f.UI.OpenAsync<CatalogView,CatalogArgs>(Immediate());await Frames();poolSmall=f.Cells.Length;Assert.That(poolSmall,Is.InRange(6,8));
                var watch=System.Diagnostics.Stopwatch.StartNew();var before=GC.GetTotalMemory(false);
                for(int i=0;i<300;i++)f.List.Scroller.Position=i*23;
                watch.Stop();jumpMilliseconds=watch.Elapsed.TotalMilliseconds;memoryDelta=GC.GetTotalMemory(false)-before;
                Assert.That(f.Cells.Length,Is.EqualTo(poolSmall));await session.CloseAsync();
                await f.UI.OpenAsync<CatalogView,CatalogArgs>(Immediate(100000));await Frames();Assert.That(f.Cells.Length,Is.EqualTo(poolSmall));
                ((RectTransform)f.List.transform).sizeDelta=new Vector2(360,768);f.List.ResizeViewport();await Frames();poolLarge=f.Cells.Length;
                Assert.That(poolLarge,Is.GreaterThan(poolSmall));Assert.That(poolLarge,Is.InRange(12,14));Assert.That(f.Errors,Is.Empty);
            }finally{await f.DisposeAsync();}
        });
        [UnityTest] public IEnumerator RecycledCell_CancelsAndRejectsAnIgnoringLoader()=>UniTask.ToCoroutine(async()=>
        {
            var f=new Fixture();try
            {
                await f.UI.OpenAsync<CatalogView,CatalogArgs>(Pending());await Frames();var cell=f.Cells.First(c=>c.IsVisible);var old=requests.Last(r=>r.Id==cell.ItemId);
                f.List.Scroller.Position=100;Assert.That(old.Token.IsCancellationRequested,Is.True);Assert.That(cell.ItemId,Is.Not.EqualTo(old.Id));
                old.Completion.TrySetResult("STALE");await Frames();Assert.That(cell.Caption,Is.Not.EqualTo("STALE"));
                var fresh=requests.Last(r=>r.Id==cell.ItemId);fresh.Completion.TrySetResult("CURRENT");await Frames();Assert.That(cell.Caption,Is.EqualTo("CURRENT"));
            }finally{await f.DisposeAsync();}
        });
        [UnityTest] public IEnumerator SameItemId_RebindStillRejectsAnOlderRequest()=>UniTask.ToCoroutine(async()=>
        {
            var f=new Fixture();try
            {
                await f.UI.OpenAsync<CatalogView,CatalogArgs>(Pending());await Frames();var cell=f.Cells.First(c=>c.IsVisible);int id=cell.ItemId;var old=requests.Last(r=>r.Id==id);
                f.List.Scroller.Position=100;f.List.Scroller.Position=0;Assert.That(cell.ItemId,Is.EqualTo(id));var fresh=requests.Last(r=>r.Id==id);Assert.That(fresh,Is.Not.SameAs(old));
                fresh.Completion.TrySetResult("NEW");await Frames();old.Completion.TrySetResult("OLD");await Frames();Assert.That(cell.Caption,Is.EqualTo("NEW"));
            }finally{await f.DisposeAsync();}
        });
        [UnityTest] public IEnumerator SessionClose_CancelsAndCachedReopenRejectsOldResults()=>UniTask.ToCoroutine(async()=>
        {
            var f=new Fixture();try
            {
                var session=await f.UI.OpenAsync<CatalogView,CatalogArgs>(Pending());await Frames();var cached=f.View;var old=requests.ToArray();
                await session.CloseAsync();Assert.That(old.All(r=>r.Token.IsCancellationRequested),Is.True);
                await f.UI.OpenAsync<CatalogView,CatalogArgs>(Immediate(prefix:"NEW "));Assert.That(f.View,Is.SameAs(cached));foreach(var r in old)r.Completion.TrySetResult("OLD");await Frames();
                Assert.That(f.Cells.Where(c=>c.IsVisible).All(c=>c.Caption.StartsWith("NEW ")),Is.True);
            }finally{await f.DisposeAsync();}
        });
        [UnityTest] public IEnumerator Shutdown_CancelsBeforeLateCompletion()=>UniTask.ToCoroutine(async()=>
        {
            var f=new Fixture();try
            {
                await f.UI.OpenAsync<CatalogView,CatalogArgs>(Pending());await Frames();var pending=requests.ToArray();await f.UI.ShutdownAsync();
                Assert.That(pending.All(r=>r.Token.IsCancellationRequested),Is.True);foreach(var r in pending)r.Completion.TrySetResult("LATE");await Frames();Assert.That(f.Errors,Is.Empty);
            }finally{await f.DisposeAsync();}
        });
        [UnityTest] public IEnumerator NativeScroller_UsesDragAndPositionDrivenPresentation()=>UniTask.ToCoroutine(async()=>
        {
            var f=new Fixture();try
            {
                await f.UI.OpenAsync<CatalogView,CatalogArgs>(Immediate());await Frames();var scroller=f.List.Scroller;var point=RectTransformUtility.WorldToScreenPoint(null,scroller.transform.position);
                var data=new PointerEventData(EventSystem.current){button=PointerEventData.InputButton.Left,position=point};
                ((IPointerDownHandler)scroller).OnPointerDown(data);((IBeginDragHandler)scroller).OnBeginDrag(data);data.position+=Vector2.up*100;((IDragHandler)scroller).OnDrag(data);((IEndDragHandler)scroller).OnEndDrag(data);((IPointerUpHandler)scroller).OnPointerUp(data);
                Assert.That(scroller.Position,Is.GreaterThan(0));Assert.That(f.Cells.Where(c=>c.IsVisible).Any(c=>c.transform.localScale.x<1),Is.True);
            }finally{await f.DisposeAsync();}
        });
    }
}
