namespace Codegen.A {
public partial class ViewA {
partial void CheckBindings() { if(label == null) throw new global::System.InvalidOperationException("Binding unavailable before OnCreate"); }
public bool BindingReady() => label != null;
}
}
