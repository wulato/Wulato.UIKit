# R3 Session Binding

先阅读 [安装与生命周期约定](../../Documentation~/Integrations/R3.md)（导入 Assets 后请从 UIKit 包文档打开此链接）。安装 R3/R3.Unity 1.3.1，导入本示例，再增加 `WULATO_UIKIT_R3`。

1. 建立带 RectTransform / CounterView 的 Prefab。可以绑定已有 Text / 两个 Button 字段，也可留空使用示例自动生成的计数、Increment、Close 控件。
2. 在现有 Registry Provider 的 `Populate` 中注册，放在 Modal 层：

```csharp
builder.Add<CounterView, CounterModel>(
    new UIViewTypeKey("counter"), UIViewAsset.Direct(counterPrefab),
    UILayer.Modal, closePolicy: UIClosePolicy.CacheOne);
```

3. 使用现有 `UIContextHost` / Builder 完成 Context 初始化；业务持有 Model：

```csharp
using (var model = new CounterModel())
{
    var session = await ui.OpenAsync<CounterView, CounterModel>(model);
    await session.Closed;
    // UI session 已解绑；Model 在这里仍可供业务使用。
    model.Increment();
} // 业务调用方最终 Dispose Model。
```

`CounterView` 展示三个原生 R3 订阅如何通过 `scope.Track` 自动结束；不引入 Presenter、ViewModel 基类或绑定引擎。CacheOne 每次重开都创建新的 Session，订阅不会累积。SDK 只为示例可选依赖，不随 UIKit 打包。

`Tests/` 是可选 PlayMode 测试程序集，额外要求 `UNITY_INCLUDE_TESTS`；普通项目不必启用它。所有 SDK/DLL 应按项目既有依赖管理方式安装，示例不负责安装或升级。
