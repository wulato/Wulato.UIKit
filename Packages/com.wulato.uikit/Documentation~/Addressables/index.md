# Addressables 接入与所有权

适配位于 `com.wulato.uikit/Runtime/Integrations/Addressables`，不再安装独立 UIKit 适配包。主包仅强制 uGUI/UniTask；项目按需安装 Addressables 1.21.21 / SBP 1.21.25。开发默认 Unity 2022.3.62f2，其他版本必须显式验证。升级见 [0.5 开发迁移](../Migration-0.5.md)。适配在 Unity 启动及 Editor 初始化时幂等注册工厂，默认按 Registry 自动路由，无需注入 Loader。

```csharp
builder.Add<InventoryView,int>(new UIViewTypeKey("inventory"),
    UIViewAsset.External("addressables", "UI/Inventory"),
    closePolicy: UIClosePolicy.CacheOne);
var ui = new UIContextBuilder(root, registry).Build();
var session = await ui.OpenAsync<InventoryView,int>(12, cancellationToken);
```

`builder` 的注册示例位于业务 UIRegistryProvider.Populate(UIRegistryBuilder builder) 内，Context 构建仍使用显式 UIRegistry。可直接配置完整 GUID；GUID 必须已收录 catalog。使用明确 address/GUID，不使用 label、子资产键或多资源匹配。Provider ID 固定 `addressables`，区分大小写，不 Trim。Prefab/Resources 描述仍走原默认加载器，缺失后端在默认 Context Build 时、分配资源前报错；Editor Validate/构建校验同样检查第一方 addressables 后端。自定义 provider 可继续显式 WithLoader，调用方保留依赖所有权。

Editor 的 UIBindingMarker 同时填写 externalProviderId/externalAssetId。Generate → 等待编译 → Complete writeback → Validate，之后由宿主显式构建本地 Addressables 内容。新注册输出 External 描述，无 Prefab 硬引用；绑定仍序列化到原 Prefab。地址改变会使生成产物过期。普通构建预处理只校验，不创建 Groups、不生成 C#、不构建 Bundle。Addressables 校验要求 Prefab 显式加入启用的 Bundle Group，address 唯一，Prefab 为 activeSelf=true 的 RectTransform 根及唯一 enabled UIView。

加载器先解析唯一 GameObject location，释放查询句柄，再加载并在非激活父节点下实例化。每次加载持有自己的引用；不建立全局池或资源缓存。View/Session 生命周期由核心协调。

调用方取消由核心及时交付并继续 FIFO；适配器持续观察底层操作一次，完成后检查取消，释放未交付资源。不能把直接调用加载器的任务提前当作取消完成，不能二次 await 同一普通 UniTask。失败保留原始异常，核心负责归类；未知地址、类型或配置错误不会创建活跃实例。

租约拥有实例及加载句柄。CacheOne 关闭保留两者，重开建立新的 Session；ClearCache、LRU 淘汰和最终释放同步禁用/脱离/销毁实例，实际销毁后释放句柄。Dispose 主线程幂等；外部先销毁实例也必须 Dispose 租约。迟到及延迟销毁清理由独立任务持有，不依赖 View/UIRoot 存活。不要对手动实例调用 ReleaseInstance，或自行释放加载器内部句柄。应用进程结束不承诺继续清理。

本批只提供本地内容加载闭环。远端 catalog、下载进度、更新与预加载属于宿主；不修改 Addressables 全局异常处理器或全局初始化所有权。导入 LocalInventory 可体验带参打开、缓存重开、清空与取消。

本批专项和安装验收不代表移动端、IL2CPP、性能或完整发布验收。
