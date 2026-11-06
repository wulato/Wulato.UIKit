using System;
using R3;

namespace Wulato.UIKit.Samples.R3Binding
{
    /// <summary>Owned by the business caller; ending a UI session does not dispose this model.</summary>
    public sealed class CounterModel : IDisposable
    {
        public ReactiveProperty<int> Count { get; }
        public CounterModel(int initial = 0) => Count = new ReactiveProperty<int>(initial);
        public void Increment() => Count.Value++;
        public void Dispose() => Count.Dispose();
    }
}
