using System;
using System.Linq;
using UnityEngine;
using UnityEngine.EventSystems;
namespace Wulato.UIKit.Tests.Input
{
    public class InputRuntimeView : UIView { }
    public sealed class RuntimeInputIntents : IDisposable
    {
        public readonly EventSystem Events;
        public readonly InputEventSpy[] Owners;
        public readonly UIContextId[] Contexts={new UIContextId(Guid.NewGuid()),new UIContextId(Guid.NewGuid())};
        public readonly UISessionId[] Sessions={new UISessionId(1),new UISessionId(2)};
        public IUIInputArbiter Arbiter;
        public IDisposable Domain;
        IDisposable locked;
        public int OwnerId;
        public RuntimeInputIntents(EventSystem events,InputEventSpy[] owners) {Events=events;Owners=owners;}
        public static Type Require(string name)
        {
            var type=AppDomain.CurrentDomain.GetAssemblies().Select(a=>a.GetType(name)).FirstOrDefault(t=>t!=null);
            NUnit.Framework.Assert.That(type, NUnit.Framework.Is.Not.Null,"Missing production type: "+name);return type;
        }
        public static BaseInputModule AddDriver(EventSystem events,string name) => (BaseInputModule)events.gameObject.AddComponent(Require(name));
        public void Initialize(BaseInputModule module)
        {
            Domain=(IDisposable)Activator.CreateInstance(Require("Wulato.UIKit.UIInputDomain"),Events,module);
            Arbiter=(IUIInputArbiter)Activator.CreateInstance(Require("Wulato.UIKit.UIInputArbiter"),Domain);
            for(int i=0;i<2;i++)
            {
                Owners[i].gameObject.AddComponent<InputRuntimeView>();
                Arbiter.Register(Contexts[i],new UIInputRegistration(i,null,null));
            }
            Arbiter.Commit(Arbiter.Prepare(new UIInputCandidate(Contexts[1],Sessions[1],Owners[1].GetComponent<InputRuntimeView>(),new UIInputPolicy(false,false))));
            TransferOwner(0);
        }
        public void SetLocked(bool value) {if(value&&locked==null)locked=Arbiter.AcquireLock(Contexts[OwnerId]);else if(!value){locked?.Dispose();locked=null;}}
        public void TransferOwner(int id)
        {
            if(Arbiter==null)return;
            if(id!=OwnerId)Arbiter.Revoke(Contexts[OwnerId],Sessions[OwnerId]);
            OwnerId=id;
            var view=Owners[id].GetComponent<InputRuntimeView>();
            Arbiter.Commit(Arbiter.Prepare(new UIInputCandidate(Contexts[id],Sessions[id],view,new UIInputPolicy(true,true,true))));
        }
        public void Dispose() {locked?.Dispose();(Arbiter as IDisposable)?.Dispose();Domain?.Dispose();}
    }
}
