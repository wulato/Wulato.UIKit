# Wulato.UIKit 通用 uGUI 框架方案

- 日期：2026-10-31
- 状态：架构契约已收敛；尚未实施
- 目标工程：Wulato.UIKit
- 文档范围：架构、API 契约、扩展边界与验收要求；不包含实现代码

## 1. 目标与已确认决策

基于 Unity uGUI 设计一套面向多个未来项目复用的 UI 框架。吸收 QFramework UIKit 的开发效率，以及 MC.unity 自研结构中 View、Model、Controller 分离和业务弹窗编排的经验。

用户已经确认：

1. 面向多个未来项目复用。
2. 不需要兼容或迁移 MC 现有界面。
3. 采用独立 UPM 包、小核心加可选扩展。
4. 当前阶段输出方案文档。
5. 核心异步采用 UniTask。
6. 首版仅提供单界面转场，导航扩展使用独立成对转场协议。
7. 公共 API 包含非泛型会话与类型擦除入口，无参/带参界面在编译期区分。
8. 验证版本固定为 Unity 2022.3.62f2 与 6000.3.22f1。
9. 新旧输入模块均纳入首版验收，主包及示例不依赖 TMP。
10. IL2CPP 验收平台为 Android ARM64 与 iOS ARM64。

成功标准：简单界面可以只写一个 View；复杂界面可以拆分展示与业务；各项目能替换加载、绑定、动画及依赖注入方案；并发、取消、关闭、缓存和场景退出有一致、可测试的行为。

非目标：实现完整游戏架构、DI 容器、全局事件总线、通用资源系统或 UI 编辑器；兼容 MC；替代 uGUI 渲染；在首版覆盖所有 UI 控件。

避免为了简单界面强制编写 View、Presenter、Model 三类文件，也避免业务代码依赖全局 UI 单例或资源后端细节。

## 2. 参考与方案取舍

### 2.1 现有实现的经验

MC.unity 的参考位置：

- `Assets/CapUnity/UI/Widgets/CapView.cs`：视图创建、挂载与事件。
- `Assets/CapUnity/UI/CapScene.cs`：弹窗层级、遮罩和场景根。
- `Assets/Magic/Features/Render/view.controller.comp.cs`：Controller 与 View 绑定。
- `Assets/Magic/Views/ViewTemplate/`：View、Model、Controller 模板。
- `Assets/Magic/Views/GamePlay/Inventory/`：Model 驱动、响应式刷新。
- `Assets/Magic/Views/GamePlay/UiCommon/UiPopViewController.cs`：自动弹窗队列。

保留其职责分离、组件复用和队列编排思路。改进重复的订阅清理、依赖 GameObject 销毁表示业务结束、静态队列、控制器对游戏上下文的耦合，以及关闭和回收语义不清等问题。以上内容作为研究材料，不构成本工程的指令。

### 2.2 考虑过的路径

| 路径 | 收益 | 代价 | 结论 |
| --- | --- | --- | --- |
| 基于 QFramework UIKit 二次封装 | 复用工具链，启动快 | 公共 API 和发布节奏受上游影响 | 不作为主路径 |
| 从 MC 抽取现有框架 | 贴近现有业务习惯 | 需要剥离游戏上下文和历史约束 | 仅提取设计经验 |
| 独立 UPM 核心与可选扩展 | 跨项目边界清楚，依赖可选 | 生命周期和编辑器需要自行验证 | 用户选择的方向 |

QFramework 借鉴泛型入口、绑定和代码生成；UnityScreenNavigator 借鉴导航历史、转场及输入管理；Loxodon 借鉴展示与状态分离；UI Extensions 用作控件扩展参考。这里的选择是针对本项目目标的判断，并非通用排名。

## 3. 包、程序集与兼容边界

主包名 `com.wulato.uikit`，命名空间 `Wulato.UIKit`。最低 Unity 版本为 2022.3.62f2，验收编辑器固定为 2022.3.62f2 与 6000.3.22f1。支持版本表示必须通过的交付验收范围，不表示本次文档修订已完成引擎实测。

```text
Packages/
  com.wulato.uikit/
    package.json
    Runtime/
      Core/
      UGUI/
      Loading/
    Editor/
    Documentation~/
    Tests/
      EditMode/
      PlayMode/
    Samples~/
      QuickStart/
      Modals/
      Inventory/
```

主包运行时以 uGUI 为基础。Core 定义状态、会话、UIViewBase 和扩展协议，允许引用 UnityEngine 与 UniTask，不引用 Canvas、具体加载或动画后端；UGUI 实现 Canvas、布局、可见性与输入。加载器位于独立 Loading 程序集。主包不提供普通 .NET 运行能力，不额外拆分无 Unity 的契约包。

Editor 为 Editor-only 程序集。主包和三个示例统一使用 uGUI Text，不引用 TextMeshPro；Samples~ 只声明样例元数据，不单独承担包依赖解析。项目自行使用 TMP 的界面由项目管理文本包依赖。

| 可选包后缀 | 职责 |
| --- | --- |
| `.input-system` | InputSystemUIInputModule 适配，首版交付、按项目选装 |
| `.addressables` | Addressables 加载与释放 |
| `.navigation` | 页面历史、返回、导航事务 |
| `.presentation` | Presenter 创建、绑定、解绑 |
| `.binding.r3` | 响应式绑定适配 |
| `.transitions.dotween` | DOTween 转场 |
| `.collections` | 虚拟列表、网格和 Cell 回收 |
| `.dialogs` | 带类型结果的确认、输入等对话框 |
| `.popup-queue` | 排队、优先级、去重、暂停 |

依赖方向为扩展到核心，不允许核心反向引用扩展。扩展间如有依赖，必须在包和程序集层显式声明。

核心不得通过条件编译、类型名反射或自动扫描发现特定可选包。加载、转场、展示和输入后端通过核心定义的接口或能力入口，由宿主显式注入；适配注册代码属于宿主或扩展包。内部事务实现可保持私有，但扩展所需能力必须通过明确的跨程序集协议提供，不能要求扩展访问任意内部状态。

核心异步统一使用 `UniTask`、`UniTask<T>` 与 `CancellationToken`，依赖 UniTask 2.5.11。响应式、Presenter 和 DI 均为可选扩展，不因异步选型进入核心。主线程/帧等待协议为 `IUIAsyncScheduler`，默认以 UniTask PlayerLoop 实现，宿主可注入测试调度器；Unity 对象访问前必须显式回到主线程。

调度器提供主线程切换、下一帧 Update、scaled/unscaled 时间等待。下一帧使用 NextFrame 语义，不将 Yield 或延时等同于布局完成；布局准备采用第 9.1 节的屏障。超时使用 unscaled 真实经过时间，运行时调度与迟到清理宿主独立于 View 和 UIRoot 存活。

