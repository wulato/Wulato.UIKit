# FancyScrollView Catalog

需要官方 FancyScrollView 1.9.0（另行安装），导入本示例并增加 `WULATO_UIKIT_FANCY_SCROLL`。详见 UIKit 包内 [Controls 指南](../../Documentation~/Integrations/Controls.md)；导入 Assets 后从包文档打开该链接。

使用示例内 Resources/UIKitFancyCatalogDemo.prefab（CatalogView、CatalogList 与 SDK Scroller.viewport 已序列化连接），Registry Provider 注册：

```csharp
builder.Add<CatalogView, CatalogArgs>(new UIViewTypeKey("catalog"),
    UIViewAsset.Direct(catalogPrefab), UILayer.Modal,
    closePolicy: UIClosePolicy.CacheOne);
```

通过已有 Host / Builder 初始化 Context 后：

```csharp
var ids = Enumerable.Range(0, 10000).ToArray();
var args = new CatalogArgs(ids, async (id, ct) =>
{
    await UniTask.Delay(50, cancellationToken: ct);
    return "Item " + id;
});
var session = await ui.OpenAsync<CatalogView, CatalogArgs>(args);
```

CatalogView 可绑定自制 CatalogList 控件；务必在 Inspector 配置 Scroller.viewport，空引用会明确报错。CatalogList 使用 SDK 自带 Scroller / 池；CatalogCell 展示换项取消、同 ID 重绑的序号检查及 Session 结束后的迟到清理。原生滚动位置同时驱动 Cell 缩放，不需要额外动画 SDK。

固定行高 64；池保留最大已见视口所需数量。示例不承诺可变行高、动态数据源或通用列表 API。业务持有 ID 快照及 Loader；不要在打开期间原地修改 ID 列表。示例结果是字符串；替换为资源加载时必须补充迟到 Lease 的释放责任。

Tests 为可选 PlayMode 测试程序集，额外要求 UNITY_INCLUDE_TESTS。普通项目不用打开测试。未安装 SDK 时关闭示例符号；其他默认样例继续使用 ScrollRect。
