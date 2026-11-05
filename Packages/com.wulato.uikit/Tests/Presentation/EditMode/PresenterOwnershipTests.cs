using System;
using NUnit.Framework;
using UnityEngine;
using Wulato.UIKit.Tests.Support;
namespace Wulato.UIKit.Presentation.Tests
{
    public class PresenterView:UIView{}
    public class ModelView:UIView<IDisposable>{}
    [Category("presentation")]
    public class PresenterOwnershipTests
    {
        sealed class Factory:IPresenterFactory
        {
            public int Creates,Binds,Disposes;public IDisposable OwnedModel;public bool FailBind,FailDispose,Skip;
            public IUIPresenter Create(UIViewBase view,UIArguments args,UISessionScope scope)
            {if(Skip)return null;Creates++;return new Presenter(this);}
            sealed class Presenter:IUIPresenter
            {
                readonly Factory owner;
                public Presenter(Factory owner){this.owner=owner;}
                public void Bind(){owner.Binds++;if(owner.FailBind)throw new InvalidOperationException("bind");}
                public void Dispose(){owner.Disposes++;owner.OwnedModel?.Dispose();if(owner.FailDispose)throw new InvalidOperationException("dispose");}
            }
        }
        UIFlowTestHarness Fixture(Factory factory)
        {
            var b=new UIRegistryBuilder();b.Add<PresenterView>(new UIViewTypeKey("presenter"),UIViewAsset.Resources("presenter"),closePolicy:UIClosePolicy.CacheOne);
            b.Add<ModelView,IDisposable>(new UIViewTypeKey("model"),UIViewAsset.Resources("model"));
            var f=new UIFlowTestHarness(b.Build());f.Observer=new UIPresenterObserver(factory);return f;
        }
        [Test] public void DuplicatePresentationRegistration_IsRejected()
        {
            var root=new GameObject("presenter-builder",typeof(RectTransform),typeof(UIRoot));var registry=ScriptableObject.CreateInstance<UIRegistry>();
            try{var factory=new Factory();var builder=new UIContextBuilder(root.GetComponent<UIRoot>(),registry).UsePresentation(factory);Assert.Throws<InvalidOperationException>(()=>builder.UsePresentation(factory));}
            finally{UnityEngine.Object.DestroyImmediate(root);UnityEngine.Object.DestroyImmediate(registry);}
        }
        sealed class Model:IDisposable{public int Disposes;public void Dispose(){Disposes++;}}
        [Test] public void ExternalModel_RemainsOwnedByCaller()
        {
            var external=new Model();var owned=new Model();var factory=new Factory{OwnedModel=owned};
            using(var f=Fixture(factory))
            {f.UI.OpenAsync<ModelView,IDisposable>(external).GetAwaiter().GetResult().CloseAsync().GetAwaiter().GetResult();Assert.That(external.Disposes,Is.Zero);Assert.That(owned.Disposes,Is.EqualTo(1));}
        }
        [Test] public void CacheReopen_CreatesFreshPresenterPerSession()
        {
            var factory=new Factory();using(var f=Fixture(factory))
            {
                f.UI.OpenAsync<PresenterView>().GetAwaiter().GetResult().CloseAsync().GetAwaiter().GetResult();
                f.UI.OpenAsync<PresenterView>().GetAwaiter().GetResult().CloseAsync().GetAwaiter().GetResult();
                Assert.That(factory.Creates,Is.EqualTo(2));Assert.That(factory.Binds,Is.EqualTo(2));Assert.That(factory.Disposes,Is.EqualTo(2));Assert.That(f.Creates,Is.EqualTo(1));
            }
        }
        [Test] public void PartialBindFailure_RollsBackOwnership()
        {
            var factory=new Factory{FailBind=true,FailDispose=true};using(var f=Fixture(factory))
            {
                var error=Assert.Throws<UIException>(()=>f.UI.OpenAsync<PresenterView>().GetAwaiter().GetResult());
                Assert.That(error.InnerException,Is.TypeOf<AggregateException>());Assert.That(factory.Disposes,Is.EqualTo(1));Assert.That(f.Releases,Is.EqualTo(1));
            }
        }
        [Test] public void DisposeFailure_DoesNotBlockClosedOrLeaseRelease()
        {
            var factory=new Factory{FailDispose=true};using(var f=Fixture(factory))
            {
                var session=f.UI.OpenAsync<PresenterView>().GetAwaiter().GetResult();var result=session.CloseAsync().GetAwaiter().GetResult();
                Assert.That(result.Errors.Count,Is.GreaterThan(0));Assert.That(session.State,Is.EqualTo(UIState.Closed));Assert.That(factory.Disposes,Is.EqualTo(1));
            }
        }
        [Test] public void UnmatchedView_DoesNotCreatePresenter()
        {
            var factory=new Factory{Skip=true};using(var f=Fixture(factory))
            {f.UI.OpenAsync<PresenterView>().GetAwaiter().GetResult().CloseAsync().GetAwaiter().GetResult();Assert.That(factory.Creates,Is.Zero);}
        }
    }
}