普通操作返回的 UniTask 按单次消费处理。`Closed`、重复关闭、重复 Shutdown 使用每会话/每 Context 固定且不复用的 `UniTaskCompletionSource<T>`，允许同时和重复等待；不使用单次消费或池化完成源承载这些共享结果。任意底层加载/钩子/转场 UniTask 只由一个观察执行器 await 一次，再将结果转入内部完成记录；取消/超时竞争和迟到清理观察该记录，不能第二次 await 原操作。参见 [UniTask 的消费与完成源契约](https://github.com/Cysharp/UniTask/tree/2.5.11)。

外部 Task 服务可通过适配接入；导出 Task 时为同一共享结果只转换并缓存一次，框架公共 API 不混用两套异步类型。业务不使用 Task.Run 执行生命周期或动画。

### 3.1 跨程序集能力协议清单

以下协议属于核心扩展边界；所有权、调用时序、异常与停止行为遵循本方案。实现计划只拆解实现和验证任务，不重新选择协议职责。

| 能力 | 协议边界与实施要求 |
| --- | --- |
| View 加载与租约 | `IUIViewLoader` / `IUIViewLease`；明确未交付、已交付和迟到租约的释放责任，见第 7.1、9 节 |
| 主线程与帧等待 | 最小可注入调度协议；生命周期及超时观察不依赖 View 或 Root 存活，见第 3、7.1 节 |
| 转场 | `IUITransition`；进入/退出、停止、基准捕获及幂等复位，见第 7.2 节 |
| 输入 | 输入域注册、锁租约、所有权/焦点意图及注销；共享设施只由仲裁器操作，见第 8.1 节 |
| 扩展服务 | 显式注册及注销、Context 停止通知；受总退出预算约束，不得在停止回调中等待同一队列命令 |
| 会话扩展 | IUISessionLifecycleObserver；打开准备、终结及终结通知，绑定租约随 Session 释放，见第 6.1 节 |
| 导航事务 | prepare/commit/rollback 能力入口；事务步骤选择 CoreDefault 或 External 转场所有者，保证只有一套动画与一次生命周期，见第 10 节 |

先用默认实现或测试替身验证跨程序集接入，无须为此提前实现 Addressables、DOTween 或导航包。

### 3.2 UPM 与程序集契约

主包首个版本为 0.1.0，package.json 设置 `unity: "2022.3"`、`unityRelease: "62f2"`，运行时依赖 `com.unity.ugui: "1.0.0"` 与 `com.cysharp.unitask: "2.5.11"`，不声明 TMP 或 Input System 依赖。消费项目显式锁定依赖；2022.3 验收项目锁定 uGUI 1.0.0，6000.3 验收项目锁定 uGUI 2.0.0。Input System 适配包名为 `com.wulato.uikit.input-system`，0.1.0 版本依赖主包 0.1.0 与 `com.unity.inputsystem: "1.17.0"`，两个验收项目均使用该输入版本。包声明的依赖与 Samples 元数据遵循 [Unity 包清单格式](https://docs.unity3d.com/2022.3/Documentation/Manual/upm-manifestPkg.html)。

开发工程使用内嵌 Packages/com.wulato.uikit，消费项目使用固定 tag/commit 的 Git URL 与 package path；不得依赖移动分支。UniTask 由宿主 manifest 显式添加 `https://github.com/Cysharp/UniTask.git?path=src/UniTask/Assets/Plugins/UniTask#2.5.11`，主包声明数字版本依赖，不把 Git URL 当作包级传递依赖。Wulato Git 扩展同样由宿主显式添加，CI 提交并使用 packages-lock.json，不改写 Library/PackageCache。

首版消费项目锁定 UniTask 2.5.11，不接受低于该版本的宿主依赖；升级到其他版本须先通过兼容矩阵并更新锁文件。保留显式 uGUI 依赖，不把模板默认安装视为包契约。干净接入测试覆盖 Unity 6 宿主未显式声明 uGUI、宿主锁定 uGUI 2.0.0、已有低版本 UniTask 三种情况；接入校验拒绝实际依赖不符的环境并给出修复提示，不依赖 UPM 一定自动升级或报冲突。Editor 接入校验与 CI 同时校验实际 Unity 版本，不能仅依赖 unityRelease 元数据阻止旧编辑器。

| asmdef 名称 | 显式引用 | 平台与边界 |
| --- | --- | --- |
| Wulato.UIKit.Core | UniTask | Runtime；UIViewBase、契约、注册数据、调度与 Session 状态 |
| Wulato.UIKit.Loading | Core、UniTask | Runtime；Prefab/Resources 默认加载，无 UGUI 反向引用 |
| Wulato.UIKit.UGUI | Core、Loading、UniTask、UnityEngine.UI | Runtime；UIRoot、UIContextBuilder、Canvas、布局和旧输入驱动 |
| Wulato.UIKit.Editor | Core、Loading、UGUI、UniTask、UnityEngine.UI | Editor-only；生成、绑定、校验 |
| Wulato.UIKit.Tests.EditMode | Core、Loading、UGUI、UniTask、UnityEngine.TestRunner、UnityEditor.TestRunner | Editor-only；TestAssemblies 标记，非 Player 依赖 |
| Wulato.UIKit.Tests.PlayMode | Core、Loading、UGUI、UniTask、UnityEngine.TestRunner | TestAssemblies 标记；由测试宿主启用，不进入正式构建 |
| Wulato.UIKit.InputSystem | Core、UGUI、UniTask、Unity.InputSystem | 仅在选装适配包内，不由主包 asmdef 引用 |

Core、Loading、UGUI 的缩写在表内对应完整 Wulato.UIKit.* 名称。UnityEngine 引擎引用由默认 Unity 引用提供；运行时不引用 Editor 或 Test 程序集。示例有独立 asmdef，只引用主包与 UniTask；Input System 的测试位于适配包。

采用 SemVer：0.x 的破坏性变更只进入新的 minor，patch 不破坏公开契约；1.x 起破坏性变更提升 major。所有变更记录 CHANGELOG.md；能兼容保留的旧入口至少保留一个 minor 并标记 Obsolete，必须直接删除的 0.x 变更同时交付迁移说明。扩展包声明其编译使用的核心基线版本，CI 验证与消费项目实际锁定版本的组合。

## 4. 核心模型

| 类型 | 职责与边界 |
| --- | --- |
| `UIRoot` | 序列化配置挂载点、Canvas、相机、遮罩与安全区域 |
| `UIContext` | 独立运行环境；持有注册、加载器、调度器、会话和缓存 |
| `UIRegistry` | View 类型到资源和策略的映射，构建前校验 |
| `UIViewBase` / `UIView` / `UIView<TArgs>` | 共同视图基础与无参/带参分支；持有引用、展示参数并转发操作 |
| `IUISession` / `IUISession<TView>` | 一次打开产生的唯一身份、状态与关闭结果 |
| `IUIViewLoader` | 创建 View 并返回资源租约 |

不提供强制全局单例。普通项目创建一个 Context，多玩家或独立预览可以创建多个 Context。

View 实例与 Session 必须分离。缓存可以复用 View，每次打开都新建 Session。所有延迟回调验证 Session 身份；旧句柄不能关闭或更新已被新会话复用的实例。

首版设计支持 `Multiplicity: Single / Multi`；Multi 表示同类型可同时存在多个活跃 Session，与关闭后的实例复用无关。`UISessionId` 在 Context 的整个存活期内不复用；跨 Context 路由使用 Context 身份和 SessionId 的组合。`UISessionScope` 提供该身份及作用域，事件订阅、结果回传和关闭请求绑定具体 Session；View 类型只能用于注册或显式批量操作。框架入口拒绝过期身份，业务延迟回调也必须验证所捕获的会话，不能读取 View 当前 Session 后把旧结果投递给新会话。

注册表每个 View 类型只对应一个定义，多皮肤、多 Prefab 变体使用不同 View 类型；UIViewTypeKey 只是该类型定义的稳定标识，不引入同类型的多资源变体，不能隐式按类名猜测资源路径。

父界面内的子视图和列表 Cell 不属于独立 Session，不进入全局层级与导航历史，其资源由父级负责。

## 5. 对外 API 契约

`UIViewBase : MonoBehaviour` 为共同基类；无参界面继承 `UIView : UIViewBase`，带参界面继承 `UIView<TArgs> : UIViewBase`。两条分支互不继承，带参 View 不能调用无参 OpenAsync。框架实现会话句柄，业务只消费接口，不实现框架内部会话状态机。

```csharp
public interface IUIContext
{
    UniTask<IUISession<TView>> OpenAsync<TView>(
        CancellationToken cancellationToken = default)
        where TView : UIView;

    UniTask<IUISession<TView>> OpenAsync<TView, TArgs>(
        TArgs args, CancellationToken cancellationToken = default)
        where TView : UIView<TArgs>;

    UniTask<IUISession> OpenAsync(
        UIViewTypeKey type, UIArguments args,
        CancellationToken cancellationToken = default);

    UniTask<UIBatchCompletion> CloseAllAsync(
        UICloseReason reason = UICloseReason.Owner);
    UniTask<UIBatchCompletion> CloseAllOfTypeAsync<TView>(
        UICloseReason reason = UICloseReason.Owner)
        where TView : UIViewBase;
    UniTask ClearCacheAsync();
    UniTask<UIShutdownCompletion> ShutdownAsync();
    TService GetService<TService>() where TService : class;
    UIDiagnosticsSnapshot GetDiagnostics();
}

public interface IUISession
{
    UISessionId Id { get; }
    UIState State { get; }
    UniTask<UICompletion> Closed { get; }
    UniTask<UICompletion> CloseAsync(
        UICloseReason reason = UICloseReason.Owner);
}

public interface IUISession<out TView> : IUISession
    where TView : UIViewBase { }
```

`UIViewTypeKey` 为生成注册映射中的稳定类型标识，不是资源路径或运行时反射名称。`UIArguments.None` 表示无参，`UIArguments.From<TArgs>(args)` 保存参数类型与值；泛型入口和扩展入口共享调度及校验，在加载前拒绝参数不匹配。注册生成器输出类型 key 和显式工厂桥接；导航历史和异质队列通过该入口工作，不反射调用泛型方法。非泛型句柄同样不暴露 MonoBehaviour。

首版统一使用上述参数容器，struct 参数会装箱，泛型入口也不承诺消除该分配。CloseAllOfTypeAsync 只匹配精确的已注册具体 View 类型，不包含派生类型；以 UIViewBase、UIView 或未注册类型调用返回 InvalidRequest，全量关闭使用 CloseAllAsync。

```csharp
var inventory = await ui.OpenAsync<InventoryView, InventoryArgs>(
    new InventoryArgs(playerId, InventoryTab.Equipment), ct);
UICompletion completion = await inventory.CloseAsync();
```

UICompletion 包含 SessionId、Reason、Errors 和 HasPendingCleanup。UIBatchCompletion 包含本次快照的逐项 UICompletion 与批量清理错误；UIShutdownCompletion 包含所有终结结果、全局清理错误及 PendingCleanupCount。它们都是不可变结果，不携带仍可操作 View 的引用。

GetDiagnostics 在主线程同步返回不可变快照：Context 身份与运行状态、各 Session 的身份/类型 key/状态、活跃与隔离租约计数、缓存 key 与 LRU 顺序、输入所有者及锁数量、未决清理数。快照不返回 GameObject、View、Model 或可操作租约；含采样时间和采集级别，关闭高成本诊断后仍返回基础状态，详细条目为空。Context 停止后保留最终快照，后续迟到清理由独立宿主报告。

核心只返回会话终结信息，不承载业务选择结果。普通业务通过参数中的会话级回调或业务接口回传选择，例如 `OnSelected(itemId)`；回调捕获 Session 身份，过期会话不提交结果。需要统一提交/取消/退出结果的交互使用 Dialogs 扩展，业务结果不塞进 UICompletion 或全局事件总线。

OpenAsync 在资源就绪、生命周期、布局、进入动画与输入提交完成后返回。失败或取消通过返回操作报告，不提前发布半打开句柄。批量关闭在执行时对活跃 Session 取快照，逐项完成清理并返回每项 UICompletion；该命令之后排队的打开不属于此快照。关闭接受后不暴露取消 Token，每项受关闭预算约束。

`CloseAsync` 与 `Closed` 共用稳定完成源：退出钩子结束或被隔离，View 已隐藏且无输入，实例已缓存、释放已尝试或明确移交独立隔离清理所有者，活跃索引和单实例占用已解除，遮罩及焦点已重新仲裁，随后提交 Closed。UICompletion 包含关闭原因、全部清理错误及未决隔离标记，不把隔离等同于资源释放。

主线程完成通知队列由独立运行时宿主持有，Root 销毁及 Shutdown 期间继续泵送，直到所有会话与 Shutdown 结果完成；它不依赖 View 的 Update。完成屏障在主线程一次提交，中间不 await，不执行用户通知；缓存去激活、层级和输入候选移除先于完成。在完整状态提交后，通过主线程完成通知队列触发完成源。UniTask 续体可以同步执行，因此通知队列不在状态修改栈内触发；续体提交的新命令只入队，不能重入正在调度的命令。连续和多方等待返回同一结果，完成后可立即重新打开同类型。

Shutdown 接受后，未完成的关闭仍正常完成其共享结果，原因采用 ContextShutdown，并保留已收集错误；不额外取消或故障化 Closed。多次关闭以第一次接受的原因终结，只有 ContextShutdown/RootDestroyed 强制退出可覆盖该原因。RootDestroyed 与 ContextShutdown 为框架保留原因，业务 CloseAsync 不得伪造。

### 5.1 Context 创建与注入

```csharp
IUIContext ui = new UIContextBuilder(root, registry)
    .WithSettings(settings)
    .Build();
```

UIRoot 与 UIRegistry 为必填，Build 在主线程同步校验并创建独立 Context：复制注册表与配置快照，校验类型、输入模块、层级和必要组件，同一 Root 不允许绑定两个存活 Context。默认提供 Prefab/Resources 加载器、UniTask 调度器、无动画转场、布局屏障和 Unity 日志错误接收器。Builder 通过 WithLoader、WithScheduler、WithTransition、WithLayoutReady、WithErrorSink 显式替换相应协议。

单 Context 默认创建一个输入仲裁器；多个 Context 共用输入域时必须通过 WithInputArbiter 传入同一个宿主仲裁器。输入域在 EventSystem 上保存唯一仲裁器注册：第二个 Context 未显式注入已有仲裁器或注入不同实例时 Build 失败，且释放本次构建的全部临时资源；禁止两个默认仲裁器各自接管同一 EventSystem。Input System 项目必须由对应适配包显式提供输入驱动，未安装或模块不匹配时 Build 报错，不静默降级。默认创建的服务随 Context 释放；外部注入的共享服务由宿主拥有，Context 只释放自己的注册和租约。

Builder.WithServiceFactory 注册每 Context 的扩展服务工厂，Build 后服务集合只读；Context 停止时通知、解绑并释放所创建服务。服务的业务调用须遵循同一队列与嵌套调用规则，不能在停止回调中等待自己的队列命令。UIRegistry 以 ScriptableObject 编辑，运行时使用经校验的不可变映射，不允许运行中修改注册配置。

宿主通过 ui.GetService<TService>() 取得已注册扩展，未注册类型抛 InvalidOperationException，Context 停止后查询抛 UIException(ContextStopped)；GetDiagnostics 仍允许读取最终快照。GetService 是同步查询，不属于异步失败操作。每服务类型只允许一份注册，按注册顺序创建并逆序释放，工厂只能取得已经创建的依赖，构建失败时回滚已创建服务。View 不自行查询服务，通过参数、Session 扩展或 Presenter 接收业务依赖。

## 6. View 生命周期与作用域

```csharp
public sealed class InventoryView : UIView<InventoryArgs>
{
    protected override void OnCreate()
    {
        closeButton.onClick.AddListener(RequestClose);
    }

    protected override UniTask OnOpenAsync(
        InventoryArgs args,
        UISessionScope session,
        CancellationToken cancellationToken)
    {
        playerName.text = args.PlayerId;
        SelectTab(args.InitialTab);
        return UniTask.CompletedTask;
    }

    protected override void OnClosed(UICloseReason reason)
    {
        ClearSelection();
    }
}
```

View 作者可覆盖的钩子如下；同步钩子不得阻塞或返回异步工作。首次打开的完整顺序仅在第 9 节定义，避免维护两份准备流程。

| 所属分支 | protected virtual 签名 | 时机与失败行为 |
| --- | --- | --- |
| `UIViewBase` | `void OnCreate()` | 每实例一次，未激活；失败释放实例 |
| `UIView` | `UniTask OnOpenAsync(UISessionScope session, CancellationToken cancellationToken)` | 每 Session 一次；失败执行打开回滚 |
| `UIView<TArgs>` | `UniTask OnOpenAsync(TArgs args, UISessionScope session, CancellationToken cancellationToken)` | 每 Session 一次；失败执行打开回滚 |
| `UIViewBase` | `bool CanClose(UICloseReason reason)` | 用户关闭入口，同步否决；异常记错并视为否决 |
| `UIViewBase` | `UniTask OnCloseAsync(UICloseReason reason, CancellationToken cancellationToken)` | 接受关闭并终止 Session 作用域后；异常不阻止清理 |
| `UIViewBase` | `void OnForegroundEntered(UIForegroundScope foreground)` | 取得前台资格，携带本次新建作用域 |
| `UIViewBase` | `void OnForegroundExited()` | 失去资格，Foreground Token 取消及订阅释放后通知 |
| `UIViewBase` | `void OnClosed(UICloseReason reason)` | 正常关闭或已进入 OnOpenAsync 的打开回滚，按配对表执行 |
| `UIViewBase` | `void OnDispose()` | 最终实例释放前；容忍部分初始化，异常不阻止租约释放 |

UIViewBase 另提供非 virtual 的 `protected void RequestClose()`，关闭按钮以 UserRequest 原因路由到当前会话的 CanClose；无活跃会话时忽略。返回键与遮罩分别传入 Back、Mask 原因。默认同步通知为空、CanClose 返回 true、异步钩子返回 UniTask.CompletedTask。

首次取得前台资格时，先创建临时 Foreground 作用域并调用 OnForegroundEntered，成功后才提交 Open/输入并发布句柄；失败撤回候选资格，按打开失败回滚，OpenAsync 报错。打开时没有前台资格的 Toast 等界面不调用该通知。已 Open 的会话恢复前台时通知失败，撤回资格并按生命周期错误终结会话；Closing 中 OnForegroundExited 抛错只加入当前清理错误，不递归发起第二次终结。

UIView 与 UIView<TArgs> 通过 UIViewBase 的内部桥接进入统一调度器；无参/带参分支在泛型约束中区分，类型擦除入口另在加载前校验，不使用隐式 null 规避类型要求。

| 作用域 | 生命周期 | 典型资源 |
| --- | --- | --- |
| Instance | 实例创建至最终释放 | 固定监听、实例组件 |
| Session | 打开开始至关闭被接受或打开回滚 | 数据订阅、业务请求 |
| Foreground | 当前具备前台交互资格的每段期间 | 输入、焦点、前台刷新 |

作用域提供取消 Token 和 `Track(IDisposable)`，释放需幂等；某个 Dispose 抛错不能阻止其他资源清理。每次恢复前台创建新的 Foreground 作用域。

UISessionScope 是会话身份、SessionToken、Track 和调度/布局能力的受限上下文，不是 IUISession 操作句柄；它不暴露 OpenAsync/CloseAsync 或可变 Context 状态。它允许被延迟回调捕获，Session 结束后 Token 保持取消、IsCurrent=false，Track 新资源立即 Dispose，不得向复用的 View 继续投递。IUISession 只在 Open 成功后发布，负责外部所有者关闭与等待结果。

OnOpenAsync 的 cancellationToken 为独立打开阶段 Token，链接调用方打开 Token、SessionToken、Context 停止及打开超时；打开完成后释放调用方取消注册，业务长期订阅必须使用 scope.SessionToken，不能把打开 Token 当作会话存活信号。OnCloseAsync 的 Token 独立于已取消的 SessionToken，受关闭生命周期预算和剩余 Shutdown 预算控制。OnOpenAsync 内可以通过 scope.Scheduler 等帧、通过 scope.WaitLayoutAsync 等布局，它们是当前命令的准备步骤，不重新排队，不属于禁止的嵌套 UI 命令，且计入打开阶段预算；返回后框架仍执行最终布局屏障。

关闭开始立即取消 Session Token，退出动画使用独立且有超时上限的转场 Token。`OnCloseAsync` 不能依赖已经释放的 Session 订阅，需用参数或快照完成退出展示。

Unity 的 `OnEnable`、`OnDisable` 不代表业务打开与关闭。缓存隐藏和父级激活变化不能自动创建新会话。

内部状态为 `Opening → Open → Closing → Closed`；公开句柄在成功打开后返回，因此正常使用只观察到 Open、Closing、Closed。Opening 仅出现在框架内部和只读诊断中；失败的打开在内部回滚后终止，不提前发布句柄。缓存属于实例管理状态，不属于 Session 状态。

打开回滚与正常关闭分开，钩子配对规则如下：

| 失败位置 | 清理行为 |
| --- | --- |
| 加载失败，未交付租约 | 加载器清理内部部分资源；不调用 View 钩子 |
| `OnCreate` 失败 | 释放已登记的 Instance 资源与租约；不调用 `OnCloseAsync` / `OnClosed`，不缓存 |
| OnCreate 已完成，但尚未进入 OnOpenAsync 的会话扩展失败 | 终结已创建的扩展绑定及 Session 作用域，释放实例；不调用 View 的 OnCloseAsync / OnClosed，不缓存 |
| 已进入 `OnOpenAsync`，随后失败或取消（含进入动画） | 取消并释放 Session/Foreground；跳过正常 `OnCloseAsync` 和退出动画；调用一次 `OnClosed(OpenFailed/OpenCanceled)` 做同步收尾，再释放实例，不缓存 |
| 已成功提交 Open 后关闭 | 按正常关闭流程执行；钩子异常不阻止剩余清理 |

`OnClosed` 必须容忍只完成部分打开初始化的状态。最终实例释放钩子也必须容忍 `OnCreate` 未完成，并与 Instance 作用域统一幂等清理。已超时且仍运行的回调按第 7 节隔离，不再并发调用该 View 的收尾钩子；由作用域及租约执行框架侧清理，相关钩子记为跳过。验收中的“每 Session 一次关闭”指终结与清理一次，不要求失败路径执行完整正常关闭钩子。

### 6.1 会话扩展生命周期

Builder.WithSessionObserver 注册 IUISessionLifecycleObserver，构建后集合只读；Context 服务工厂可返回实现该接口的服务并在构建时登记。接口同步提供以下回调，遵循主线程与禁止等待嵌套命令规则：

```csharp
public interface IUISessionLifecycleObserver
{
    IDisposable OnPreparing(
        UIViewBase view, UIArguments args, UISessionScope session);
    void OnTerminating(UISessionScope session, UICloseReason reason);
    void OnTerminated(UICompletion completion);
}
```

OnPreparing 在 OnCreate 成功、Session 及参数建立、隐藏激活并执行 Awake/OnEnable 后，且在 OnOpenAsync 前按登记顺序执行；返回幂等的绑定租约，框架纳入 Session 清理。Preparing 失败使打开失败，当前工厂负责其尚未交付的部分资源，框架逆序释放之前已交付绑定。只有成功返回绑定的观察者收到该 Session 的终结回调。

接受关闭或打开回滚时先撤回前台资格、取消 SessionToken，再逆序通知 OnTerminating、释放绑定及其他 Session 资源；Terminating 异常汇入清理错误，继续处理其余观察者。OnTerminated 在 View 收尾及缓存/释放决策完成后、Closed 屏障提交前执行；异常同样汇入最终结果，不阻断完成。回调接收当时的只读结果快照，Closed 保存包含所有观察者收尾错误的最终结果。Presenter 扩展在 Preparing 创建/绑定 Presenter，返回的绑定租约负责解绑与 Dispose，不让 View 自行创建 Presenter。

## 7. 异步、取消与异常契约

首版每个 Context 以 FIFO 串行执行完整命令，串行范围包含状态变更、生命周期和单界面转场的全部等待期；不同普通命令的动画不重叠。预加载可以独立并行，不改变会话和输入状态。所有 Unity 对象访问在主线程执行；公共操作要求从主线程提交，后台提交在开发模式下明确报错。

| 情况 | 行为 |
| --- | --- |
| 单实例重复打开 | 返回错误码 AlreadyOpen 的失败操作，不忽略新参数 |
| 多实例重复打开 | 每次创建新 Session |
| 同一 Session 重复关闭 | 等待同一共享完成源，不重复触发生命周期 |
| 排队等待期间取消 | 不启动加载，释放请求占用 |
| 加载或打开期间取消 | 回滚资源、绑定和可见性，UniTask 取消 |
| 打开完成后取消原 Token | 不关闭已打开 Session |
| 关闭钩子或动画失败 | 记录错误，继续完成清理和 Closed |
| 根节点被外部销毁 | 进入紧急清理，不缓存失效实例 |
| Shutdown | 立即拒绝新请求并取消当前打开，再执行完整清理 |

取消 Token 可从回调中触发取消，但实际 Unity 状态修改回到主线程进行。不可取消的加载按下述有界等待规则托管；不能让已取消请求重新显示。

单实例判断在调度执行时进行，成功打开前不允许同类型请求越过它。生命周期回调不可等待同一 Context 的嵌套命令；使用回调执行标记检测并快速失败，避免死锁。跨界面跳转由外部流程在当前操作完成后编排。

单实例判断服从命令 FIFO，而不在提交时提前占用类型：已经排队的关闭先执行，后续打开可以成功；先前打开失败后下一条请求也可以正常尝试。串行执行使检查与会话登记不被其他命令插入，这不是并发检查竞态。拒绝结果通过该请求的失败任务返回；框架不承诺所有重复请求在提交瞬间被拒绝。

Single 重复打开始终拒绝，不隐式更新参数、聚焦或替换会话，不交付 SingleFocus/SingleReplace 操作或注册枚举。

关闭分为用户请求关闭与所有者强制关闭。返回键、遮罩、关闭按钮走 `CanClose(reason)`；Session.CloseAsync、批量清理、Shutdown 为所有者关闭，不受业务拦截。关闭任务一旦接受不向调用者暴露取消，避免半关闭状态。

需要异步确认关闭时，CanClose 同步返回 false，并向业务流程投递一次确认请求；业务在当前回调返回后打开确认对话框，确认后凭原 Session 身份调用 CloseAsync。等待期间重复关闭请求去重，原会话已结束则放弃确认结果。CanClose 不同步等待同一 Context 的对话命令。

### 7.1 有界等待、迟到结果与退出

加载、生命周期和转场等待分别设定可配置超时，使用不受 `Time.timeScale` 影响的时钟。调用方取消、Context 停止或等待超时后，框架停止等待底层 UniTask 并解除调度占用，不依赖底层实现主动响应 Token；超时报告明确的超时错误，调用方取消报告 UniTask 取消。无法协作停止的 View 不进入缓存，禁止迟到回调重新提交框架状态，并释放或隔离其资源。不得宣称 CancellationToken 能强制终止任意用户代码；同步阻塞主线程的代码无法靠该超时机制抢占。

对尚未完成的加载保留独立的迟到结果观察者：观察异常，成功交付租约后在主线程幂等释放，永不挂载到活跃层。观察者的清理调度不能依赖已销毁的 UIRoot 或已关闭的 Context，且不保留不必要的业务对象。加载器在返回租约前拥有部分资源；失败或取消且不交付租约时，由加载器清理。迟到观察者订阅原操作对应的内部完成记录，不再次 await 原始 UniTask；该记录不再被普通队列或 Shutdown 等待。

同一迟到任务观察机制覆盖被放弃等待的打开钩子、关闭钩子与转场；全部记录来源、会话身份及最终异常，不将它们继续计入命令队列或 Shutdown 的等待。已交付实例立即失去交互和缓存资格；能安全停止时释放租约，不能停止时在非激活隔离区移交独立清理所有者，并在迟到任务结束后释放。Shutdown 报告未决隔离租约，不把移交等同于释放。用户任务自行创建且未登记的外部资源仍由业务拥有；框架不能强制回收任意用户代码中的局部资源。

Shutdown 必须能在普通命令队列之外发出停止信号，不能等待被挂起的 Open 自己完成后才取消它。多次 Shutdown 返回同一完成任务。

Shutdown 立即拒绝新请求，终结排队任务，打断当前打开的等待，再按有界清理流程终结活跃会话、释放缓存、停止扩展服务和输入注册。它完成表示框架已停止，已持有租约已释放或明确移交隔离清理所有者，迟到任务已移交观察；不表示永不完成的第三方操作已被强制终止。诊断必须保留未决任务、隔离租约和清理错误。清理预算须包含整个退出流程的总上限，不能仅靠每个界面的超时叠加；超出预算时跳过余下展示钩子，执行必要的同步框架清理。应用进程结束后不承诺继续执行迟到清理。

### 7.2 转场取消与状态复位

转场通过可注入的 `IUITransition` 协议接入，提供 `EnterAsync`、`ExitAsync`、`Stop` 和 `Reset`；异步方法接收单一 UITransitionContext 与 CancellationToken 并返回 UniTask，Stop/Reset 为同步幂等操作。每次转场持有独立 Token，进入 Token 联动打开取消与 Context 停止，退出 Token 独立于已取消的 Session Token，并受退出预算约束。适配器负责停止其 tween/协程，恢复其修改过的透明度、缩放、位移等属性；复位基于捕获的布局基准，不假定 Prefab 的缩放和位置为默认值。

Stop/Reset 均接收对应 UITransitionContext。Stop 返回前必须停止该次动画的全部写入，并将仍运行的 EnterAsync/ExitAsync 完成源置为取消；已经结束的结果保持不变。单一观察执行器接收该结果，不能重新 await 原 UniTask。核心收到取消信号即调用 Stop、核验内部完成记录并复位，不继续等待正常的转场超时；适配器不遵守 Stop 契约时直接撤回可见性并隔离实例、记录协议错误。两秒上限是动画或适配器失效兜底，不是每次取消必须等待的时长。

转场播放默认使用 unscaled 时间，暂停菜单在 `Time.timeScale = 0` 时仍可播放；等待超时始终使用真实经过时间。转场只能写入专用动画节点的透明度、localScale、anchoredPosition 偏移和 localRotation，并在执行前声明实际写集；不得写入可见性门控、业务节点的 sizeDelta、anchors、pivot、层级顺序或 LayoutGroup 配置。布局驱动节点外包独立动画节点，避免 LayoutGroup 与转场同时写同一属性。缓存复位只还原这些转场属性；业务在每次 OnOpenAsync 中明确重建会话展示状态。

每次打开在 `OnOpenAsync` 完成、所需布局准备完成后，且进入动画首次写入属性前，捕获该次进入的展示基准；不是在实例创建或上次缓存时捕获。展示基准表示未施加动画偏移的正常展示值，安全隐藏通过独立的可见性/输入门控保证，不以把透明度强制归零代替基准。关闭时在 `OnCloseAsync` 完成、退出动画写入前捕获退出基准，保留业务在打开期间作出的布局变更；复位仅还原转场拥有的属性，不覆盖业务状态。首版适配器遵循上述捕获时机和属性写集，不覆盖业务布局。

- 进入被取消或失败：先禁用交互并隐藏，停止动画后复位到安全隐藏态，按打开失败回滚，不返回成功 Session。
- 正常进入完成：提交可见终态后才能提交 Open。正常退出、退出失败或超时均最终隐藏；仅停止及复位成功的健康实例可进入缓存。进入基准捕获并施加入场初态后、动画播放首帧前解除外壳可见性门控，输入锁保持至 Open 提交，不能让整段入场动画在 alpha=0 的外壳下播放。
- 无法停止的迟到动画或复位失败：隔离并释放实例，不允许缓存复用；Session 身份校验不能代替停止对 Transform 等对象的直接写入。

转场期间默认丢弃重复返回输入，不把连击变成多个关闭命令。尚无公开 Session 的 Opening 通过原打开 Token 或 Shutdown 中止；打开成功后的取消原 Token 不触发退出。导航替换请求由导航扩展管理其打开 Token，不改变核心这一语义。

### 7.3 Context 配置与错误分类

`UIContextSettings` 集中提供加载等待、打开/关闭生命周期、进入/退出转场超时、Shutdown 总预算、缓存容量和诊断级别。创建 Context 时校验范围并复制为运行期只读快照，配置变更通过新建 Context 生效，避免处理中途修改预算。默认值为加载 30 秒、打开生命周期 10 秒、关闭生命周期 2 秒、进入/退出转场各 2 秒、Shutdown 总预算 5 秒、缓存容量 8、诊断级别 Basic；时长必须为有限正数，容量为非负数。各阶段等待取自身超时与剩余 Shutdown 预算的较小值，不能因进入下一阶段重置总预算。

这些数值是故障上限，不是最短等待或动画时长。默认无动画转场立即结束；正常关闭只等待实际钩子及动画完成，不能通过固定 Delay 消耗预算。自定义动画按注册配置时长播放，并在预算内结束。

最小错误分类须区分重复打开、加载失败、生命周期失败、转场失败、等待超时和 Context 已停止；错误附带阶段、Context/Session 身份及原始异常。调用方取消保持标准取消语义，不包装成普通失败。打开前失败由 OpenAsync UniTask 报告，已打开后的关闭错误汇入 `UICompletion` 和诊断；多项清理错误须保留全部信息。统一使用 UIException 携带 UIErrorCode（AlreadyOpen、InvalidRequest、LoadFailed、LifecycleFailed、TransitionFailed、Timeout、ContextStopped）及阶段信息；取消保留 OperationCanceledException，不为每一种关闭原因创建异常。

### 7.4 任务线程与异常通道

生命周期钩子在主线程调用；框架在 await 钩子结束后显式回到主线程再修改 Unity 状态。钩子内访问 Unity 对象前必须处于主线程，不得在 `ConfigureAwait(false)` 或后台工作后直接操作 View；业务自行切换线程时必须通过调度器显式切回。钩子入口及框架后续状态提交设置开发期主线程断言；这些断言不能检测或修复钩子内部所有越界访问。

共享完成源在主线程完成通知队列中触发；普通操作只消费一次，迟到任务观察不得再次消费底层 UniTask。公共异步入口的参数错误、主线程要求、重复打开及停止状态检查均通过 `UniTask.FromException` 返回；调用方取消返回取消操作。同步 Build/配置校验仍同步抛出错误。Task 桥接使用独立 TaskCompletionSource 时启用 RunContinuationsAsynchronously，不能假设 UniTask 完成源具备该选项。

框架由单一观察执行器消费命令与迟到任务，向注入的错误接收器上报阶段和身份，再把结果转给调用方的完成源；错误接收器失败只记录日志，不中断清理。业务仍须 await 并处理预期失败。UI 事件用显式错误处理器调用 Forget，或在 async void 处理器内部 try/catch；框架不依赖 UniTaskScheduler.UnobservedTaskException 或 GC 的未观察异常事件兜底。参见 [UniTask 异常处理](https://github.com/Cysharp/UniTask/tree/2.5.11) 与 [C# 异步返回类型](https://learn.microsoft.com/en-us/dotnet/csharp/asynchronous-programming/async-return-types)。

## 8. 层级、输入和前台状态

默认逻辑层：`Page → Overlay → Modal → Toast → System`。层级表达渲染优先级；是否接管输入由独立策略决定，Toast 默认不接管输入，System 可配置。

逻辑层与物理 Canvas 分离。默认按层配置 Canvas，避免每个界面固定一个 Canvas；高频变化部分是否拆分由性能测量决定。

Modal 默认规则：

- 最上方可交互 Modal 拥有模态输入，下层 UI 不接受输入。
- 共享遮罩位于顶层 Modal 下方，样式取顶层 Modal 配置。
- 视觉遮罩可以透明，但输入阻断独立存在。
- 下层弹窗继续显示；关闭顶层时恢复前一输入所有者与焦点。
- Overlay 和 System 默认渲染顺序不能绕过模态输入规则，需显式声明输入策略。

转场期间 Context 使用输入锁，失败路径必须释放。屏蔽涵盖指针、键盘、手柄导航、提交和返回操作，不能只禁用鼠标 Raycast。框架不负责屏蔽项目自定义的全局玩法输入，提供输入所有权事件供项目接入。

UIRoot 支持 Overlay 和 Screen Space Camera，安全区域作为可选挂载节点。World Space 和复杂多显示器布局留给后续适配。分辨率适配通过 CanvasScaler 配置完成，不改变子视图原本的锚点布局。

输入阻断由仲裁器与输入驱动共同执行：指针使用覆盖输入域的 Graphic blocker（raycastTarget=true），并将非所有者 CanvasGroup 设置为不可交互且不接收射线；导航/提交/取消仅路由给当前所有者。所有者被转场锁住时，仲裁器保存有效选择快照并清空选择，由输入驱动丢弃该期间的导航/提交/取消，锁释放后不重放旧按键。EventSystem.sendNavigationEvents 只由共享驱动管理，不能为锁住一个 Context 而误禁另一个有效所有者。

输入驱动在失去所有权时终结旧目标的 pointer press/drag 状态，不允许跨转场残留按压在解锁后触发点击；输入阻断不只依赖 CanvasGroup 的即时属性。新旧模块均验证指针、拖拽、导航、Submit/Cancel 及切换焦点。blocker 复用实例，不每帧创建/改层级，记录启停造成的 Canvas rebuild 和射线成本。

### 8.1 多 Context 的输入边界

独立 Context 不等于天然独立的物理输入域。EventSystem 持有当前选中对象和导航开关，参见 [Unity EventSystem API](https://docs.unity.cn/Packages/com.unity.ugui%402.0/api/UnityEngine.EventSystems.EventSystem.html)。首版内置单个 EventSystem 及一个 UI 输入域。旧 StandaloneInputModule 驱动在主包中；InputSystemUIInputModule 驱动在 .input-system 适配包中，二者均为首版交付与验收项，项目按实际输入后端选装。输入包不作为主包硬依赖，不同时激活两个 UI 输入模块。

多个 Context 共用该输入域时，宿主显式创建共享输入仲裁器并登记优先级，仲裁器统一决定模态所有者、返回目标与焦点；同优先级以最近获得交互的 Context 为先。Context 只提交锁和焦点意图，不能直接禁用共享 EventSystem、切换其输入模块或恢复过期选中对象。转场锁仅阻止本 Context 的交互；处于模态阻断或转场中的当前所有者仍保留屏障，不能因暂时不可交互而把输入泄漏给下层 Context。关闭或注销后按登记优先级和最近交互顺序重新仲裁，恢复焦点前验证对象有效、活跃且属于新的所有者。

转场锁按租约持有，普通模态候选变更或最近交互排序不得抢走当前所有者的焦点；锁释放后再处理这些候选。需要紧急 System 界面接管时，宿主必须显式声明更高优先级的抢占策略，由仲裁器撤销旧焦点并转移所有权，不能靠 Canvas 排序绕过。旧转场结束仅释放自己的锁，恢复焦点须再次验证所有权代次，不能抢回已转移的焦点。Root 销毁及 Shutdown 必须释放其全部锁租约。

首版不承诺两个 Context 同时拥有独立键盘/手柄焦点。分屏多人需后续接入独立输入域适配器，同时限定各域的事件路由、射线范围和焦点导航；仅创建多个普通 EventSystem 不算完成该能力。隔离验收须分别验证“Context 内部状态隔离”和“共享输入域按明确规则仲裁”，不得要求共享设施没有任何交互影响。

## 9. 注册、加载与缓存

注册表使用 ScriptableObject 保存 Prefab 或资源标识、逻辑层、实例策略、缓存策略和关闭行为。编辑器类型选择保存为稳定的可序列化类型 key，构建前生成显式类型映射与工厂引用，保留 IL2CPP 需要的泛型实例；禁止运行时扫描程序集或依靠 Type.GetType 还原注册类型。

```text
InventoryView
  Asset:        UI/Inventory
  Layer:        Modal
  Multiplicity: Single
  ClosePolicy:  CacheOne
  BackAction:   RequestClose
```

默认关闭后释放；高频单实例界面可选 CacheOne。首版多实例仅支持关闭后释放，不引入全局 View 对象池。缓存按 Context 管理，配置实例数容量并按 LRU 淘汰；容量不是 GPU/内存大小估算。提供显式清空缓存入口。

`CacheOne` 表示每类型最多缓存一个健康实例，允许 LRU 淘汰，不保证常驻。Context 容量是所有缓存实例的硬上限；容量为 0 等同禁用缓存，首版不提供 pin。关闭后是否命中缓存必须可诊断，淘汰不改变已经完成的 Session 结果。

缓存实例在接受关闭时释放 Session/Foreground，退出展示及 `OnClosed` 完成后停止残余转场并复位，再对 Shell 执行 `SetActive(false)`，将完整外壳移到 Context 专用的非激活 `cacheRoot`，从活跃层级、遮罩与输入候选中移除；与新加载使用的 `stagingRoot` 分开。非激活层级中的 Canvas 和 GraphicRaycaster 不参与正常渲染及射线处理，无需额外改写其组件 enabled 配置。重新打开先复位上次转场写集并在隐藏状态绑定参数，由 OnOpenAsync 更新业务布局，再捕获本次转场基准；执行需要帧或布局的 `OnOpenAsync` 前，按下文的隐藏准备流程激活。缓存不停止独立后台 UniTask，停止业务工作依靠 Session 作用域取消。重新挂载须保留锚点及预期布局；根被销毁或实例状态不可确认时直接淘汰。

```csharp
public interface IUIViewLoader
{
    UniTask<IUIViewLease> CreateAsync(
        UIViewAsset asset,
        Transform inactiveParent,
        CancellationToken cancellationToken);
}

public interface IUIViewLease : IDisposable
{
    UIViewBase View { get; }
}
```

加载器拥有实例与资源释放责任，Dispose 幂等且在主线程调用。框架持有租约直到实例最终淘汰，不在关闭并缓存时释放资源。核心不直接 Destroy 加载器持有的实例。

默认加载器支持主包内配置的直接 Prefab 引用和 Resources 标识；两者必须在注册中明确区分。Addressables 由扩展管理对应句柄，其依赖引用计数随租约保持。

`stagingRoot` 与 `cacheRoot` 均为非激活 GameObject。加载器必须在实例化时就指定非激活父节点，交付租约前实例始终保持 `activeInHierarchy == false`，禁止先在活跃层实例化再移入。Prefab 根必须 activeSelf 为 true、UIView 组件 enabled 为 true，子节点保留作者配置；编辑器校验根激活状态与组件。实例化的激活与 Awake 规则见 [Unity Object.Instantiate](https://docs.unity3d.com/2022.3/Documentation/ScriptReference/Object.Instantiate.html)。

首次准备顺序固定如下，缓存重开跳过加载与 OnCreate，其余会话步骤重新执行：

```text
非激活 stagingRoot 下创建 Shell/AnimationRoot → 加载 Prefab → 验证绑定
→ OnCreate → 建立 Session/参数
→ Shell 移至目标层并保持隐藏 → 激活 Shell → Awake/OnEnable
→ 会话扩展 OnPreparing → OnOpenAsync → 最终布局屏障
→ 捕获进入基准并施加动画初态
→ 解除可见性门控并播放进入动画（保持输入锁）
→ 存在前台候选时准备新 Foreground 并调用 OnForegroundEntered
→ 提交 Open 与输入资格 → 完成 OpenAsync
```

OnCreate 只依赖已序列化的引用和框架显式初始化，不依赖 Awake/Start；不能在非激活 View 上启动协程、等待 Update/布局、读取其他组件依赖 Awake 建立的内部状态，或创建会绕过非激活父节点而触发业务生命周期的活跃子实例。生成器只生成序列化引用和固定监听绑定，不生成上述工作。业务动态子实例在激活后的 OnOpenAsync 创建。Awake/OnEnable 不读取业务参数、不创建 Session；参数化展示只在 OnOpenAsync 中进行。

### 9.1 可见性门控与布局准备

每个实例使用以下包装层级，staging/cache 状态移动的是整个 Shell：

```text
目标逻辑层 RectTransform（准备时为非激活 stagingRoot）
└─ Shell：Context 创建，RectTransform + 可见性/输入 CanvasGroup
   └─ AnimationRoot：Context 创建，RectTransform + 动画 CanvasGroup
      └─ PrefabRoot：Loader 创建并由资源租约拥有，UIViewBase + RectTransform
```

Context 在调用 loader 前创建 Shell/AnimationRoot，将非激活 AnimationRoot 作为 inactiveParent 传入；loader 必须以 worldPositionStays=false 直接在该父节点下实例化，不能自行另建包装。Shell/AnimationRoot 使用 stretch anchors (0,0)–(1,1)、offsetMin/Max 为零、pivot (0.5,0.5)、单位缩放和无旋转，未播放动画时与目标层具有相同局部矩形；包装节点不参与 LayoutGroup 驱动。PrefabRoot 原有 anchors、pivot、anchoredPosition 和 sizeDelta 原样保留，等价于直接挂到目标层，不要求所有 Prefab 根统一 stretch。

缓存重开先将 Shell 挂回目标层，再执行 OnOpenAsync 和布局屏障，在实际父级尺寸下捕获新的动画基准；不把缓存根尺寸或包装偏移保存为下次基准。Context 拥有空包装节点，loader 只拥有 Prefab 和资源；释放先执行租约责任，再清理空包装，不以销毁包装代替释放租约。隔离的实例与包装一起移交独立清理所有者。

Shell 的 CanvasGroup 独立控制门控：隐藏准备时 alpha=0、interactable=false、blocksRaycasts=false；显示时释放可见性门控，交互资格仍由输入仲裁决定。转场只修改 AnimationRoot，不修改 Shell 或 PrefabRoot 业务布局。子 Canvas/CanvasGroup 不得绕过门控或输入策略；校验拒绝未登记的 overrideSorting 和 ignoreParentGroups 配置，并校验包装配置及根 RectTransform 引用完整性。

`IUILayoutReady.WaitLayoutAsync` 在激活且保持隐藏后、OnOpenAsync 完成后执行：等待下一次主线程 Update，调用 `Canvas.ForceUpdateCanvases()` 完成当前已登记的 uGUI 布局与图形更新，然后返回；紧接着捕获转场基准，中间不再 await。该屏障保证调用时已提交的布局状态，不保证未来的异步图片、字体或业务更新已完成；影响首次布局的数据必须在 OnOpenAsync 返回前准备完毕。自定义布局通过注入该协议完成自身就绪工作，仍须支持取消和打开超时。默认实现不额外叠加 ForceRebuildLayoutImmediate 与任意帧延时。参见 [Unity Canvas 更新 API](https://docs.unity3d.com/ja/current/ScriptReference/Canvas.ForceUpdateCanvases.html)。

## 10. 扩展契约与边界

### 展示与绑定

Presenter 是普通 C# 对象，由 `IPresenterFactory` 创建。每个 Session 创建一个 Presenter，打开回滚或关闭时解绑并释放。DI 容器通过工厂适配，View 不通过全局 ServiceLocator 获取业务服务。

View 可以直接展示普通参数，也可绑定外部 Model。外部传入 Model 默认由调用方拥有；框架不擅自 Dispose。Presenter 创建并拥有的 Model 则由 Presenter 释放。

### 导航

```csharp
await navigator.PushAsync<HomeView>(ct);
await navigator.PushAsync<SettingsView>(ct);
await navigator.BackAsync(ct);
```

页面描述与历史属于 Navigator；临时 Modal 不进入页面历史。新页面成功准备后才提交历史，失败或取消保持原页面可用。切换期间原页面暂时失去交互，提交后关闭旧页面并保存其类型 key 与不可变参数，后退通过保存描述重建。该事务需要核心内部的 prepare/commit/rollback 支持，不能简单拼接两次公开 Open/Close 实现。核心首版只做单界面转场；导航扩展使用新增的 INavigationTransition 协议承载 from/to 和事务信息，不改写单界面 IUITransition。导航事务可在内部并行播放一对页面动画，但始终独占 Context 命令调度权；共享元素转场不在交付范围。

导航扩展通过核心的事务能力入口执行整个切换，持有 Context 调度权至 commit 或 rollback，普通 Open/Close 不能插入事务中间；Shutdown 仍可从队列外停止事务。内部步骤不得重新排队并等待自己的 Context 命令。历史变更与页面可见性、输入所有权一同提交，失败时恢复原页面及有效焦点。

事务步骤明确指定转场所有者 CoreDefault 或 External。普通 OpenAsync/CloseAsync 固定使用 CoreDefault；导航 prepare/close 步骤使用 External，不执行单界面 IUITransition 的 EnterAsync/ExitAsync，由 INavigationTransition 唯一负责成对动画。抑制动画不跳过生命周期、布局、门控、作用域或完成屏障；事务仍可调用核心隐藏/复位及紧急清理能力。回滚在重新显示旧页面前停止成对动画并复位双方，不再补播一套核心默认动画。

导航请求串行 FIFO，每个请求返回独立完成任务。A→B 准备期间收到 C，在 B 事务结束后执行，不隐式取消 B。业务可在提交前去重或防抖，导航扩展不交付 ReplacePending/LatestWins 或请求抢占。

导航扩展首次交付的历史恢复限进程内、同一 Context，不承诺跨进程参数序列化。缓存被淘汰时可根据保存参数重建页面，调用方应传入适合恢复的不可变参数。

历史只保存页面描述，不保留活跃页面实例或自动保存滚动位置、展开状态、输入内容；CacheOne 命中也不构成状态保留承诺。需要恢复的展示状态由项目在页面关闭前保存到外部 Model/状态存储，并在恢复打开时通过不可变参数快照或业务接口重新应用；外部状态由项目拥有，核心不增加 Retain 或绕过 LRU 的常驻策略。

### 对话框

```csharp
DialogResult<bool> result = await dialogs.ShowAsync(
    new ConfirmRequest("删除存档", "确定删除这个存档吗？"), ct);
```

区分提交结果、用户取消和 Context 退出。ShowAsync 的 Token 覆盖整个对话等待期，打开完成后取消也会请求结束该对话；这是对话扩展的契约，与核心 OpenAsync 只控制打开阶段明确区分。

### 自动弹窗

```csharp
popupQueue.Enqueue(
    new RewardPopupRequest(rewards),
    priority: 100,
    deduplicationKey: rewardBatchId);
```

队列实例按 Context 隔离，等待 Session.Closed，不等待 GameObject 销毁。优先级高者先出，同优先级 FIFO，不抢占正在显示的弹窗。去重覆盖等待和正在显示的请求，结束后释放 key；暂停只阻止下一条开始。失败记录并继续，Shutdown 清空队列且取消当前请求。

队列暴露显式暂停能力，`AcquirePause()` 返回可释放租约，多个剧情/系统各自释放后才恢复出队，避免单个 `Resume()` 提前解除其他来源的暂停。暂停不隐藏、不关闭当前 Session，也不取消其已开始的打开请求。应用切后台是否获取暂停租约由宿主决定。

剧情遮挡当前弹窗优先用显式输入策略的覆盖界面完成：原弹窗保持 Session，失去 Foreground 后停止前台行为，覆盖关闭后恢复新的 Foreground。真正的挂起/隐藏/压栈恢复涉及业务计时、结果回传及显示状态，留给后续独立展示策略；不能用 Close 后重开冒充恢复，也不把这一能力加入首版核心或默认队列。队列作为 Context 的可注销扩展服务接收停止通知，Shutdown 时即使队列暂停也必须终结等待请求，并释放全部去重 key。

## 11. 编辑器体验

主包提供最少但完整的工具：

1. 创建 UIRoot 与注册表。
2. 从 Prefab 创建 View 脚本与组件引用文件。
3. 绑定标记保存序列化引用，生成 `.Bindings.cs`，不依赖运行时按名称查找。
4. 校验重复类型、缺失 Prefab、缺失组件、字段冲突、跨 Prefab 非法引用和层级配置。
5. 构建前校验注册映射与绑定产物，缺失或过期时阻止构建并给出资源定位。

生成器只覆盖生成文件，业务文件首次创建后不覆盖。不强制对象命名规则；绑定通过显式标记或序列化组件完成。重命名和 Prefab Variant 的处理必须有测试覆盖。

### 11.1 生成路线与构建顺序

首版采用 Editor 显式生成 `.Bindings.cs`、注册映射与序列化引用的路线。产物写入项目配置的生成目录，随项目提交；不修改只读 UPM 缓存。脚本创建、编译/Domain Reload、组件挂载和 Prefab 引用写回分步执行，工具须能在重载后继续，并避免 AssetPostprocessor 递归触发重编译。重命名依赖稳定资源标识及序列化引用，不仅依赖对象路径字符串。

首版不引入 Source Generator。Prefab 扫描、组件挂载、绑定及注册映射全部通过 Editor 显式生成，避免并存两套生成链路。

完整顺序为“生成或更新 → 等待脚本编译及引用写回 → 校验 → 构建”。生成产物带输入指纹或等效过期检测；普通构建预处理只校验，不在构建回调里首次生成 C# 后假设立即参与本轮编译。CI 可使用分阶段 Editor 调用完成生成和构建；显式类型映射与绑定在目标 IL2CPP 平台验证裁剪后的实际行为。

提供可重复执行的 Editor 菜单及命令行流水线入口，允许跨编译重载或分进程分阶段继续，不要求一个同步方法跨越全部编译。CI 强制验证干净检出的生成、编译、校验和构建，失败返回非零状态并定位输入资源；再次生成不产生额外内容差异。

开发模式提供 Context、Session、资源租约、输入所有者和缓存的只读诊断信息。正式构建允许关闭高成本诊断。

首版交付只读诊断快照及可配置采集级别，不交付 Runtime Debugger 窗口。关闭诊断后不保留高成本历史采样，停止失败等必要错误报告仍按项目日志策略处理。UIRoot 的相机配置仅供 Camera 模式使用，Overlay 不要求相机。

加载、布局、打开/关闭钩子、转场和缓存操作提供独立 ProfilerMarker，并记录命令与 Session 身份。异步阶段分别记录经过时间和同步执行片段，不让一个 ProfilerMarker 跨 await 悬挂。GC 与 Canvas rebuild 通过隔离测试场景的 Unity Profiler/ProfilerRecorder 采样，不使用主线程分配计数差值冒充一个异步命令的独占分配；采样结果标注平台与环境。

运行期注册、队列、缓存和输入所有权均由 Context 或宿主实例持有。必须使用的静态桥接在 SubsystemRegistration 重置，并在退出 PlayMode 时注销事件与终结任务；Editor 工具另行清理其订阅。域重载及场景重载开/关组合下连续进入退出 PlayMode 不得残留旧 Session、锁或迟到清理宿主。

## 12. 交付阶段和验收

### 第一阶段：核心闭环

包含主包、Input System 适配包、默认加载、层级与遮罩、生命周期作用域、取消和异常清理、单实例/多实例、CacheOne、注册校验与基础绑定生成，以及设置页面、叠加弹窗、带参数背包三个示例。背包示例使用普通 ScrollRect，不把虚拟列表纳入核心。

| 子阶段 | 交付与退出条件 |
| --- | --- |
| 1a 入口：初始化验证 | 在两个编辑器的最小场景验证非激活实例化、OnCreate、隐藏激活及 Awake/OnEnable 顺序；记录日志与断言结果，通过后才编写依赖该顺序的生产生命周期代码 |
| 1a：执行与会话 | 核心接口、Builder、调度器、生命周期和 Single/Multi；用测试替身通过 FIFO、取消、重入、共享完成源及完成屏障测试 |
| 1b：加载与回收 | 默认加载、租约、非激活准备、CacheOne/LRU 和迟到清理；通过失败、根销毁及有界 Shutdown 的资源计数验收 |
| 1c 入口：输入验证 | 用两个指定编辑器和 Input System 1.17.0 验证双驱动的导航/提交/取消拦截、press/drag 清理与焦点恢复；通过后才编写完整模态仲裁逻辑 |
| 1c：展示与输入 | 布局屏障、转场停止/复位、遮罩与仲裁、新旧输入驱动；通过暂停时间、转场中拖拽、焦点及提交/返回阻断测试 |
| 1d：工具与接入 | Editor 生成、三个示例、文档、诊断采样与 CI；两个编辑器的干净构建及平台冒烟通过 |

交付包包含 XML 公共接口注释、Documentation~ 中的从零接入/生命周期/取消与错误处理说明、QuickStart、CHANGELOG.md 及迁移说明。CI 配置放在 .github/workflows，使用有 Unity 许可的 macOS 自托管执行器运行指定编辑器矩阵；每个矩阵项独立工作目录，不能让两个 Unity 版本打开同一份项目缓存。生成、编译、EditMode/PlayMode、Player 构建和运行结果均归档，失败返回非零状态。

入口验证使用独立最小场景与测试替身，不要求先实现完整框架。输入驱动通过输入模块公开/受保护接口和框架节点门控实现，不复制模块私有状态机、不反射改写私有字段。初始化或输入验证不通过时停止对应阶段，提交实测证据并在对话确认契约变更；不得用降级输入支持或改生命周期顺序绕过关卡。入口验证是必须执行的工程步骤，本文不把其结果标为已通过。

### 第二阶段：业务流程扩展

按项目需要实现 Navigation、Dialogs、Popup Queue 和 Presentation；先验证事务与所有权，再扩展便利 API。

### 第三阶段：生态适配

按真实项目需求增加 Addressables、R3、DOTween 与 Collections。先固定扩展点再分别验证适配，不同时实现所有后端。

### 必须验证的行为

| 用例组 ID | 设计条款 | 类别 | 验收重点 |
| --- | --- | --- | --- |
| LIF-01 | 6 | 生命周期 | 每实例创建一次，每 Session 终结一次；按第 6 节验证各阶段失败的钩子配对及清理幂等 |
| CAC-01 | 9 | 缓存 | 非激活缓存不渲染/接收输入；转场属性复位、业务布局按 OnOpenAsync 更新，不重复监听；旧 Session 不能影响新 Session；容量 0 与 LRU 淘汰符合上限 |
| CAN-01 | 7.1 | 取消 | 排队、加载、打开各阶段可取消；忽略取消的加载不会阻塞后续命令，迟到租约在主线程仅释放一次 |
| CON-01 | 3、7 | 并发 | 重复打开符合策略；重复关闭共享完成任务；嵌套调用快速失败 |
| CON-02 | 7 | FIFO | 已排队关闭先执行后同类型打开成功；前一打开失败不阻止后一请求重试 |
| INP-01 | 8、8.1 | 输入 | 模态覆盖、键盘/手柄焦点和转场输入锁正确恢复；按声明支持的输入模块验证 |
| INP-02 | 8、8.1 | 输入移交 | press/drag 中失去所有权不会在解锁后触发旧点击，锁期间 Submit/Cancel 不重放；重复默认仲裁器 Build 失败 |
| TRN-01 | 7.2 | 转场 | 进入/退出在不同进度取消、超时、抛错均隐藏并复位；无法停止的实例不缓存 |
| TRN-02 | 7.2 | 停止收敛 | Stop 返回前结束写入并取消活跃操作，不等待两秒上限；已结束结果不被改写，复位幂等 |
| ERR-01 | 7.3、7.4 | 异常 | 钩子、动画和 Dispose 异常不阻止剩余清理 |
| LATE-01 | 3、7.1 | 迟到任务 | 加载、打开钩子、关闭钩子和转场迟到抛错均被观察；隔离租约在任务结束后仅释放一次 |
| LAY-01 | 9、9.1 | 初始化与布局 | 非激活实例化的 Awake 顺序、隐藏激活、OnOpenAsync 后布局刷新及动画属性写集在支持版本上分别验证 |
| PLAY-01 | 11.1 | Editor 运行期 | Domain Reload/Scene Reload 开关组合连续进入退出两次 PlayMode，不残留会话、输入锁或旧订阅 |
| SHUT-01 | 5、7.1 | 退出 | 活跃界面、缓存和扩展服务停止；永不完成的异步加载不阻塞 Shutdown；迟到结果在 Root 销毁后仍能清理；总预算与未决诊断可验证 |
| ISO-01 | 4、8.1 | 隔离 | 两个 Context 的层级、缓存、身份和队列隔离；共享输入域不会被另一 Context 直接禁用或错误夺取焦点 |
| CLOSE-01 | 3、5 | 完成屏障 | `Closed` 后立即重开同类型成功；队列下一条启动前上一条清理和输入仲裁已完成 |
| CLOSE-02 | 5、7 | 原因与确认 | 多次关闭保留首次原因、Shutdown 覆盖未完成原因；CanClose 否决、异步确认去重、旧 Session 结束后放弃确认结果 |
| BATCH-01 | 5 | 批量快照 | 快照之后排队的打开不属于批量关闭；逐项错误均可观察；按类型精确匹配且拒绝基类类型 |
| FORE-01 | 6 | 前台作用域 | 失去前台取消 Token 并释放订阅，恢复时新建作用域；首次/恢复通知失败及关闭中退出通知失败符合各自终结规则 |
| EXT-01 | 5.1、6.1 | 会话扩展 | 创建/绑定在 Awake 后且 OnOpen 前；Preparing 失败回滚已交付绑定，关闭/打开回滚仅解绑一次，终结回调错误不阻断完成 |
| SHELL-01 | 9、9.1 | 包装布局 | 非 stretch 根与特殊 pivot 包装后布局不变；跨分辨率缓存重开重新捕获基准；包装销毁不代替释放租约 |
| TXN-01 | 3.1、10 | 转场所有权 | 核心事务替身使用 External 不执行默认 Enter/Exit，生命周期及门控仍各一次；回滚停止外部动画并复位 |
| DIAG-01 | 5、5.1 | 只读接口 | 快照不能修改运行状态，关闭采集后基础状态仍可读；服务查询、重复注册和工厂失败清理符合所有权 |
| GEN-01 | 11、11.1 | 编辑器 | 生成幂等、不覆盖业务文件；重命名和 Variant 引用稳定；干净检出生成/编译/校验/构建闭环，过期产物阻止构建 |
| PLAT-01 | 3.2、12 | 平台 | Unity 2022.3.62f2 和 6000.3.22f1 通过编译及 PlayMode；两版本均完成 Android ARM64 与 iOS ARM64 的 IL2CPP 构建和启动冒烟 |
| API-01 | 5、5.1 | API 与构建 | 无参入口对带参 View 编译失败；类型擦除参数错误在加载前失败；默认注入、共享服务所有权与无效配置校验 |
| CFG-01 | 7.3 | 配置 | 配置快照不随源配置改变；默认值、非法范围、剩余 Shutdown 预算计算与诊断一致 |
| PKG-01 | 3.2 | 打包 | Git 固定版本和干净依赖解析；Unity 6 未声明/已锁定 uGUI 及旧 UniTask 接入校验；样例无 TMP；Core 无可选后端或 Editor 引用，测试程序集不进入正式构建 |
| PERF-01 | 11.1、12 | 性能 | 标记覆盖加载/布局/钩子/转场/缓存；50 次基线及资源计数无增长，记录采样条件 |

性能验证使用代表性页面，记录首次打开、缓存重开、Canvas rebuild、GC 分配和关闭后资源变化。具体预算由目标项目决定，不在没有测量时承诺零 GC 或固定帧耗时。

平台矩阵覆盖两个编辑器 × Android/iOS ARM64 × 旧输入/Input System 驱动。移动端冒烟包含直接引用与 Resources 打开、带参类型擦除入口、缓存重开、触摸关闭和 Shutdown，验证裁剪后的生成映射与异步路径。Android 构建后安装启动，iOS 导出 Xcode、编译并在真机启动；执行器配置对应 SDK、签名凭据和设备，测试结果与构建日志归档。未交付导航等扩展不计入首版平台构建。

执行分为每次变更的两编辑器编译与 EditMode/PlayMode、合入前的 Android 构建冒烟、发布前的完整移动端矩阵三个关卡；iOS 真机测试允许人工触发并归档证据，但不是免验项。阶段一发布须通过完整矩阵，不能因执行设备暂缺而把已确认的兼容范围标为完成。

首阶段优先自动化连续并发打开/取消/关闭与 Shutdown 打断、各阶段异常后的租约/实例计数、多 Context 焦点及转场锁仲裁三组场景。使用可控加载器和转场替身精确推进竞态，不以不稳定的固定延时碰运气；纯调度与所有权测试放 EditMode，涉及激活、布局和 EventSystem 的测试放 PlayMode。

性能基线至少包含代表性 Modal 连续打开/关闭 50 次，分别测量首次加载、预热后的释放重建和 CacheOne 重开，固定页面参数、平台、Editor/Player 模式及诊断配置。目标是减少缓存重开的资源创建与分配；不预设 GC 必然显著低于首次打开的硬阈值，若收益不足应定位测量原因。关闭及延迟销毁稳定后，Release 应无该类型存活实例/租约，CacheOne 最多保留一个（且仍受缓存容量约束），不得随循环次数增长；测试记录待清理的迟到任务，不能用移出活跃索引冒充资源已释放。

第二阶段扩展另验收：导航连续请求 FIFO、失败回滚、Shutdown 中断事务及普通命令不能插入；队列多来源暂停、暂停中 Shutdown、等待/显示请求去重 key 释放；对话 Token 在打开后取消能结束等待。UniTask 与新旧输入模块均纳入首版兼容矩阵；Source Generator 不交付，不列为可选的首版验收路径。

## 13. 实施边界与变更规则

本方案规定核心接口形态、生命周期与所有权、默认策略、兼容矩阵及验收行为。实施计划只安排实现顺序、文件拆分和验证步骤；内部集合结构、私有类型拆分和 Editor 窗口布局属于实现细节，不改变上述契约。公开契约或支持范围如需调整，先在对话中确认，再修订本方案，不把未决选项或 Review 处理记录写入方案。

本次文档修订不包含实现代码、包安装、项目升级或发布操作。设计验收条款是后续实现的通过条件，不代表目前已通过运行期或目标平台测试。

## 14. 外部参考

以下为 2026-10-31 讨论中查阅的公开资料。上游版本可能变化，实施时应固定依赖版本，不把参考项目作为默认运行时依赖。

- [QFramework 仓库](https://github.com/liangxiegame/QFramework)
- [QFramework UIKit 文档](https://github.com/liangxiegame/QFramework/wiki/当前版本)
- [UnityScreenNavigator](https://github.com/Haruma-K/UnityScreenNavigator)：导航、生命周期与转场；讨论中查阅的主线要求 Unity 6，不能直接假设兼容 2022.3。
- [Loxodon Framework](https://github.com/vovgou/loxodon-framework)：MVVM 与数据绑定。
- [Unity UI Extensions](https://unity-ui-extensions.github.io/ugui/)：uGUI 控件扩展参考。
- [Unity UI 优化建议](https://create.unity3d.com/Unity-UI-optimization-tips)：Canvas 划分与 Raycaster 成本。
- [Addressables ReleaseInstance](https://docs.unity.cn/Packages/com.unity.addressables%402.9/api/UnityEngine.AddressableAssets.Addressables.ReleaseInstance.html)：创建方式与实例释放契约。
- [UniTask 2.5.11](https://github.com/Cysharp/UniTask/tree/2.5.11)：核心异步依赖、PlayerLoop、取消与多方等待契约。
