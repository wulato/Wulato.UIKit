namespace Codegen.B {
internal partial class ViewB {
partial void CheckBindings() { if(label == null) throw new global::System.InvalidOperationException("Binding unavailable before OnCreate"); }
public bool BindingReady() => label != null;
}
}
