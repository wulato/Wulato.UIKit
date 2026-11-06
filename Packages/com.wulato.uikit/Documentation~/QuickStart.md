# 从空项目接入

主包当前版本 0.4.0。推荐先跑通一个 Settings 页面，再阅读 [生命周期](Lifecycle.md) 和 [取消与错误](CancellationAndErrors.md)。本页不代表移动端或完整发布验收通过。

## 1. 安装主包与输入后端

开发默认使用 Unity **2022.3.62f2**，不固定额外验证版本。其他编辑器按兼容目标显式配置依赖和验证，不自动切换项目版本。默认宿主显式锁定 uGUI 1.0.0；已有 Unity 6 兼容 profile 使用 2.0.0，但不是本轮默认验收要求。UniTask 固定为：

```json
"com.cysharp.unitask": "https://github.com/Cysharp/UniTask.git?path=src/UniTask/Assets/Plugins/UniTask#2.5.11"
```

在 Package Manager 中通过 Add package from disk 选择主包 package.json；已有固定 Git 发布时可使用仓库 URL 的 `?path=Packages/com.wulato.uikit#<实际固定提交SHA>`，不要直接粘贴占位符。提交宿主解析后的 packages-lock.json。

- **Legacy：** 使用主包 LegacyUIInputDriver；项目输入设置须支持旧输入。
- **Input System：** 只需额外安装 Unity Input System 1.17.0。项目自行将 Active Input Handling 设置为 Input System；工作台拖入主包 Runtime/Integrations/InputSystem 的 InputSystemUIInputDriver.cs，无需 UIKit 适配包。
- **Addressables：** 额外安装 Unity Addressables 1.21.21（及对应 SBP 1.21.25），适配已在主包；Registry 的 addressables 来源自动路由，无需 WithLoader，见 [接入与所有权](Addressables/index.md)。

场景只能启用一个 UI 输入模块。工作台不删除普通 StandaloneInputModule、不自动安装包或修改 ProjectSettings；发现冲突会列出对象，请明确处理。

## 2. 单独运行 QuickStart

1. Package Manager → 主包 Samples → Import QuickStart。无需导入 Modals、Inventory，也无需移动默认版本目录。
2. 打开 `Tools > Wulato > UIKit > Workbench` 的“样例”页，选择 QuickStart。多份候选时明确选择目录。
3. 明确选择输入驱动，点击“预览并配置所选样例”，检查目录与改动摘要后应用。脏场景必须保存或取消。
4. 工具自动等待编译、写回绑定、安装 Usage partial 并接线样例场景。状态在“生成”页和窗口底部显示；失败后先修复再重试。
5. 点击“打开样例场景”，进入 PlayMode。按钮请求关闭；Demo 组件 Context Menu 的 Open UI 可再次打开。

Modals、Inventory 也可单独导入并通过主包工作台配置。Inventory 使用普通 ScrollRect，CacheOne 复用实例但重新创建 Session。旧 Samples 两步菜单仍保留，供原有三样例批处理使用；默认使用工作台。

工具按脚本身份和相邻资产确定目录，兼容默认版本目录与旧无版本目录。不会移动 Prefab/Scene，不覆盖已修改的样例源码或 Usage。编译完毕后再次配置应保持幂等。

## 3. 创建自己的界面

在工作台“界面”页选择新建，填写 Assets 下的目标目录、命名空间和类名。无参生成 UIView；带参先编译公开参数类型，再填写完整类型名。工具创建用户拥有的 partial 脚本和普通 RectTransform Prefab，等待编译后绑定并生成注册。

已有 UI 使用“已有 Prefab”模式，选择普通 Prefab 和已编译的 public partial View 脚本。工具保留 GUID、子节点及现有组件；冲突时拒绝覆盖。Variant 首版继续使用原有 Marker/Generate 手工流程。

选中 Prefab 后可编辑 Marker 的 Layer、Multiplicity、ClosePolicy、BackAction 和 Bindings。默认 Page/Single/Release/None。每个绑定明确填写字段名和 Prefab 内组件引用，不依赖节点名称猜测。保存后重新生成全部注册。

