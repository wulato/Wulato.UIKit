# 从空项目接入

当前版本为 0.1.0 开发检查点，T10 提供 Editor 样例与性能基线；移动端/IL2CPP 发布验收留给 T11。

## 安装与输入

1. 使用 Unity **2022.3.62f2** 或 **6000.3.22f1** 新建空项目。用 Package Manager 安装 uGUI；2022 锁定 `com.unity.ugui: 1.0.0`，Unity 6 在项目 manifest 显式锁 `2.0.0`。
2. 在项目 `Packages/manifest.json` 添加 UniTask 的固定 Git 引用：`"com.cysharp.unitask": "https://github.com/Cysharp/UniTask.git?path=src/UniTask/Assets/Plugins/UniTask#2.5.11"`。不保留旧版 UniTask 声明。
3. 将本仓库固定提交安装为本地包（Package Manager → Add package from disk → `Packages/com.wulato.uikit/package.json`）。也可以在 Git 发布可用后使用 `https://github.com/wulato/Wulato.UIKit.git?path=Packages/com.wulato.uikit#<固定提交SHA>`。`<固定提交SHA>` 是调用方选择的真实固定提交，不能直接粘贴占位符。提交项目解析后的 packages-lock.json。
4. 旧输入项目使用一个 EventSystem 和 `LegacyUIInputDriver`，删除普通 StandaloneInputModule。新输入项目额外安装 Input System **1.17.0** 与本仓库 `com.wulato.uikit.input-system` 包，Active Input Handling 设为 Input System；将模块替换为 `InputSystemUIInputDriver`。只启用一个模块。主包不会自动发现或安装可选后端。

## 导入三个样例

Package Manager 中按 QuickStart、Modals、Inventory 导入三个 Samples，使用默认位置 `Assets/Samples/Wulato UIKit/0.1.0/<名称>` 时，请将三个目录一起移至 **`Assets/Samples/Wulato UIKit/<名称>`**（在 Unity Project 窗口移动以保留 GUID）。工具使用此明确路径，不搜索或改写任意业务目录。

导入后执行 `Tools > Wulato > UIKit > Samples > 1 Generate imported samples`，等待脚本编译结束，再执行 `2 Complete and wire scenes`，等待 Usage partial 编译，最后执行 `Tools > Wulato > UIKit > Validate`。前一次生成提供字段声明，第二步将 `.Usage.cs.txt` 启用为同程序集业务 partial，演示生成字段的实际使用；业务源代码不会被通用生成器改写。不要同时编译两份 Usage partial。

打开 SettingsScene、ModalsScene 或 InventoryScene，按上节给场景加入对应 EventSystem/驱动后进入 PlayMode。设置页演示 owner close/按钮请求关闭；Modals 的 `ModalsDemo.OpenAsync()` 可再开一层；关闭顶层恢复前层焦点。Inventory 使用固定玩家参数和普通 ScrollRect，关闭后保留一个 CacheOne 实例，下次打开刷新参数。示例不提供弹窗队列或导航历史。关闭全部窗口后可在 Inspector 的 demo 组件 Context Menu 中重新打开。

## 自己注册与生成

创建顶层 `public partial class MyView : UIView`（有参数时继承 `UIView<MyArgs>`）。先以无字段引用的源文件编译，再创建 **activeSelf 的 RectTransform 根 Prefab**，加入一个根 UIBindingMarker，分配业务 MonoScript、唯一 typeKey、层级、Multiplicity、ClosePolicy 和 BackAction。Bindings 中逐项填字段名及 Prefab 内组件引用，名称不依赖 GameObject 名字。

执行 `Tools > Wulato > UIKit > Generate`，等待编译，执行 `Complete writeback`。生成目录为 `Assets/Wulato.UIKit.Generated`，Bindings/provider 按业务 asmdef GUID 路由；将生成的 `UIRegistry.asset` 交给 Builder。生成后才在业务 partial 中直接使用生成字段。Prefab Variant 作为注册项时排除 base 的 includeInRegistry。Validate/构建前校验会拒绝过期、错误路由及缺失的写回结果。

```csharp
using System.Threading;
using Cysharp.Threading.Tasks;
using Wulato.UIKit;
using Wulato.UIKit.Samples.Inventory;

// root 是场景中的 UIRoot，registry 是生成资产；EventSystem 已安装匹配驱动。
IUIContext ui = new UIContextBuilder(root, registry).Build();
IUISession<InventoryView> session = await ui.OpenAsync<InventoryView, InventoryArgs>(
    new InventoryArgs("player-1", InventoryTab.Equipment), cancellationToken);
UICompletion closed = await session.CloseAsync();
// closed.Errors 和 HasPendingCleanup 需要按业务日志/退出策略处理。
await ui.ClearCacheAsync();
UIShutdownCompletion stopped = await ui.ShutdownAsync();
```

所有操作从主线程提交；普通操作的 UniTask 只 await 一次。Session 不公开可复用 View；保存 Session 只控制这一次打开。不要在 View 钩子中等待同一 Context 的下一条普通命令。完整生命周期见 [Lifecycle](Lifecycle.md)，失败处理见 [CancellationAndErrors](CancellationAndErrors.md)。
