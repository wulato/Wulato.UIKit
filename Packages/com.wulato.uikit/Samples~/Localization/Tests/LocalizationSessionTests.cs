using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Threading;
using Cysharp.Threading.Tasks;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.EventSystems;
using UnityEngine.Localization;
using UnityEngine.Localization.Settings;
using UnityEngine.Localization.Tables;
using UnityEngine.ResourceManagement.AsyncOperations;
using UnityEngine.TestTools;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace Wulato.UIKit.Samples.Localization.Tests
{
    // Only replace the asset source/clock. Native LocalizedString, table entry lookup,
    // formatting, locale events and Addressables reference counting remain in use.
    public sealed class PendingTable : AsyncOperationBase<StringTable>
    {
        protected override void Execute() { }
        public void Finish(StringTable table) => Complete(table, true, (string)null);
    }
    public sealed class TestStringDatabase : LocalizedStringDatabase
    {
        public readonly List<PendingTable> Pending = new List<PendingTable>();
        public readonly List<AsyncOperationHandle<StringTable>> Handles = new List<AsyncOperationHandle<StringTable>>();
        public override AsyncOperationHandle<StringTable> GetTableAsync(TableReference table, Locale locale = null)
        {
            var op = new PendingTable(); Pending.Add(op);
            var handle = Addressables.ResourceManager.StartOperation(op, default);
            Handles.Add(handle); return handle;
        }
        public void ReleaseSources() { foreach (var h in Handles) if (h.IsValid()) Addressables.Release(h); }
    }
    public sealed class TestSettings : LocalizationSettings
    {
        AsyncOperationHandle<LocalizationSettings> initialized;
        public override AsyncOperationHandle<LocalizationSettings> GetInitializationOperation()
        {
            if (!initialized.IsValid()) initialized = Addressables.ResourceManager.CreateCompletedOperation<LocalizationSettings>(this, null);
            return initialized;
        }
        public void ReleaseSource() { if (initialized.IsValid()) Addressables.Release(initialized); }
    }
    public sealed class FailingLocalizationView : LocalizationView
    {
        protected override async UniTask OnOpenAsync(LocalizedString value, UISessionScope scope, CancellationToken ct)
        { await base.OnOpenAsync(value, scope, ct); throw new InvalidOperationException("localization-open-failure"); }
    }
    public sealed class LocalizationRegistry : UIRegistryProvider
    {
        public GameObject View, Failing;
        public override void Populate(UIRegistryBuilder b)
        {
            b.Add<LocalizationView,LocalizedString>(new UIViewTypeKey("localized"),UIViewAsset.Direct(View),closePolicy:UIClosePolicy.CacheOne);
            b.Add<FailingLocalizationView,LocalizedString>(new UIViewTypeKey("failing"),UIViewAsset.Direct(Failing));
        }
    }
    [Category("localization")]
    public sealed class LocalizationSessionTests
    {
        sealed class Fixture : IUIErrorSink
        {
            internal readonly TestStringDatabase Database;
            internal readonly IUIContext UI;
            internal readonly Locale English, French;
            internal readonly List<UIException> Errors=new List<UIException>();
            readonly LocalizationSettings previousSettings;
            readonly TestSettings settings;
            readonly EventSystem previousEvents;
            readonly GameObject root, sources, events;
            readonly UIRegistry registry;
            readonly LocalizationRegistry provider;
            readonly List<Object> assets = new List<Object>();
            internal Fixture()
            {
                previousSettings = LocalizationSettings.Instance;
                settings = ScriptableObject.CreateInstance<TestSettings>();
                LocalizationSettings.Instance = settings;
                Database = new TestStringDatabase();
                LocalizationSettings.StringDatabase = Database;
                English = Locale.CreateLocale("en"); French = Locale.CreateLocale("fr");
                LocalizationSettings.SelectedLocale = English;
                previousEvents = EventSystem.current;
                sources = new GameObject("localization sources"); sources.SetActive(false);
                provider = ScriptableObject.CreateInstance<LocalizationRegistry>();
                provider.View = Prefab<LocalizationView>(); provider.Failing = Prefab<FailingLocalizationView>();
                registry = ScriptableObject.CreateInstance<UIRegistry>();
                typeof(UIRegistry).GetField("providers",BindingFlags.Instance|BindingFlags.NonPublic).SetValue(registry,new List<UIRegistryProvider>{provider});
                events = new GameObject("events",typeof(EventSystem));
#if ENABLE_INPUT_SYSTEM && !ENABLE_LEGACY_INPUT_MANAGER
                events.AddComponent(Type.GetType("Wulato.UIKit.InputSystem.InputSystemUIInputDriver, Wulato.UIKit.InputSystem",true));
#else
                events.AddComponent<LegacyUIInputDriver>();
#endif
                EventSystem.current = events.GetComponent<EventSystem>();
                root = new GameObject("root",typeof(RectTransform),typeof(UIRoot));
                root.GetComponent<Canvas>().renderMode=RenderMode.ScreenSpaceOverlay;
                UI = new UIContextBuilder(root.GetComponent<UIRoot>(),registry).WithErrorSink(this).Build();
            }
            GameObject Prefab<T>() where T : UIViewBase
            { var go=new GameObject(typeof(T).Name,typeof(RectTransform));go.transform.SetParent(sources.transform,false);go.AddComponent<T>();return go; }
            internal LocalizationView View => root.GetComponentInChildren<LocalizationView>(true);
            internal Text Label => View.GetComponentInChildren<Text>(true);
            internal void Finish(int index, string value, Locale locale)
            {
                var shared=ScriptableObject.CreateInstance<SharedTableData>();shared.TableCollectionName="Sample";
                var table=ScriptableObject.CreateInstance<StringTable>();table.SharedData=shared;table.LocaleIdentifier=locale.Identifier;
                table.AddEntry("greeting",value);assets.Add(table);assets.Add(shared);
                Database.Pending[index].Finish(table);
            }
            public void Report(UIException error) => Errors.Add(error);
            internal async UniTask DisposeAsync()
            {
                await UI.ShutdownAsync();
                Assert.That(UI.GetDiagnostics().ActiveLeaseCount,Is.Zero);
                Assert.That(UI.GetDiagnostics().InputLockCount,Is.Zero);
                foreach(var error in Errors)
                { Assert.That(error.Code,Is.EqualTo(UIErrorCode.LifecycleFailed));Assert.That(error.ToString(),Does.Contain("localization-open-failure")); }
                for(int i=0;i<Database.Pending.Count;i++) if(!Database.Handles[i].IsDone) Finish(i,"cleanup",English);
                await UniTask.NextFrame(); Database.ReleaseSources();
                LocalizationSettings.Instance=previousSettings;
                settings.ReleaseSource();((IDisposable)settings).Dispose();
                Object.DestroyImmediate(settings);
                Object.DestroyImmediate(English);Object.DestroyImmediate(French);
                foreach(var asset in assets)Object.DestroyImmediate(asset);
                Object.DestroyImmediate(root);Object.DestroyImmediate(sources);Object.DestroyImmediate(events);
                Object.DestroyImmediate(registry);Object.DestroyImmediate(provider);
                if(previousEvents)EventSystem.current=previousEvents;
            }
        }
        static LocalizedString Reference() => new LocalizedString("Sample","greeting");
        static UniTask Wait(Func<bool> condition) => UniTask.WaitUntil(condition).Timeout(TimeSpan.FromSeconds(3));
        [TearDown] public void Trace()
        {
            var dir=Environment.GetEnvironmentVariable("UIKIT_EVIDENCE_DIR");if(string.IsNullOrEmpty(dir))return;
            File.WriteAllText(Path.Combine(dir,TestContext.CurrentContext.Test.Name+".trace.json"),
                "{\"sdk\":\"Unity.Localization 1.4.5\",\"status\":\""+TestContext.CurrentContext.Result.Outcome.Status+"\"}");
        }
        [UnityTest] public IEnumerator CachedReopen_DetachesEachOpeningAndKeepsReferenceUsable() => UniTask.ToCoroutine(async()=>
        {
            var f=new Fixture();var reference=Reference();try
            {
                LocalizationView cached=null;
                for(int i=0;i<3;i++)
                {
                    var s=await f.UI.OpenAsync<LocalizationView,LocalizedString>(reference);
                    if(cached==null)cached=f.View;else Assert.That(f.View,Is.SameAs(cached));
                    Assert.That(reference.HasChangeHandler,Is.True);f.Finish(i,"Hello"+i,f.English);
                    await Wait(()=>f.Label.text=="Hello"+i);await s.CloseAsync();
                    Assert.That(reference.HasChangeHandler,Is.False);Assert.That(reference.CurrentLoadingOperationHandle.IsValid(),Is.False);
                }
            }finally{await f.DisposeAsync();}
        });
        [UnityTest] public IEnumerator LateOldSession_CannotOverwriteReopenedLabel() => UniTask.ToCoroutine(async()=>
        {
            var f=new Fixture();var old=Reference();var next=Reference();try
            {
                var s=await f.UI.OpenAsync<LocalizationView,LocalizedString>(old);var cached=f.View;
                await s.CloseAsync();Assert.That(old.HasChangeHandler,Is.False);
                await f.UI.OpenAsync<LocalizationView,LocalizedString>(next);Assert.That(f.View,Is.SameAs(cached));
                f.Finish(1,"Current",f.English);await Wait(()=>f.Label.text=="Current");
                f.Finish(0,"Stale",f.English);await UniTask.DelayFrame(3);Assert.That(f.Label.text,Is.EqualTo("Current"));
                await f.UI.ShutdownAsync();Assert.That(next.HasChangeHandler,Is.False);
            }finally{await f.DisposeAsync();}
        });
        [UnityTest] public IEnumerator LocaleSwitch_LatePreviousLanguageIsDiscardedByNativeSdk() => UniTask.ToCoroutine(async()=>
        {
            var f=new Fixture();var reference=Reference();try
            {
                await f.UI.OpenAsync<LocalizationView,LocalizedString>(reference);
                LocalizationSettings.SelectedLocale=f.French;await Wait(()=>f.Database.Pending.Count==2);
                f.Finish(1,"Bonjour",f.French);await Wait(()=>f.Label.text=="Bonjour");
                f.Finish(0,"Hello",f.English);await UniTask.DelayFrame(3);Assert.That(f.Label.text,Is.EqualTo("Bonjour"));
            }finally{await f.DisposeAsync();}
        });
        [UnityTest] public IEnumerator OpenRollback_AndShutdownDetachPendingSubscriptions() => UniTask.ToCoroutine(async()=>
        {
            var f=new Fixture();var failed=Reference();var active=Reference();try
            {
                try { await f.UI.OpenAsync<FailingLocalizationView,LocalizedString>(failed);Assert.Fail("Expected open failure"); }
                catch(UIException) { }
                Assert.That(failed.HasChangeHandler,Is.False);
                await f.UI.OpenAsync<LocalizationView,LocalizedString>(active);var label=f.Label;
                await f.UI.ShutdownAsync();Assert.That(active.HasChangeHandler,Is.False);
                f.Finish(0,"Failed",f.English);f.Finish(1,"Stopped",f.English);await UniTask.DelayFrame(3);
                Assert.That(!label||label.text=="",Is.True);
            }finally{await f.DisposeAsync();}
        });
    }
}
