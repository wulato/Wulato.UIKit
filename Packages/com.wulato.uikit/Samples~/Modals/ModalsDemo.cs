using System;
using Cysharp.Threading.Tasks;
using UnityEngine;
namespace Wulato.UIKit.Samples.Modals
{
    public sealed class ModalsDemo : MonoBehaviour
    {
        [SerializeField] UIRoot root;
        [SerializeField] UIRegistry registry;
        IUIContext ui;
        public IUIContext Context=>ui;
        [ContextMenu("Open UI")] void OpenFromInspector()=>OpenAsync().Forget(Debug.LogException);
        async void Start()
        {
            try { ui=new UIContextBuilder(root,registry).Build();await OpenAsync(); }
            catch(Exception error){ Debug.LogException(error); }
        }
        public UniTask OpenAsync()=>OpenCore();
        async UniTask OpenCore()
        {
            if(ui==null)return;
            await ui.OpenAsync<StackedModalView>();
        }
        // Business confirmation is external to UIKit. Disable your command while this awaits.
        bool confirming;
        public async UniTask ConfirmAndOpenAsync(Func<UniTask<bool>> confirm)
        {
            if(confirming)return;
            confirming=true;
            try{if(await confirm())await OpenAsync();}finally{confirming=false;}
        }
    }
}
