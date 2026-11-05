using System;
using System.Linq;
using System.Threading;
using Cysharp.Threading.Tasks;
using NUnit.Framework;
using Wulato.UIKit.Tests.Support;
namespace Wulato.UIKit.Tests
{
    public sealed class CacheA : ProbeView { }
    public sealed class CacheB : ProbeView { }
    public sealed class CacheC : ProbeView { }
    [Category("cleanup")]
    public class CachePolicyTests : SessionTestBase
    {
        static SessionFixture Fixture(int capacity) => new SessionFixture(new UIContextSettings { CacheCapacity = capacity }, b =>
        {
            b.Add<CacheA>(new UIViewTypeKey("a"), UIViewAsset.Resources("a"), closePolicy: UIClosePolicy.CacheOne);
            b.Add<CacheB>(new UIViewTypeKey("b"), UIViewAsset.Resources("b"), closePolicy: UIClosePolicy.CacheOne);
            b.Add<CacheC>(new UIViewTypeKey("c"), UIViewAsset.Resources("c"), closePolicy: UIClosePolicy.CacheOne);
        });
        static IUISession<T> Open<T>(SessionFixture f) where T : ProbeView
        {
            f.Loader.ViewType = typeof(T); return f.UI.OpenAsync<T>().GetAwaiter().GetResult();
        }
        [Test] public void TerminatedObserverFailure_EvictsTentativeCacheBeforeClosed()
        {
            var observer = new ProbeObserver { OnEnd = () => throw new Exception("terminated") };
            using (var f = new SessionFixture(new UIContextSettings { CacheCapacity = 1 },
                b => b.Add<ProbeView>(new UIViewTypeKey("p"), UIViewAsset.Resources("p"), closePolicy: UIClosePolicy.CacheOne), observer))
            {
                var session = f.UI.OpenAsync<ProbeView>().GetAwaiter().GetResult();
                Assert.That(session.CloseAsync().GetAwaiter().GetResult().Errors.Count, Is.GreaterThan(0));
                Assert.That(f.UI.GetDiagnostics().CacheLru, Is.Empty); Assert.That(f.Loader.ReleaseCount, Is.EqualTo(1));
            }
        }
        [Test] public void CapacityZero_Releases()
        {
            using (var f = Fixture(0)) { Open<CacheA>(f).CloseAsync().GetAwaiter().GetResult(); Assert.That(f.Loader.ReleaseCount, Is.EqualTo(1)); Assert.That(f.UI.GetDiagnostics().CacheLru, Is.Empty); }
        }
        [Test] public void CacheOne_LruEvictsOldest()
        {
            using (var f = Fixture(2))
            {
                Open<CacheA>(f).CloseAsync().GetAwaiter().GetResult(); Open<CacheB>(f).CloseAsync().GetAwaiter().GetResult();
                Open<CacheA>(f).CloseAsync().GetAwaiter().GetResult(); Open<CacheC>(f).CloseAsync().GetAwaiter().GetResult();
                Assert.That(f.Loader.CreateCount, Is.EqualTo(3)); Assert.That(f.Loader.ReleaseCount, Is.EqualTo(1));
                Assert.That(f.UI.GetDiagnostics().CacheLru.Select(k => k.Value), Is.EqualTo(new[] { "a", "c" }));
                ProbeView.Measurements.Add("lru=" + string.Join(",", f.UI.GetDiagnostics().CacheLru.Select(k => k.Value)) + ":creates=" + f.Loader.CreateCount + ":releases=" + f.Loader.ReleaseCount);
                Open<CacheB>(f); Assert.That(f.Loader.CreateCount, Is.EqualTo(4));
            }
        }
        [Test] public void CacheReuse_NewSessionAndCanceledOldScope_CannotCloseNewSession()
        {
            using (var f = Fixture(1))
            {
                var a = Open<CacheA>(f); var oldScope = ProbeView.Scope; var view = f.Loader.LastView;
                var result = a.CloseAsync().GetAwaiter().GetResult(); var b = Open<CacheA>(f);
                Assert.That(f.Loader.LastView, Is.SameAs(view)); Assert.That(b.Id, Is.Not.EqualTo(a.Id));
                Assert.That(a.CloseAsync().GetAwaiter().GetResult(), Is.SameAs(result)); Assert.That(b.State, Is.EqualTo(UIState.Open));
                Assert.That(oldScope.IsCurrent, Is.False); int releases = 0; oldScope.Track(new ActionResource(() => releases++));
                Assert.That(releases, Is.EqualTo(1)); Assert.That(ProbeView.Events.Count(e => e == "create"), Is.EqualTo(1));
                Assert.That(ProbeView.Events.Count(e => e == "open"), Is.EqualTo(2)); Assert.That(f.Loader.CreateCount, Is.EqualTo(1));
                ProbeView.Measurements.Add("reuse:old=" + a.Id + ":new=" + b.Id + ":old-current=" + oldScope.IsCurrent + ":creates=" + f.Loader.CreateCount);
            }
        }
        [Test] public void UnhealthyClose_NeverEntersCache()
        {
            using (var f = Fixture(1))
            {
                var session = Open<CacheA>(f); ProbeView.Close = (r, ct) => UniTask.FromException(new Exception("close"));
                Assert.That(session.CloseAsync().GetAwaiter().GetResult().Errors.Count, Is.GreaterThan(0));
                Assert.That(f.Loader.ReleaseCount, Is.EqualTo(1)); Assert.That(f.UI.GetDiagnostics().CacheLru, Is.Empty);
            }
        }
        [Test] public void ClearCache_DisposeErrorsDoNotStrandOtherLeases()
        {
            using (var f = Fixture(2))
            {
                Open<CacheA>(f).CloseAsync().GetAwaiter().GetResult(); Open<CacheB>(f).CloseAsync().GetAwaiter().GetResult();
                ProbeView.Disposing = () => throw new Exception("dispose");
                Assert.Throws<UIException>(() => f.UI.ClearCacheAsync().GetAwaiter().GetResult());
                Assert.That(f.Loader.ReleaseCount, Is.EqualTo(2)); Assert.That(f.UI.GetDiagnostics().CacheLru, Is.Empty);
                ProbeView.Disposing = null;
            }
        }
    }
}
