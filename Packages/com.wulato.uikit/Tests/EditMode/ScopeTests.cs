using System;
using System.Linq;
using System.Threading;
using Cysharp.Threading.Tasks;
using NUnit.Framework;
using Wulato.UIKit.Tests.Support;
namespace Wulato.UIKit.Tests
{
    [Category("sessions")] public class ScopeTests : SessionTestBase
    {
        [Test] public void DisposeThrows_OtherResourcesStillDispose()
        {
            var scope=new UIInstanceScope();int disposed=0;
            scope.Track(new ActionResource(()=>disposed++));scope.Track(new ActionResource(()=>throw new Exception("dispose")));
            Assert.Throws<AggregateException>(()=>scope.Dispose());Assert.That(disposed,Is.EqualTo(1));Assert.That(scope.Token.IsCancellationRequested,Is.True);
            scope.Dispose();scope.Track(new ActionResource(()=>disposed++));Assert.That(disposed,Is.EqualTo(2));
        }
        [Test] public void EndedSession_IsNotCurrentAndImmediatelyDisposesTrackedResources()
        {
            using(var f=new SessionFixture()) { var s=f.UI.OpenAsync<ProbeView>().GetAwaiter().GetResult();var scope=ProbeView.Scope;s.CloseAsync().GetAwaiter().GetResult();int n=0;scope.Track(new ActionResource(()=>n++));Assert.That(n,Is.EqualTo(1));Assert.That(scope.IsCurrent,Is.False);Assert.That(scope.Token.IsCancellationRequested,Is.True); }
        }
    }
}