生成代码位于 Assets/Wulato.UIKit.Generated，并按业务 asmdef GUID 路由。现有 Generate、Complete writeback、Validate 菜单仍可使用；正常交互流程会在脚本编译后自动续跑。输入在写回前变化时必须重新生成，不能绕过指纹校验。

## 4. 配置场景并打开界面

工作台“场景”页明确选择 Root、生成的 Registry、EventSystem 和输入驱动，点击检查配置。缺少的 Root/EventSystem 会创建；支持 Undo，只标脏，不保存场景。配置只负责编辑器资产，业务仍显式创建并持有 Context：

```csharp
IUIContext ui = new UIContextBuilder(root, registry).Build();
IUISession<SettingsView> session = await ui.OpenAsync<SettingsView>();
UICompletion closed = await session.CloseAsync();
// 根据业务策略检查 closed.Errors 和 HasPendingCleanup。
await ui.ShutdownAsync();
```

SettingsView 为你自己创建的无参 View 类型。带参使用 `OpenAsync<TView, TArgs>(args, cancellationToken)`。业务自行持有 Root/Registry 引用，不需要全局单例。

所有操作从主线程提交；普通 UniTask 只 await 一次。不要在 View 钩子中等待同一 Context 的下一条普通命令。Session 句柄仅控制本次打开，不暴露缓存 View。更多配置见 [扩展接入](Extensions.md)。

## Inspector 宿主接入

工作台“场景”页配置 Root、Registry 和明确输入驱动后，使用“安装/更新 Context 宿主”。也可手工添加 UIContextHost 并在 Inspector 指定引用，默认在 Start 装配。不要同时保留同 Root 的旧业务启动器。

```csharp
IUIContext ui = host.Initialize();
var session = await ui.OpenAsync<SettingsView>();
await session.CloseAsync();
await host.ShutdownAsync();
```

需要 configure 注入时，在自动 Start 前（例如业务 Awake）调用 Initialize，或关闭 Initialize On Start。成功后再次无参调用返回同一 Context；再次传 configure 会拒绝。Build 失败可修复后显式重试，Shutdown 后不可重启同一宿主。禁用组件不关闭，销毁/退出 PlayMode 请求有界关闭；不接管外部共享依赖。原手动 Builder 路径继续有效。

[0.5 开发迁移](Migration-0.5.md) · [独立预览](Preview.md)

Host 主入口为 `host.UI`（Initialize 返回相同 Context）；Navigation/Dialogs/PopupQueue 默认装配，无需 Use*。普通 Open/Close 可直接使用 IUIContext，Presenter 仍可选。迁移及统一 Dialog 注册见 [0.5 迁移](Migration-0.5.md)。

2.2 起，Open(Page) 创建新根历史；追加页面请用 PushAsync，替换顶项用 ReplaceAsync，PopToAsync 恢复最近匹配历史项及原参数。Modal 等其他层 Open 不入历史。2.3 起 BackAsync 与输入 Cancel 统一处理本 Context 弹窗优先及页面返回；尊重 CanClose，根页 no-op。详见 [Navigation](Navigation/index.md)。

2.4 起普通弹窗可用 `await ui.DialogAsync(request, ct)`；排队弹窗用 `await ui.PopupAsync<NoticeView>(cancellationToken: ct)` 或带参数重载。View 在统一 Registry 注册为非 Page，取消仅作用于该请求。见 [Dialogs](Dialogs/index.md) 与 [Popup Queue](PopupQueue/index.md)。

3.1 起 Host 可配置单界面与页面配对 Preset；空字段仍使用 Immediate。可选 [DOTween](Integrations/DOTween.md) 提供 Fade/Scale/Slide，使用者按需安装 SDK 并显式启用。

需要响应式状态时，可选导入 [R3 Session Binding](Integrations/R3.md)；每次打开的订阅由 Session.Track 管理，业务持有 Model。简单界面无需引入 R3。
