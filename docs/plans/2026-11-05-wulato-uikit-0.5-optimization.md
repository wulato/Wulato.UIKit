# Wulato.UIKit 0.5 架构审查与分阶段优化计划

**状态：2026-11-05 PR #1/#2/#3/#4/#5 均已按用户确认合并，main 为受保护默认分支；任务 1.2 已合并，Phase 1 已合并，任务 2.1 实现与专项验证完成，PR #6 等待用户审核合并；2.2 尚未开始。**

**目标：** 交付一个以 uGUI 为基础、业务入口统一、生命周期可靠、只需安装一个 UIKit UPM Package 的内部框架。

**执行方式：** DEEP；当前 Agent 串行实施，每项任务独立分支、专项验收、提交及 PR，用户确认合并后继续。保留历史计划和证据，不默认启动子代理。

**依据：** 本轮用户批准的 0.5 架构与行为决策以本文件为准；原始设计 `../superpowers/specs/2026-10-31-wulato-uikit-design.md` 中未被本计划明确替代的运行时正确性契约继续有效。`../superpowers/plans/2026-11-03-wulato-uikit-master-optimization.md` 作为历史成果及证据入口保留，不覆盖其内容。验证使用 `../../Verification/README.md` 和现有执行器。

## A. 当前基线与架构审查

### A1. Git 与验证状态

2026-11-05 规划和 Phase 0 启动时均已只读核对：

| 项目 | 实际状态 |
| --- | --- |
| 当前原工作区分支 | `codex/p0-p1-preflight` |
| 已提交基线 | `a117100ee4a1ad95de410ae3eb3a70b284c11e75`，与远端同名分支一致 |
| 远端 | `github`，`git@github.com:wulato/Wulato.UIKit.git` |
| 默认分支 | `master`，`e041e0391bb3f4a560170bdb9562df3ea570abed`，初始提交；启动时不存在 main |
| 差异 | 当前分支领先 master 22 个提交，涉及 2079 个文件 |
| 开放 PR | 启动时没有 |
| Worktree | 规划时只有原工作区；Phase 0 使用一个干净 worktree 保留脏目录 |
| Unity | `2022.3.62f2` |
| 包版本 | 主包/Input System 0.4.0，Addressables 0.2.0 |

未提交内容必须保留且不纳入稳定基线：根 manifest/lock（包括 uGUI 从 1.0.0 改为 2.0.0）、ProjectSettings（EVO_UI）、四个验证工具/测试文件、旧计划执行记录、未跟踪 Evo UI/TMP 资产和元数据。用户已选择仅导入已提交内容。这些修改不得被覆盖、自动提交或当作 a117100 的验证输入。

现有验证体系包含 suite 选择、隔离宿主、源码指纹、必需用例、原始日志和证据校验，应复用。历史 M6 安装补验不等于移动发布通过。旧计划末尾的真实专项补验仍记为进行中；类别筛选问题的自动修复计数为 **1**，移交不能重置。原工作区未提交修复不自动引入本基线；若基线验收复现相同问题，先核对已有修复并记录独立任务，不重复无证据修复。

规划轮仅执行只读 Git/远端查询、源码与测试结构检查、文档和证据索引核对、官方资料研究及 git diff --check（通过）；未运行 Unity/Python 测试、性能测试、Player 构建或安装第三方包。后续实际验证另记执行记录，不沿用规划轮结论。

### A2. 模块盘点

以下路径相对 `Packages/com.wulato.uikit/`；独立适配包另标明。审查覆盖所有 UIKit 模块的源码结构、关键执行链、测试目录与必需用例配置；架构静态审查不构成全部运行时行为已通过的证明。

主包 Runtime：80 个 C# 文件、4368 行；Editor：22 个文件、1537 行。重构依据是职责和接入成本，不是行数。

| 模块、源码位置 | 已实现职责/依赖 | 维护成本与问题 | 0.5 处理与保留能力 |
| --- | --- | --- | --- |
| Core：Runtime/Core/ | 注册快照、参数、错误、Session、缓存、生命周期；UnityEngine/UniTask | 中高；复杂度主要来自正确性 | 保留逻辑边界，隐藏内部协议 |
| Engine：Core/Sessions/UIContextEngine.cs、Transactions partial | FIFO、开关、提交回滚、Shutdown、输入协调 | 高；已有 partial 分工 | 保留唯一内核，区分内部原始打开与公共导航 |
| Facade/Builder：UGUI/UIContextFacade.cs、UIContextBuilder.cs | Root 所有权、装配、失败回滚、转发 | 中；显式模块装配增加步骤 | 保留装配所有权，业务服务默认装配 |
| Host：UGUI/UIContextHost.cs | Inspector 启动、幂等初始化、有界停止、Reload | 中高；不同于 RuntimeHost | 保留场景装配，不吞并迟到清理宿主 |
| Session/Scope：Core/Sessions/、Scopes/ | 每次打开身份、共享 Closed、资源跟踪、前台作用域 | 中；不同 Scope 表示不同寿命 | 保留 Session/Foreground 与逆序清理 |
| Queue/Transaction：Core/Async/、Transactions/ | 单次消费、通知隔离、取消竞争、原子替换 | 高；部分公开协议用于跨程序集 | 协议转 internal，保留 FIFO/提交门/单次观察 |
| Registry/Loading：Core/UIRegistry.cs、Runtime/Loading/ | 类型映射、Prefab/Resources、租约 | 中；默认与 Addressables 回退分发重复 | 内部统一路由，保留显式 key/类型校验/租约 |
| Navigation：Runtime/Navigation/ | Push/Back、描述符历史、配对转场、事务发布 | 中；缺 Replace/PopTo，输入 Back 分离 | 扩展现有 Navigator，不保存旧 View |
| Dialogs：Runtime/Dialogs/ | 请求映射、Session responder、结果与 Closed 屏障 | 中；额外 Registry/装配样板 | 统一注册来源，保留结果/取消/关闭错误区分 |
| PopupQueue：Runtime/PopupQueue/ | 优先级、FIFO、去重、暂停、不抢占、停止 | 中；入口底层、缺单请求取消 | 泛型入口和取消复用现有队列 |
| Presentation：Runtime/Presentation/ | 每 Session Presenter、失败解绑、外部 Model 所有权 | 低，约 56 行 | 保留可选，不强制业务架构 |
| Input：UGUI/Input/、独立 input-system 包 | 单域仲裁、Modal 屏障、按住隔离、焦点、事件清理 | 高；遍历/分配需实测 | 保留安全链路，隐藏内部跟踪器，不仅切 CanvasGroup |
| Transition：Core/Transitions/、UGUI/Transitions/ | 写集、基线、同步 Stop、自研帧执行 | 中；现有协议可承载 Tween | 保留协议/Immediate，DOTween 替代通用执行器 |
| 独立 addressables 包 Runtime/ | 唯一位置、非激活实例化、租约、销毁后释放 | 中高；已有所有权测试 | 迁主包可选程序集，保留取消与释放时序 |
| Editor/Authoring/、Binding/、Registry/ | Workbench、生成/路由/指纹/Undo/续跑/预览恢复 | 中高；安全保护不可直接删除 | 扩展已有工具，不重写编辑器 |
| Tools/Verification/、Verification/ | 选择、隔离、矩阵、证据、构建与设备 | 高；包/程序集迁移影响映射 | 随任务更新，远端/移动门禁最后处理 |

### A3. 结论与优先级

- **P0 安装/装配：** 主包已有七个 Runtime 程序集、四业务模块；用户选择收敛基础 Runtime，减少跨程序集公共协议。
- **P0 资源来源：** Addressables 手工注入 Loader 改为 Registry 来源驱动。
- **P1 导航/返回：** 扩展已有事务和 Navigator，不增加第二套生命周期。
- **P1 可选编译：** Runtime/Editor/Tests/Samples 分别隔离，不能只给源码加 #if。
- **P2 体验/生态：** 契约稳定后完善模板、动画、绑定示例。
- **性能观察：** Gate 遍历、输入驱动数组、仲裁 LINQ、类型查找需测量；无已确认瓶颈或零 GC 承诺。

## B. 技术选型与推荐结论

第三方兼容性须用锁定版本实际验证；本表是工程选择，不是安装或验收结果。

| 能力 | 当前/候选 | 推荐 |
| --- | --- | --- |
| 生命周期/取消/所有权 | 现有 Engine、整体替换、重新自研 | 保留并收敛已有内核，避免重做复杂竞态正确性 |
| 简单 API | Context、QFramework.UIKit | 借鉴直观入口，不集成整套 QFramework；返回 Session，不暴露缓存 View |
| 导航 | 当前 Navigator、UnityScreenNavigator | 改进当前实现，参考页面流/配对动画，不叠加两套屏幕生命周期 |
| Transition | 当前 Runner、DOTween/Pro | 集成可选 DOTween，候选 1.3.045；Pro 非必需，0.5 暂无独立 Pro 播放适配 |
| 绑定 | 手写事件、R3、自研 | 可选 R3 示例，session.Track(subscription)，不自研 Observable/路径绑定 |
| 复杂列表 | ScrollRect、FancyScrollView、自研 | 默认 ScrollRect；候选 FancyScrollView 1.9.0 提供示例，不做统一列表引擎 |
| 控件 | uGUI、UI Extensions、Evo | 按需直接使用成熟控件；UI Extensions 仅指南/兼容检查，不加核心依赖 |
| Theme | 自研、Evo Styler | 不自研；本地 Evo 1.9.9 项目侧评估，官方 v2 最低 Unity 6 且不能直接从 v1 升级，本轮不升级 |
| Localization | 自研字典、Unity Localization | 优先官方组件，可选示例；Session 约束手工异步更新，不建语言包系统 |
| Focus/Gamepad | Gate、Selectable、Evo | 保留 Gate，复用 Selectable/Input System，先做默认焦点/恢复示例 |
| 渲染/编辑 | uGUI、FairyGUI、UI Toolkit | 继续 uGUI；不迁移 FairyGUI 资产/渲染，不加第二 Runtime 后端，不重写 Workbench |

整体方案：全部保留边界迁移风险低但接入负担不变；整体替换无法证明更省维护；**用户已选择基础 Runtime 收敛、可选适配隔离**，接受基础运行时统一重编译。

官方参考（2026-11-05 已查阅）：

- [QFramework UIKit 源码](https://raw.githubusercontent.com/liangxiegame/QFramework/master/QFramework.Unity2018%2B/Assets/QFramework/Toolkits/UIKit/Scripts/UIKit.cs)
- [UnityScreenNavigator](https://github.com/Haruma-K/UnityScreenNavigator)
- [DOTween 下载/版本](https://dotween.demigiant.com/download.php)、[API](https://dotween.demigiant.com/documentation.php)
- [R3 Unity 安装](https://github.com/Cysharp/R3#unity)：NuGet DLL 与 R3.Unity 两步，不能声称只加一个 Git 包即可
- [FancyScrollView](https://github.com/setchi/FancyScrollView)：FancyScrollView 与 FancyScrollRect/Grid 的循环/吸附能力不同
- [Unity UI Extensions](https://github.com/Unity-UI-Extensions/com.unity.uiextensions)
- [Evo v2 变化](https://docs.michsky.com/docs/evo-ui/new-in-v2)
- [Unity Localization 1.4](https://docs.unity3d.com/Packages/com.unity.localization@1.4/manual/QuickStartGuideWithVariants.html)
- [FairyGUI](https://github.com/fairygui/FairyGUI-unity)、[Unity 2022.3 UI 对比](https://docs.unity3d.com/2022.3/Documentation/Manual/UI-system-compare.html)
- [Unity asmdef 格式](https://docs.unity3d.com/2022.3/Documentation/Manual/AssemblyDefinitionFileFormat.html)
- [Addressables 加载所有权](https://docs.unity3d.com/Packages/com.unity.addressables@1.21/manual/LoadingAddressableAssets.html)

## C. 目标架构与公共契约

### C1. 单 Package 与程序集

唯一 UIKit Package 为 `com.wulato.uikit`。必需依赖仍为 uGUI/UniTask 2.5.11。用户允许外部必需依赖；一个 UIKit 包不代表自动安装所有 SDK。文档明确 UniTask 的 Git/内部源解析方式。

```text
Packages/com.wulato.uikit/
  package.json
  Runtime/
    Wulato.UIKit.asmdef
    Core/ Loading/ UGUI/ Navigation/ Dialogs/ PopupQueue/ Presentation/
    Integrations/
      Addressables/    # 独立 asmdef
      InputSystem/    # 独立 asmdef
      DOTween/        # 独立 asmdef
  Editor/
    Wulato.UIKit.Editor.asmdef
    Authoring/ Binding/ Registry/ Diagnostics/
    Integrations/     # SDK 专属校验，独立 asmdef
  Tests/              # 保留行为 suite
  Samples~/           # QuickStart、BusinessFlow、Addressables、DOTween、R3、FancyScrollView
  Documentation~/
```

```mermaid
flowchart LR
  Runtime[Wulato.UIKit] --> UniTask
  Runtime --> UGUI[UnityEngine.UI]
  Editor[Wulato.UIKit.Editor] --> Runtime
  Addressables[UIKit.Addressables] --> Runtime
  Addressables --> AddressSDK[Unity.Addressables / ResourceManager]
  Input[UIKit.InputSystem] --> Runtime
  Input --> InputSDK[Unity.InputSystem]
  Tween[UIKit.DOTween] --> Runtime
  Tween --> DOTween
  OptionalEditor[可选 Editor 集成] --> Editor
  OptionalEditor --> Addressables
```

- 基础 Runtime 不引用可选 SDK 或 UnityEditor。删除原七个基础 asmdef，保留目录边界；事务/Shell/调度/输入跟踪实现转 internal，不提供空壳兼容程序集。
- SDK 引用使用程序集名称，不依赖消费项目 GUID。
- Addressables/Input System 的 Runtime、Editor、Tests 各自声明 Version Defines/Constraints；符号不跨 asmdef 自动传播。
- 首批锁定 Addressables 1.21.21、Input System 1.17.0；未验证版本明确提示，不自动升级 SDK。
- DOTween 使用显式集成符号；先核对 SDK/官方程序集配置，不能仅凭全局 DOTWEEN 符号假定可引用。
- SDK 缺席时相应适配不编译；基础 Editor/默认样例无适配类型引用。条件声明不替代真实干净编译。

### C2. 核心对象关系

```mermaid
classDiagram
  UIContextHost --> IUIContext : 创建并持有
  UIContextFacade ..|> IUIContext
  UIContextBuilder --> UIContextFacade : 装配
  UIContextFacade --> UIContextEngine : 原始命令
  UIContextFacade --> UINavigator : Page 操作
  UIContextFacade --> UIDialogs
  UIContextFacade --> UIPopupQueue
  UINavigator --> UIContextEngine : 同一事务队列
  UIContextEngine --> UISessionHandle
  UISessionHandle --> UISessionScope
  UIContextEngine --> UIViewCache
  UIContextEngine --> UIResourceResolver
  UIResourceResolver --> IUIViewLoader
  IUIViewLoader --> IUIViewLease
  UIContextEngine --> UIInputArbiter
  UIContextEngine --> UIOrphanRegistry
```

Facade 是唯一公共业务入口，Engine 不再实现公共 IUIContext。Host 管装配，Engine 管逻辑生命周期，RuntimeHost 管 Root 销毁后的通知/迟到清理。Navigation/Dialogs/PopupQueue 默认装配，Presenter 显式可选。保留 Loader/Transition/Observer 扩展点；低层事务不继续作为公共通用协议。不新增 DI 容器、自动扫描或全局 Context。

### C3. 生命周期与资源流程

```text
请求 → 主线程/参数校验 → Context FIFO
 → 加载/取缓存 → 新 Session
 → OnCreate（仅新实例，保留先于 Awake 的契约）
 → 隐藏挂载 → Presenter/Observer → OnOpen
 → Layout → Enter/配对动画 → 原子提交页面/历史/输入 → 返回 Session

关闭 → 撤销输入/Foreground → 取消 Session Token
 → 终结观察者/释放 Session 绑定 → OnClose → Exit → OnClosed
 → 缓存或释放实例/Lease → Closed
```

保留：原始异步任务单次消费，共享 Closed/关闭/Shutdown 完成源；缓存重开新 Session；旧 responder/句柄/订阅不影响新会话；提交前取消回滚，提交后旧页有界关闭，Shutdown 可接管；不合作任务迟到隔离；PendingCleanup 不冒充物理释放；Shutdown 统一绝对预算；停止诊断不可变；外部 Model/SDK/注入依赖不转移所有权。

```text
Registry addressables → Build 校验后端 → Resolver
 → Locate 唯一地址/GUID → LoadAssetAsync
 → 非激活父节点实例化 → Lease 拥有克隆/句柄
 → 缓存保留 → 实际销毁克隆后 Release
```

复用现有查询释放、失败回滚与迟到清理，不混换实例化/释放体系。内部 UIResourceResolver 复用 IUIViewLoader，集中 Prefab/Resources/External 分发。第一方适配受控初始化注册工厂，每 Context 取得独立快照；不扫描、不共享 View/Lease。运行时标准启动阶段注册，Editor 由适配 Editor 初始化；幂等并支持 Reload。缺失后端在分配前报错。

### C4. 公共 API 与导航

保留 Async 命名和强类型参数。`UIContextHost.UI` 为主入口；Initialize 仍返回 IUIContext。

```csharp
var ui = host.Initialize();
var home = await ui.OpenAsync<HomeView>();
await ui.PushAsync<InventoryView, InventoryArgs>(args, ct);
await ui.BackAsync(ct);
await ui.ReplaceAsync<SettingsView>(ct);
await ui.PopToAsync<HomeView>(ct);
await ui.CloseAsync(home);
var answer = await ui.DialogAsync(new ConfirmPurchase(productId), ct);
var popup = await ui.PopupAsync<RewardView, RewardArgs>(rewards,
    priority: 10, deduplicationKey: "daily-reward", cancellationToken: ct);
```

示例中的 home 在导航替换后可能已关闭；旧句柄重复关闭只返回自己的完成结果，不关闭后来恢复的首页。

| 入口 | 返回/规则 |
| --- | --- |
| OpenAsync<TView> / <TView,TArgs> | 保留 typed Session；Page 成功建立新根历史，其他层普通打开；类型擦除入口同样分流 |
| PushAsync<TView> / <TView,TArgs> | UniTask<IUISession>，仅 Page，追加描述符 |
| ReplaceAsync<TView> / <TView,TArgs> | UniTask<IUISession>，替换当前；空历史等价首开 |
| PopToAsync<TView>(ct) | UniTask<bool>，最近匹配历史项/原参数；缺失或已当前为 false |
| BackAsync(ct) | UniTask<bool>，顶层输入所有者弹窗优先，否则上一页；根页无动作 |
| CloseAsync(IUISession, reason) | UniTask<UICompletion>，验证 Context，不影响其他会话 |
| DialogAsync<TResult>(IDialogRequest<TResult>,ct) | DialogResult<TResult>，保留 Closed 屏障和取消 |
| PopupAsync<TView> / <TView,TArgs> | UniTask<PopupQueueResult>，泛型请求、独立取消 |
| IUISession.ContextId | 新增只读所属 Context |

- Page Open/Push/Replace/PopTo 共用 Navigator/事务/历史。新根成功提交才清旧历史，失败或提交前取消恢复旧页。
- 相同 Single 当前仍打开时 Open/Push/Replace 返回 AlreadyOpen，不隐式重载。PopTo 只重建目标，不播放跳过页的生命周期。
- Close 指定 Session 是 owner 关闭；关闭当前导航页清历史。
- Back/输入 Cancel 统一并尊重 CanClose(Back)，否决不穿透；一次最多处理一个目标。显式 Open/Push/Replace/PopTo 不额外触发用户返回确认。
- Back 在 FIFO 内路由内部导航步骤，不在钩子里嵌套公共命令。旧页关闭错误不回滚已提交历史。
- Dialog 保留 Submitted/UserCanceled/ContextStopped；false/null/default 合法，旧 responder 无效。
- Popup 等待取消移除请求；打开取消走回滚；显示后取消等待 Closed 再抛取消；去重 key 在终结屏障后释放。
- 普通异步任务只 await 一次；关闭/Shutdown 共享结果可重复等待。

### C5. 输入、动画、绑定与 Editor

Input：保留 Gate/Arbiter/Domain/驱动。Workbench 按安装依赖与 Active Input Handling 明确选择；冲突拒绝，不自动替换。运行时不创建/切换 InputModule；多 Context 显式共享输入域。

DOTween：单界面/配对适配；UIKit 管播放记录、取消、释放；只写 AnimationRoot/Opacity，默认非缩放 Update。Stop 同步 Kill 当前记录并终结等待，独立播放身份防旧回调影响重入。不 KillAll、不改业务全局配置。基础默认 Immediate；SDK 提供 preset，Host 配置单界面及配对 preset；不增加复杂每页面选择策略。

R3：不新增生产绑定引擎；复用 uGUI 扩展，订阅交 session.Track，后台通知先主线程；缓存 View 销毁不是绑定寿命。

Workbench：延续窗口/状态机/独立 PlayMode 预览，增加模板、来源、依赖状态和 Diagnostics；保留 Undo、所有权/GUID/指纹、用户代码保护和恢复。

## D. 实施任务、验收与回退

### D1. 通用执行规则

每项任务独立 PR，任务编号不是额外任务文档。保存本计划后先 Phase 0；后续从更新 main 创建 feat/*、fix/*、refactor/*、chore/*。复查当前差异和验收，逐项暂存、提交、创建 PR；不 git add .。用户确认 main 合并后开始依赖任务；回退优先整项 revert，不改远端历史。同原因最多两轮自动修复，移交保留计数。第三方安装、系统/凭据变化另行授权，计划不扩展环境权限。

以下 P=Packages/com.wulato.uikit/。移动文件包含 .meta 并记录完整移动清单。以下任务均未因计划批准自动记为已验收。

### Phase 0：稳定基线与 GitHub Flow

#### 0.1 建立 main 基线

- **目标/方案：** 从 master/e041e03 创建 main；chore/uikit-0.5-baseline 引入现有已提交历史并创建基线 PR；不改原分支/脏目录。
- **文件：** 现有 22 个提交的历史范围，本计划、AGENTS.md、README.md、Verification/README.md 的当前任务入口。
- **Breaking Changes：** 无 Runtime 变化；任务入口切 0.5，历史保留。
- **依赖：** 计划批准；main 合并仍由用户确认。
- **风险：** 大基线 PR，历史证据与脏目录指纹不同。
- **验收：** PR 除明确文档修改外与 a117100 树一致；合并后才切默认分支 main。
- **最小测试：** Git 树/差异、CLI、干净消费项目 package-install/quickstart-only；不移动发布/远端全量。
- **回退：** 不合并，保留 master 默认；不擅删新 main。

#### 0.2 串行开发约定

- **目标/方案：** 固定分支/PR/合并/证据规则。脏目录保留，可使用一个干净工作区；不自动多 worktree 并行。
- **文件：** AGENTS.md、README.md、本计划。
- **Breaking Changes：** 长期 codex 分支改短分支。
- **依赖：** 0.1 合并；合并前可以准备文档，不能宣称全部完成。
- **风险：** PR 授权被误解为 main 自动合并。
- **验收：** 按仓库能力配置 main 禁止强推/删除；Phase 6 前不强制未可用 CI。
- **最小测试：** 文档/分支/PR 设置检查。
- **回退：** revert 文档/可恢复设置，保留分支和工作区。

### Phase 1：基础架构与单 Package

#### 1.1 收敛基础 Runtime 程序集

- **目标/方案：** 新 Wulato.UIKit.asmdef 合并七个基础程序集，先不改行为；更新边界/测试/生成器。
- **文件：** P/Runtime/**.asmdef、AssemblyInfo.cs、Editor asmdef、Tests/Samples asmdef；Tools/Verification/contract_probe.py、ci_suites.py 及测试。
- **Breaking Changes：** 旧程序集引用失效；事务能力/实现 internal，无空壳。
- **依赖：** Phase 0。
- **风险：** asmdef/程序集限定类型/序列化/生成映射。
- **验收：** 无缺失脚本/GUID 变化/SDK 反向引用，生成仍在原业务程序集，无内部类型泄漏。
- **最小测试：** Python 边界、两后端编译；Legacy contracts/integration、generated-integration、loading-layout、context-host；受身份变化影响的 Host Reload。
- **回退：** 整项 revert，恢复 asmdef/引用。

#### 1.2 合并可选适配包

- **目标/方案：** 两个 SDK 适配 Runtime/Editor/Tests/文档/样例迁入主包，配置条件编译，移除独立 UIKit 适配包。
- **文件：** 两旧包目录、P/Runtime/Integrations/、Editor/Integrations/、Tests/Integrations/、package.json；runtime.py、build.py、matrix.json 及测试。
- **Breaking Changes：** 删除旧 UIKit 包引用，SDK 仍项目安装；保留脚本 GUID。
- **依赖：** 1.1。
- **风险：** SDK 缺席 asmdef、Editor/Sample 泄漏、重复类型。
- **验收：** 四依赖组合实际编译，基础安装无可选 SDK，重复旧包有迁移提示。
- **最小测试：** 四组合编译/package-install；addressables-runtime/integration；input-package/input-runtime，无全 suite。
- **回退：** revert，按记录恢复消费 manifest，不删 SDK/用户资产。

#### 1.3 统一资源路由

- **目标/方案：** UIResourceResolver 分发 Prefab/Resources/External；适配注册工厂，Registry 来源驱动，无普通 Loader 注入。
- **文件：** 新 P/Runtime/Loading/UIResourceResolver.cs、Core/UIRegistry.cs、Builder、Addressables Loader/Bootstrap、Editor 来源校验、新 ResourceResolverTests.cs。
- **Breaking Changes：** Addressables 默认无 WithLoader；自定义高级入口保留。
- **依赖：** 1.2。
- **风险：** Reload 注册、共享状态、缺后端报错时机。
- **验收：** 同 Registry 三来源，多 Context 快照隔离；分配前校验；取消/缓存/销毁后释放保持。
- **最小测试：** contracts、loading-layout、addressables-runtime/integration；幂等/Reload 定向。
- **回退：** revert 路由，恢复显式注入，资源 key 不改写。

### Phase 2：业务 API 与导航

#### 2.1 入口及默认装配

- **目标/方案：** Facade 唯一 IUIContext、Engine 内部命令；默认三业务服务，host.UI、ContextId、统一 Dialog 注册。
- **文件：** IUIContext/IUISession/Session 包装、Facade/Builder/Host、UIRegistry、Dialogs/生成器、新 ContextApiTests.cs。
- **Breaking Changes：** Context→UI；无需 UseNavigation/Dialogs/PopupQueue，移除重复入口/公开事务。
- **依赖：** 1.3。
- **风险：** 构造顺序、回滚、公共 Open 递归。
- **验收：** 业务无需 GetService；工厂逆序释放，内置服务不可覆盖；跨 Context Close 无副作用；Presenter 可选。
- **最小测试：** contracts/integration/sessions/context-host/dialogs/popup-queue/presentation 直接受影响用例。
- **回退：** revert，恢复显式装配，不保留两套长期入口。

#### 2.2 页面 Open/Push/Replace/PopTo

- **目标/方案：** 现有 Navigator 统一历史，复用 Prepare/Animate/CommitReplacement。
- **文件：** IUINavigator/UINavigator、Facade、UIContextEngine.Pages.cs、NavigationFlowTests/NavigationTransactionTests。
- **Breaking Changes：** Open(Page) 新根，新增 Replace/PopTo，按 C4。
- **依赖：** 2.1。
- **风险：** 失败清历史、取消/提交竞争、重复类型/参数。
- **验收：** 一套事务；提交前回滚焦点/历史，提交后错误不伪装未提交；Modal 不入历史/不误关。
- **最小测试：** navigation EditMode、transitions 页面用例/flow-integration PlayMode；补必需用例。
- **回退：** revert，恢复旧 Push/Back 及样例。

#### 2.3 统一 Back/Cancel

- **目标/方案：** FIFO 内弹窗优先、CanClose、页面返回、根页 no-op；内部步骤避免嵌套提交。
- **文件：** Engine 用户请求、Facade、Navigator、Arbiter 注册、输入/导航测试及联合用例。
- **Breaking Changes：** Cancel 不直接关闭导航页清历史，改统一 Back。
- **依赖：** 2.2。
- **风险：** 穿透、否决继续下传、按住连发、Shutdown/转场锁竞争。
- **验收：** 一次一个目标，否决不退页，根页稳定，两后端新边沿正确，Context 隔离。
- **最小测试：** 两后端 input-runtime；navigation/flow-integration 定向。
- **回退：** revert 返回路由，保留已交付导航，不旁路 Gate。

#### 2.4 Dialog/Popup 入口与取消

- **目标/方案：** DialogAsync/泛型 PopupAsync，统一注册；Popup 独立取消和去重屏障。
- **文件：** Dialogs/PopupQueue、Facade、DialogLifecycleTests/PopupSchedulingTests。
- **Breaking Changes：** 普通业务无需 IPopupRequest/单独 DialogRegistry，结果保留。
- **依赖：** 2.1，2.3 合并后实施。
- **风险：** 取消/提交/Closed/key 释放竞态。
- **验收：** false/null/旧 responder、暂停/打开/显示取消、其他请求推进、错误/PendingCleanup 可见。
- **最小测试：** dialogs/popup-queue EditMode，flow-integration 真实关闭/Shutdown。
- **回退：** revert 入口和取消任务，保留原语义。

### Phase 3：成熟组件

#### 3.1 DOTween 与 Presets

- **目标/方案：** 单界面/配对、Fade/Scale/Slide 与示例；迁移用途后移除通用 UITransitionRunner。
- **文件：** Runtime/Integrations/DOTween、基础 preset/Host 配置、旧 Runner、Tests/Integrations/DOTween、Samples~/DOTween。
- **Breaking Changes：** Runner 移除，IUITransition 扩展保留。
- **依赖：** Phase 2；SDK 安装另行授权。
- **风险：** Kill/Complete 重入、Sequence 时间、Stop 后写入、缺 SDK。
- **验收：** Stop 同步终结/无写入，重入/多 Context 隔离，仅复位声明属性；无 SDK Immediate 正常。
- **最小测试：** DOTween 专项、受影响 transitions/导航取消超时、无 SDK 编译、静态状态相关 Reload。
- **回退：** revert 适配/Runner 移除，不改用户 SDK/全局 Tween。

#### 3.2 R3 Session 绑定示例

- **目标/方案：** 候选 R3/R3.Unity 1.3.1，订阅纳入 Session.Track，无绑定引擎。
- **文件：** Samples~/R3、可选测试、Documentation~/Integrations/R3.md。
- **Breaking Changes：** 无核心 API 变化。
- **依赖：** 2.1；SDK/DLL 安装另行授权。
- **风险：** NuGet 不完整、缓存解绑遗漏、后台 UI。
- **验收：** 重开无重复订阅、结束不更新、Model 不释放、无 R3 主包可编译。
- **最小测试：** 绑定专项/presentation 相关场景，不完整生命周期回归。
- **回退：** revert 示例文档，不自动卸载第三方。

#### 3.3 FancyScrollView 与控件边界

- **目标/方案：** 候选 1.9.0，固定尺寸大数据/动画列表示例；UI Extensions 仅指南，不包装控制器。
- **文件：** Samples~/FancyScrollView、可选测试、Documentation~/Integrations/Controls.md。
- **Breaking Changes：** 无。
- **依赖：** Phase 2；安装另行授权。
- **风险：** 复用异步串项、拖拽/Modal 冲突。
- **验收：** Cell 数随视口、换项取消/身份校验、Session 结束回调无效、默认样例无 SDK。
- **最小测试：** 列表专项、ScrollRect/Modal 输入场景；记录局部性能不扩大声明。
- **回退：** revert 示例，继续原 ScrollRect。

### Phase 4：Workbench 与体验

#### 4.1 模板/来源/初始化

- **目标/方案：** 扩展已有窗口，Page/Modal/Dialog/可选 Presenter 模板，来源/驱动选择，Host presets。
- **文件：** Workbench、UIViewAuthoring、UIAuthoringOperation、UISceneAuthoring、Marker/生成器/SDK Editor 校验。
- **Breaking Changes：** 新模板用 0.5；Page Single/Release，Modal/Dialog Multi/Release；旧资产策略不静默更改。
- **依赖：** Phase 2、3.1。
- **风险：** 续跑覆盖文件、SDK 推断、GUID 变化。
- **验收：** 首页无需模块装配；幂等/冲突先报/Undo/原业务程序集/用户 partial 保护。
- **最小测试：** authoring/editor/generated-integration/quickstart-only，两输入配置。
- **回退：** revert 工具，不改已生成用户资产，按迁移说明处理。

#### 4.2 Diagnostics/预览

- **目标/方案：** Workbench 显示 Context/Session/历史/缓存/输入/锁/错误/PendingCleanup，延续独立 PlayMode 预览。
- **文件：** 新 Editor/Diagnostics/UIDiagnosticsPage.cs、Workbench、Preview Profile/Runtime/Page、必要诊断快照字段。
- **Breaking Changes：** 预览统一资源装配，不需 Addressables Loader Adapter。
- **依赖：** 4.1。
- **风险：** live View 保留、停止快照误判实时、恢复覆盖场景。
- **验收：** 无可写运行对象，标注终态采样；重开新 Session，保存取消/资产保护。
- **最小测试：** diagnostics/preview-authoring/runtime/context-host，保留受影响 Reload 要求。
- **回退：** revert UI，不删除用户改过临时资产。

### Phase 5：有限高级能力

#### 5.1 Localization/Theme/Focus 指南

- **目标/方案：** Unity Localization 候选 1.4.5 示例；Theme 项目侧 Evo 1.x/其他 Styler；Focus 原生 Selectable/恢复；不建新框架。
- **文件：** Samples~/Localization、Documentation~/Integrations/Localization.md、ThemeAndFocus.md、可选夹具。
- **Breaking Changes：** 无，不升级现有 Evo。
- **依赖：** Phase 4；安装另行授权。
- **风险：** 切语言迟到、关闭订阅、Styler 越门控、控件自建输入模块。
- **验收：** Session 结束异步更新无效，不改 Shell；依赖缺席主包正常。
- **最小测试：** 本地化专项/焦点 Modal 恢复/门控。
- **回退：** revert 示例指南，不迁移主题资产。

### Phase 6：交付与发布

#### 6.1 文档/安装/迁移

- **目标/方案：** 主包 0.5.0，完整 Breaking Changes/安装/业务流/可选集成/旧包移除；固定提交分发。
- **文件：** package.json、CHANGELOG/README、Migration-0.5.md、Samples/快照配置。
- **Breaking Changes：** 汇总已有变化，不额外兼容层。
- **依赖：** Phase 1–5。
- **风险：** 全局忽略漏收 ~ 目录，消费端脏文件补齐。
- **验收：** 固定提交含所有授权文件/样例/文档/meta；干净项目一个 UIKit 包选择能力。
- **最小测试：** 四组合安装编译、QuickStart/BusinessFlow 生成运行、SDK 示例独立验收。
- **回退：** 不发布，消费保持旧确认提交。

#### 6.2 远端 CI/完整 Editor

- **目标/方案：** 现有 CI 更新路径/身份/映射；普通按影响，发布显式完整，下载 artifact 复核。
- **文件：** uikit-checks.yml、ci_suites.py、证据/门禁配置及测试。
- **Breaking Changes：** CI 不认旧适配包，证据 0.5 身份。
- **依赖：** 6.1，输入冻结。
- **风险：** 漏选/缺上传/指纹漂移/self-hosted。
- **验收：** 真实远端链路，失败证据保留，零匹配/跳过/缺证据失败。
- **最小测试：** Python 相关→远端专项→默认编辑器两后端完整适用 Editor/Reload 门禁。
- **回退：** revert CI，保留失败，不放宽门禁。

#### 6.3 移动/性能/Release Gate

- **目标/方案：** 现有执行器 Android/iOS ARM64 IL2CPP、真机输入/释放/性能。
- **文件：** mobile workflow、build.py/smoke.py、release gate/Verification 移动夹具。
- **Breaking Changes：** 无新增业务契约。
- **依赖：** 6.2；SDK/签名/设备/环境授权。
- **风险：** AOT/裁剪/触摸手柄/SDK 释放/签名设备。
- **验收：** 冻结提交两平台两输入适用矩阵，覆盖导航/Dialog/Popup/Addressables/取消/缓存/Shutdown；性能注明设备/采样边界。
- **最小测试：** 本任务完整发布门禁；额外 Unity 版本仅新增支持声明时加入。
- **回退：** 不发布，不用 Editor 替移动；修复按影响复验，最终证据匹配发布指纹。

### D2. 验证配置与顺序

```sh
python3 Tools/Verification/run.py tests --editor 2022.3.62f2 --backend legacy --mode EditMode --suite navigation
```

每任务明确 suite/mode/backend，阶段完成不自动 suite all。结构迁移需要受影响源码实际编译，不表示所有用例重复运行。

| 编译组合 | SDK | 输入 |
| --- | --- | --- |
| C0 | 无 | Legacy |
| C1 | Addressables | Legacy |
| C2 | Input System | Input System |
| C3 | Addressables + Input System | Input System |

输入/Back 另验 Both 下明确选择两驱动，不扩大全部 suite。DOTween/R3/Fancy/Localization 各自任务验证有/无依赖。

顺序：0.1→0.2→1.1→1.2→1.3→2.1→2.2→2.3→2.4→3.1→3.2→3.3→4.1→4.2→5.1→6.1→6.2→6.3。

最高风险：程序集身份/可选隔离；公共 Page Open 递归/重复生命周期/提前历史；Back 输入重入；Addressables 过早释放；DOTween Stop 重入；脏目录/历史证据误入基线。

## E. 非目标与批准边界

不做第二套页面/动画/生命周期、全局默认 Context、强制业务架构/R3、通用动画/Observable/虚拟列表/控件库、自研渲染器、FairyGUI/UI Toolkit 第二后端、重写 Workbench/完整编辑器/Source Generator、DI/扫描模块体系、主题语言/焦点树/输入管理器、Evo v2/Unity 6 升级、自动采购/内嵌 SDK、远端资源平台/热更新/持久历史、长期兼容层/空壳包、默认完整矩阵/移动构建、无测量性能重写/零 GC 承诺。

计划批准后先 Phase 0，不直接 Phase 1。每项 main 合并用户确认。历史文档/证据保留，进度/决策/验收集中记本文件。

## F. 执行记录

### 2026-11-05 Phase 0 启动

- [x] 用户指令“开始执行 plan”批准已审查的方案；只推进 Phase 0，停在 main 合并门禁。
- [x] 重新核对本地/远端：a117100、master/e041e03，无 main/开放 PR，原脏目录与审查一致。
- [x] 通过原生 create_worktree 创建一个干净工作区 `/Users/tian/.codex/worktrees/uikit-05-baseline/Wulato.UIKit`，分支 `chore/uikit-0.5-baseline`。原目录所有用户修改保留。
- [x] 保存本计划；后续证据保存在该工作区 `Artifacts/uikit/phase0/`。
- [x] 核对基线树：相对 a117100，生产源码、资产、项目配置、验证工具及宿主跟踪文件零差异；仅计划与三份工作流入口文档变化。
- [x] 最小实际验收通过：Unity 2022.3.62f2/Legacy；主包干净宿主 prepare 退出 0，package-install EditMode 2/2，QuickStart 8 个跨编译准备/幂等步骤及 PlayMode 1/1，全部无失败/跳过。
- [x] 原始证据以当前快照复核：两个 suite 的 XML/trace/哈希/必需用例 PASS，汇总 [verification.json](../../Artifacts/uikit/phase0/verification.json)。命令日志与退出码在同目录。
- [x] 创建 main/e041e03；远端默认分支仍为 master，没有合并或改写已有功能分支。
- [x] 提交并推送文档（首提交 0dff1e3），创建 [基线 PR #1](https://github.com/wulato/Wulato.UIKit/pull/1)，base=main、head=chore/uikit-0.5-baseline；未合并。后续仅补本执行记录。
- [x] 用户确认后普通合并 PR #1，切默认 main、配置保护并开始 0.2；详情见下方收尾记录。

Ruling：基线任务分支直接从 a117100 建立，而 main 从其祖先 e041e03 建立；这与从 main 引入现有分支的普通快进结果一致，避免新增无内容合并提交。PR 树仍按 a117100 加本次明确文档差异核验。

Ruling：本任务仅迁移 Git/文档，遵循用户单 Agent、现有计划记录和最小验证约定，不额外创建 SDD 账本、执行新 hook 或启动独立审查代理。生产代码变化为零，相关运行验收证明导入基线可消费，不用机械文档 TDD 制造测试。

验证命令（cwd 为上述干净工作区；日志保留真实退出码）：

```sh
python3 Tools/Verification/run.py prepare --phase runtime --package-set main --editor 2022.3.62f2 --backend legacy --host-root Artifacts/uikit/ci-selected/hosts/phase0-baseline
python3 Tools/Verification/run.py tests --editor 2022.3.62f2 --backend legacy --mode EditMode --suite package-install --host-root Artifacts/uikit/ci-selected/hosts/phase0-baseline
python3 Tools/Verification/run.py prepare-samples --editor 2022.3.62f2 --backend legacy --quickstart-only
```

原始目录：`Artifacts/uikit/20261105T075018038615-prepare-c2830d`、`20261105T075128412604-tests-2b6adc`、`20261105T075138081357-quickstart-consumers-319fbc`、`20261105T075239618242-tests-76ee44`（后三项同在 Artifacts/uikit 下）。本次未发生测试失败或代码修复，修复次数 0；原目录历史类别筛选问题计数 1 单独保留。未运行额外编辑器、Input 后端、全 suite、远端 CI、Player/IL2CPP、真机。

Ruling：基线 PR 会引入历史 workflow，HEAD 文档提交使用 `[skip ci]` 避免 pull_request/push 自动触发远端流水线，符合远端 CI 留到 Phase 6 的边界；不修改 workflow 或禁用仓库 Actions。[GitHub 官方说明](https://docs.github.com/en/actions/how-tos/manage-workflow-runs/skip-workflow-runs)。若未来配置必需检查，跳过可能留下 Pending，不应绕过门禁；Phase 0 main 合并仍由用户确认。

GitHub 连接器创建分支返回 403（Resource not accessible by integration）；已用现有 gh 账户完成获准的创建操作，没有扩展连接器权限。

集中作者自审：本次差异仅四份文档，生产树与已验证输入未变；本地链接/18 项任务字段/代码围栏/空白检查通过。原工作区仍在 codex/p0-p1-preflight，8 个已修改文件与 Evo/TMP 未跟踪资产均保留，未暂存、提交或清理其内容。未启动子代理；未执行新 hook。干净 worktree 保留供 PR 反馈使用。

以上为基线准备时的历史记录；PR #1 的后续合并与设置结果见下节。本地验证材料不冒充远端 artifact 或发布验收。

### 2026-11-05 Phase 0.2 GitHub Flow 收尾

- [x] 用户明确确认“合并 PR #1，切换默认分支为 main 并配置保护，然后按计划继续下一项任务”。
- [x] 合并前重新核对 PR #1 OPEN/CLEAN、HEAD=`7c1d9a88747b4dbd7defdbb8583528a41231cc2e`；使用 `gh pr merge --merge --match-head-commit` 普通合并，无管理员绕过、无分支删除。
- [x] PR #1 合并提交=`7b88f696b62ba707ec82f43186ecfb2b4e46cf0a`；远端 default_branch=main，master/原开发分支保留。
- [x] main 保护：required_pull_request_reviews 已配置，required_approving_review_count=0，dismiss_stale_reviews=true；enforce_admins=true；allow_force_pushes=false；allow_deletions=false；required_status_checks=null。
- [x] 从更新后的 github/main 建立 `chore/uikit-0.5-github-flow`，复用当前干净 worktree，不新建工作区、不触碰原脏目录。
- [x] 更新 AGENTS/README/验证入口与本计划，避免继续引用“PR #1 未合并/默认 master”的当前状态；历史记录保留。
- [x] 文档/远端配置 L0 核验通过：[verification.json](../../Artifacts/uikit/phase0/github-flow/verification.json)；合并树与 7c1d9a8 完全一致，四份文档以外的生产树未改。
- [x] 提交 fc71fd4 并推送任务分支，创建 [收尾 PR #2](https://github.com/wulato/Wulato.UIKit/pull/2)（main ← chore/uikit-0.5-github-flow），等待用户确认合并；随后仅补本条 PR 记录，不改生产树。
- [x] 用户后续明确确认合并 PR #2 并执行 1.1；以普通 merge 合并为 `417d52e870a76c378a0db8552753fcf741f7f90e`，从最新 github/main 建立下一任务分支。

Ruling：保护规则强制 PR，但必需批准数为 0，不要求第二个 GitHub 账号为同账号代理创建的 PR 批准。用户明确的合并确认仍由会话工作流执行，零批准不是自动合并授权。Phase 6 前不新增 required CI checks，不修改或禁用历史 workflows。

本任务档位 STANDARD（配置与文档收尾），验证为 L0：远端 PR/默认分支/保护字段读取、合并树与已验收基线比较、局部文档链接/计划字段/差异检查；不新增或重复 Unity、Python suite、Player 或远端 CI。证据保存在 `Artifacts/uikit/phase0/github-flow/`。本次代码修复次数 0；历史类别筛选修复次数 1 原样保留。

**下一动作：** 本收尾 PR 合并后，从最新 main 创建 `refactor/uikit-0.5-runtime-assembly` 执行任务 1.1；不将本次配置核验扩大为程序集迁移验收。

### 2026-11-05 任务 1.1 实现与专项验证

- 基线 `417d52e870a76c378a0db8552753fcf741f7f90e`；分支 `refactor/uikit-0.5-runtime-assembly`，复用当前 worktree，原用户脏目录不动。
- [x] 结构用例 RED→GREEN：七个基础 asmdef 收敛为根 `Wulato.UIKit.asmdef`，仅引用 UniTask/UnityEngine.UI；原 Core asmdef GUID 迁移保留，不留空壳。对应定义/AssemblyInfo 删除与消费者引用同步更新。
- [x] 事务 Access/Steps/Participant/Owner 及输入记录/跟踪/Mask/DomainRegistration 设为 internal；IUIContext/View/Session/Loader/Lease/Transition/Input driver 公共扩展保留，不修改运行时控制流。
- [x] 更新包、Editor、Tests、样例和已跟踪消费夹具 asmdef；生成器/编译探针/安装与 IL 闭包校验识别新身份；suite 选择明确覆盖根 asmdef 与 .meta 的生成/Host/Input 边界，不使用 suite all。
- [x] 新增 contracts 必需用例验证同一 Runtime 身份与内部类型可见性；按当前版本配置写入 matrix，不扩大本次编辑器目标。
- [x] 受影响 Python 117/117 PASS（10 个模块），见 `Artifacts/uikit/phase1-1/python-affected.log` 与退出码文件；结构首次预期失败日志 structure-red.log 保留。
- [x] Unity 2022.3.62f2 Legacy/Input 两主包隔离宿主 prepare 编译 PASS，原始目录 `Artifacts/uikit/20261105T081319150802-prepare-8b9dec`；未安装 Addressables。随后仅补迁移文档，不改变编译源码/引用。
- [x] 保留业务脚本/Prefab/Scene/Asset 的原始 .meta；迁移前清单 `Artifacts/uikit/phase1-1/baseline.json`。允许删除的 AssemblyInfo 元数据单独列明，不把已删除 asmdef 声称为仍存在。
- [x] Legacy EditMode：contracts 20、integration 20、editor 16、package-install 2；PlayMode：generated-integration 3、loading-layout 26、context-host 10、transitions 40、flow-integration 3，全部 PASS。
- [x] Input：input-package EditMode 2、context-host PlayMode 10 PASS。合计 11 组、152 个 Unity 用例；实际用例集合逐项与必需集合完全一致，无跳过或额外类别匹配。
- [x] 真实 codegen 九阶段 PASS；两后端跨程序集编译探针 positive/negative/recovery 各三步 PASS（negative 的退出 1/CS0311 为必需的预期结果，不记失败）。
- [x] 两后端 Host Reload PASS：Legacy 0/1/2/3、Input 0/3 六组合，每组合两次真实 Play/退出，自动退出报告无旧 Context/锁/订阅。
- [x] 当前快照、XML/trace/原始文件哈希、codegen/probe 清单及 Reload 证据索引均复核 PASS，汇总 [verification.json](../../Artifacts/uikit/phase1-1/verification.json)。
- [x] 集中作者自审：Runtime C# 差异仅访问级别与友元属性收敛，无生命周期/资源/输入算法变更；脚本及序列化资产元数据保持；消费者无旧程序集引用；文档路径、代码围栏、18 项任务字段及 diff 空白检查通过。按用户要求未启子代理。
- [x] 实现提交 `c7030dc9eb167d552347a7f7225b776593fe51a6` 已推送，创建 [任务 1.1 PR #3](https://github.com/wulato/Wulato.UIKit/pull/3)（main ← refactor/uikit-0.5-runtime-assembly）；后续仅补本条进度记录。
- [x] 后续按用户确认合并 PR #3，merge commit `68dcf43499f4dd793b40708cf8411f766aa01462`；从更新 main 开始 1.2。

Ruling：同步已跟踪的四宿主消费 asmdef（包括旧 6000 夹具）仅消除悬空引用，不运行额外编辑器或扩大兼容声明。Addressables/Input System 本次仍是独立包，仅更新它们引用的基础程序集，包目录迁移留到 1.2。

Ruling：包版本暂按原锁定值保留；Breaking Change 标记为 0.5 Unreleased 开发检查点并交付 Migration-0.5，最终版本提升按 6.1，不作为已发布 0.4 patch。

Ruling：专项增加 editor/package-install、配对事务 transitions/flow-integration 和 Input 适配引用/Host 检查，因为程序集身份与 internal 边界直接影响生成、装配与跨程序集调用；仍不运行完整 suite。既有类别筛选问题的历史修复计数 1 保留，不夹带原未提交修复。所选 suite 的实际用例集合必须和该 suite 必需集合一致，额外正则匹配不能计作专项通过。

本次生产代码/测试的非预期失败修复计数：0；有意结构 RED 不是修复循环。手工汇总证据时，相对 host 路径的排序产生不同指纹；改用 runner.selected 一致的绝对路径后一次复核通过（汇总脚本调整 1 次），未改源文件、原始证据或断言，不重跑已通过测试。证据集中于 `Artifacts/uikit/phase1-1/`；执行期间不改对应验证输入，文档和进度更新不重跑无关检查。

实际验证均从仓库根目录调用现有 run.py，host-root=`Artifacts/uikit/ci-selected/hosts/phase1-1`、editor=`2022.3.62f2`：prepare runtime/main/backend all；codegen/backend legacy；上述 tests 的 mode/suite/backend；compile-probe/backend all；play-reset/backend all/scope context-host。每条命令日志和退出码在汇总目录，所有执行退出码 0。未跑 suite all、其他编辑器、远端 CI、Player/IL2CPP、真机；Addressables Runtime 集成本次未运行。

原始索引：prepare `20261105T081319150802-prepare-8b9dec`、codegen `20261105T081711745351-codegen-00b375`、compiler `contract-probes/20261105T082154587823-dc0a08`、Host Reload `20261105T082235088575-host-reset-408088`（均位于 Artifacts/uikit）；11 个 suite 原始路径逐项列于 verification.json。材料在本地保留，不冒充远端 artifact 或完整发布门禁。


### 2026-11-05 任务 1.2 合并可选适配包

- 基线 `68dcf43499f4dd793b40708cf8411f766aa01462`；分支 `refactor/uikit-0.5-optional-adapters`，复用干净 worktree，原用户脏目录不动。
- [x] Addressables/Input System 的 Runtime/Editor/Tests 迁入主包 Integrations；LocalInventory 样例、文档及变更记录同迁。删除两个旧独立包及 manifest/lock 引用；保留程序集/类型名称、脚本及资产 GUID，不留空壳。
- [x] 每个可选 Runtime/Editor/Tests/Sample asmdef 独立声明 SDK Version Defines 与 Define Constraints；主 Runtime 不反向引用 SDK，主包必需依赖不变。业务安装一个 UIKit 包，SDK 按需安装。
- [x] 验证、Git 快照、消费者与发布工具识别单包和 SDK 组合；manifest/lock 中的旧 UIKit 适配包在准备前拒绝，并提供移除旧包/保留 SDK 提示。Editor Validate/build 同样提供迁移提示；不承诺在重复 asmdef 已导致编译失败时仍能加载 Editor 提示，应先按迁移文档移除旧包。
- [x] 根 lock 仅移除两个已退休 UIKit 包节点；不整理其他历史节点。同步四个已跟踪验证宿主引用，不运行额外编辑器。
- [x] 新增安装用例检查实际 Player/Editor 程序集与 SDK 存在性对应、基础 IL 引用隔离及旧包提示；更新 package-install 必需清单。结构用例 RED→GREEN；受影响 Python 11 模块共 135/135 PASS。
- [x] Unity 2022.3.62f2 的四个全新隔离宿主 prepare 与初次 package-install 各 4/4 PASS：C0 无 SDK、C1 仅 Addressables、C2 仅 Input System、C3 双 SDK。初次 C1/C3 安装记录在后续样例/内容准备后由最终记录替代，不重复计数。
- [x] 最终 15 组、209/209 Unity 用例 PASS：四组合 package-install 各 4；C1/C3 addressables EditMode 各 5、addressables-runtime PlayMode 各 12、addressables-integration PlayMode 各 11；C0 Legacy input-runtime 43；C2/C3 input-package 各 2、input-runtime 各 45。XML 实际用例集合与必需集合完全一致，无跳过/额外类别匹配。
- [x] C1/C3 真实样例导入/生成/编译/写回/场景/验证各 9 步 PASS；各产出 2 个真实 StandaloneOSX Bundle（不是 Player 构建），内容源指纹及 5 个内容文件哈希分别复核 PASS。
- [x] 最终各 suite 当前指纹、原始索引/XML/trace/文件哈希、4 组实际程序集开关、两个样例各 34 个原始文件哈希复核 PASS，汇总 [verification.json](../../Artifacts/uikit/phase1-2/verification.json)。保留 40 份迁移 .meta 原文，5 个适配 Runtime C# 文件内容不变。
- [x] 集中作者自审：单 UIKit 包、独立条件程序集、无基础 SDK 反向依赖、无运行时算法修改；迁移说明/路径/引用、必需用例及 diff 空白检查通过。按用户要求不启子代理。
- [x] 实现提交 `b8df5ff433972a9d10d9257ef1f66f1e10ced9b3` 已推送，创建 [任务 1.2 PR #4](https://github.com/wulato/Wulato.UIKit/pull/4)（main ← refactor/uikit-0.5-optional-adapters）；本次仅补执行记录，不自动合并。
- [x] 后续按用户确认合并 PR #4，merge commit `ab69a0e5c4f2b3120b3ac57041782855be93b5ed`；从更新 main 开始 1.3。

Ruling：Version Defines 的表达式 Addressables `[1.21.21,3.0.0)`、Input System `[1.17.0,2.0.0)` 仅决定候选程序集启用范围；实际本轮验证仅 2022.3.62f2 + Addressables 1.21.21 / SBP 1.21.25 / Input System 1.17.0，不构成整个版本区间的兼容声明。无 SDK 时适配 Runtime/Editor/Tests 不参与编译；可选样例仍须 SDK。版本保持 0.5 Unreleased 开发检查点，最终提升在 6.1。

Ruling：运行时加载/租约/输入 C# 算法不变；不提前实现 1.3 的自动资源路由，Addressables 仍显式 WithLoader，输入后端仍由项目选择。文档说明卸载旧 UIKit 适配包、保留 SDK、程序集名/GUID 不变。回退以整项 revert 并恢复消费 manifest 为准，不删除 SDK、用户资产或远端历史。

失败与修复记录：Python 首次运行的 sample SDK fixture/错误文本仍沿用旧包假设，修复 1 轮后 135/135 PASS。C3 首次 Addressables integration 10/11 PASS，唯一失败为 LocalInventoryScene 路径不存在：该独立宿主未执行 prepare-samples，补齐样例准备和真实 Bundle 构建后定向复验 11/11 PASS（宿主准备修复计数 1，已解决）。不修改运行时或放宽断言；原始失败日志保留。既有类别筛选问题历史计数 1 继续保留，不夹带原目录未提交修改。

最终验证在宿主与迁移文档稳定后集中执行一次上述 15 组，`*-final.log/.exit` 为最终命令；文档澄清引起输入指纹变化，早期试验记录保留但不与最终记录重复计数。汇总脚本初版将 Addressables sample step 误当作含 returncode 字段的通用 step；依据现有 samples.py 结构，改为核对原始成功标记、编译错误及文件哈希（脚本调整 1 次），不修改生产代码/测试/原始证据，不重复跑 Unity。当前未解决代码失败 0。

实际命令均从仓库根目录调用现有 `Tools/Verification/run.py`，editor=`2022.3.62f2`，host-root=`Artifacts/uikit/ci-selected/hosts/phase1-2-c0/c1/c2/c3`（每条命令选择单一目录）。prepare runtime 的 package-set/backend 依次 main/legacy、addressables/legacy、main/input、addressables/input；C1/C3 另运行 prepare-samples/sample-set addressables、prepare/phase addressables，再执行上列 tests/mode/suite。完整命令在各原始 command.json，日志与退出码及失败修复记录集中在 `Artifacts/uikit/phase1-2/`。未跑 suite all、额外编辑器、远端 CI、Player/IL2CPP、真机；未将 asmdef 候选版本范围当作全区间验证通过。下一动作仅交付本任务 PR 并等待用户合并，不开始 1.3。


### 2026-11-05 任务 1.3 统一资源路由

- 基线 `ab69a0e5c4f2b3120b3ac57041782855be93b5ed`；分支 `refactor/uikit-0.5-resource-routing`，复用干净 worktree，不动原用户脏目录。
- [x] 新契约测试先编译 RED：UIResourceResolver 尚不存在；预期 CS0103/CS0246 及退出 1 保存在 `Artifacts/uikit/phase1-3/routing-red.log` 与原始记录。不是非预期修复。
- [x] 统一路由、适配注册、Editor 来源检查与样例迁移；默认 Builder 使用 Resolver，保留显式 Loader 覆盖。
- [x] 受影响 Python 10 模块 121/121 PASS；最终 `python-final-2.log/.exit`。CI 专项选择覆盖 Resolver/Bootstrap 的 contracts/加载/SDK/Reload，测试源仅映射直接 suite。
- [x] 最终 15 组、236/236 Unity 用例 PASS，实际集合与 matrix 必需集合完全相等，无跳过/额外类别匹配。无 SDK Legacy：contracts 27、integration 20、editor 17、package-install 4、loading-layout 26、cleanup 15，共 109。双 SDK Input：contracts 27、integration 21、editor 17、addressables 5、package-install 4、loading-layout 26、addressables-runtime 12、addressables-integration 12、generated-integration 3，共 127。
- [x] 两宿主 codegen 各 9 阶段 PASS；SDK 样例导入/生成/写回/场景/验证 9 阶段 PASS，LocalInventory 不再注入 Loader。同 Registry 的 Prefab/Resources/Addressables 实际加载、缓存新 Session、ClearCache 销毁、取消和 Shutdown 专项 PASS。
- [x] SDK 宿主真实 StandaloneOSX 内容构建 2 个 Bundle；内容源指纹和 5 文件哈希通过。这不是 Player 构建。
- [x] 两宿主 Reload option 0/3 各两轮实际 Play/退出，共 4 组/8 次，PASS；每轮记录 SDK 有/无时后端注册状态，并保留自动退出 Context/锁/订阅归零证据。
- [x] 当前最终指纹、XML/trace/原始索引/哈希、实际 SDK 程序集开关、codegen 各 37 个原始文件及样例 34 文件哈希复核 PASS，汇总 [verification.json](../../Artifacts/uikit/phase1-3/verification.json)。
- [x] 集中作者自审与 L0：四个新增脚本均含 .meta，主包 330 GUID 无重复，既有 .meta 不变；当前文档入口路径与 diff 空白检查通过。未改 Engine/FIFO/Session/Lease/取消与迟到清理算法，无基础 Runtime→SDK 反向引用；未启子代理。
- [x] 实现提交 `c69e77ace12cd71bb5918247d39ebcd4bc95281d` 已推送，创建 [任务 1.3 PR #5](https://github.com/wulato/Wulato.UIKit/pull/5)（main ← refactor/uikit-0.5-resource-routing）；本次仅补执行记录，不自动合并。
- [x] 后续按用户明确确认合并 PR #5，merge commit `d2c2a40d7163910fcdcb17a182bc37ed98b926d9`；从更新 main 开始 2.1。

Ruling：每 Context 构建时复制第一方 provider 工厂表，先验证整个 Registry，再为用到的后端各创建一个 Loader；只共享工厂，不共享 Loader/View/Lease。Engine/FIFO/取消/迟到清理不改动；Resolver 直接传递原始 UniTask，不额外消费。显式 WithLoader 覆盖整个路由，依赖仍调用方所有，保留自定义 backend 高级入口。

Ruling：Editor 只对第一方 addressables 来源检查后端可用性，未知自定义来源保留显式 Loader 工作流；默认 Context Build 则对所有 External 来源验证后端。工厂注册仅第一方 internal 友元接口，不引入公共注册中心、扫描、DI 或第二生命周期。Unity 启动按 SubsystemRegistration 清表、AfterAssembliesLoaded 重新注册；Editor 适配 InitializeOnLoad 注册。采用 AlwaysLinkAssembly 保护仅自动启动引用的适配入口，实际 Player/IL2CPP 验收仍留 Phase 6。依据 [Unity 初始化 API](https://docs.unity.cn/2022.2/Documentation/ScriptReference/RuntimeInitializeOnLoadMethodAttribute.html) 与 [Domain Reload 说明](https://docs.unity.cn/Manual/DomainReloading.html)，实际准确版本由本轮验证记录证明。

本次非预期修复记录：Reload 探针新增 LINQ 查询缺少 using System.Linq，首次 prepare 编译 CS1061；补齐后 prepare PASS（同原因 1 轮，已解决）。既有类别筛选历史修复计数 1 保留，不引入原目录未提交修复。

Editor 修复记录：首次 editor 专项揭示 Inputs 在 Preview/Generate 阶段提前要求 SDK，破坏无 SDK 生成纯描述的既有契约；将第一方后端检查移到 ValidateFor/构建门禁，新增独立回归用例（同原因修复 1 轮）。同批 RegistryGenerationTests 缺少现有 codegen 夹具，补运行 codegen；其失败留下的宿主生成产物由该标准准备流程恢复（宿主准备原因修复 1 轮）。原始失败与输入变化记录保留；最终验证在准备和文档稳定后进行。

SDK 宿主编译修复记录：原 Addressables Editor asmdef 只有基础/SDK 引用，新 Editor bootstrap 调用适配 Runtime 入口时 CS0103。依据引用图补上 Wulato.UIKit.Addressables 显式引用，仍由相同 SDK 条件隔离（此引用原因修复 1 轮）；不修改基础 Runtime 引用。输入稳定后复验受影响的两宿主证据。

Ruling：复用现有 UIRegistry.CopyValidated/不可变 UIRegistration，不为路由增加新 Registry 公共类型或重复复制协议。原 DefaultLoader 删除；Prefab/Resources/External 统一在内部 UIResourceResolver 分发，Addressables 原有显式 Loader 的 fallback 保留以支持 WithLoader 高级入口。

验证范围：默认 Unity 2022.3.62f2；无 SDK Legacy 宿主和双 SDK Input 宿主。contracts、integration、editor、package-install、loading-layout；基础另跑 cleanup，SDK 另跑 addressables/editor/runtime/integration 和 generated-integration。默认/双禁用 Reload（option 0/3）各两次真实 Play，两个宿主共 8 次；不扩展四组合 Reload、其他编辑器或全 Suite。codegen/样例先准备、内容先构建，再在稳定输入执行相关专项；不因收尾自动运行完整 CI 或 Player。

汇总脚本修复记录：codegen 汇总没有 evidence 字段，而 samples 有；依据现有 codegen.py 格式解析其固定宿主子目录后复核通过（汇总脚本修复 1 次）。不改原始证据或测试断言，不为该汇总问题重跑 Unity。最终未解决失败 0；同原因均未超过 1 轮自动修复。

原始失败保留：有意 RED `20261105T123712318055-prepare-3d7c1b`；LINQ 编译 `20261105T124057322336-prepare-21935c`；Editor 门禁/缺夹具 `20261105T124535658427-tests-7ccd01`；SDK Editor asmdef 引用 `20261105T124932091619-prepare-99d05c`（均在 Artifacts/uikit）。文档或程序集引用变化前的预备通过记录不与最终 236 用例重复计数。最终执行脚本 `Artifacts/uikit/phase1-3/final-commands.sh`、逐条日志/退出码与汇总原始路径可定位全部命令；证据本地保留，不冒充远端上传或完整发布验收。未运行 suite all、完整 Reload 组合、额外编辑器、远端 CI、Player/IL2CPP、真机。下一动作仅交付本任务 PR 并等待用户合并，不开始 2.1。


### 2026-11-05 任务 2.1 入口及默认装配

- 基线 `d2c2a40d7163910fcdcb17a182bc37ed98b926d9`；分支 `refactor/uikit-0.5-default-composition`，复用干净 worktree，原用户脏目录不动。先前“进行下一步”的合并请求被自动审批认为未明确授权而拒绝；用户本轮明确确认后正常合并，无绕过。
- [x] 新契约编译 RED（AddDialog/ContextId/CloseAsync 尚未实现，CS1061/CS7036），原始 `20261105T134848711386-prepare-fe59c7`，不是非预期修复。实现后无 SDK Legacy prepare 和首轮 contracts PASS。
- [x] Engine 不再实现 IUIContext，生产实现只剩 Facade；Host.UI、Context/Session ContextId、Context.CloseAsync 归属校验及 typed 包装转发。默认三服务先于用户工厂；Presenter 保持可选；删除 UseNavigation/Dialogs/PopupQueue 与 WithTransactions 旧入口。
- [x] AddDialog 把请求/结果映射放进 Registry 快照；DialogRegistry 改为内部只读映射，生成器输出 AddDialog 并检查 Modal 层。迁移直接样例、夹具、Host/Preview 消费者。
- [x] 最终受影响 Python 10 模块 122/122 PASS，见 `Artifacts/uikit/phase2-1/python-final.log/.exit`。新 ContextApiTests 源文件/元数据同时选择其拥有的 contracts/integration，不扩大无关 suite。
- [x] 最终 29 组、362/362 Unity 用例 PASS；实际集合逐项与 matrix 必需集合完全一致，无跳过/额外类别匹配。Legacy 无 SDK 16 组/225 项，Input 双 SDK 13 组/137 项；精确命令见 `final-commands.sh`，结果逐项见 verification.json。
- [x] 计划最小 contracts/integration/sessions/context-host/dialogs/popup-queue/presentation 均覆盖；因修改生成器、Host/Preview 消费者和默认 Navigator 事务装配，补 editor/package-install/navigation/transitions/generated-integration/flow-integration/flow-samples/preview-runtime 直接相关组；不是 suite all 或完整宿主矩阵。
- [x] 两宿主 codegen 各 9 阶段 PASS；Legacy business 样例准备 6 步、SDK all 样例 9 步 PASS，生成器实际产出统一 AddDialog 映射，默认服务驱动的真实业务/样例/Addressables 场景均通过。SDK 真实 StandaloneOSX 构建 2 个 Bundle，源指纹及 5 文件哈希通过；不是 Player。
- [x] Host Reload：Legacy 0/1/2/3、Input 0/3，共 6 组，各两次真实 Play/退出，共 12 次；旧 Context/锁/订阅归零报告 PASS。
- [x] 当前指纹、XML/trace/原始索引/文件哈希、实际依赖程序集开关、codegen 各 37 文件、样例 25/34 文件及 Reload 证据复核 PASS，汇总 [verification.json](../../Artifacts/uikit/phase2-1/verification.json)。汇总首次通过，无修改原始证据。
- [x] 集中作者自审：生产仅 Facade 实现 IUIContext；外来/伪造关闭先验所属 Engine 实例，不调用不可信句柄方法；默认服务预装、逆序释放和失败回滚有新旧专项证据；无第二生命周期。保留 Presenter/注入依赖调用方所有权。删除旧静态 NavigationBuilderExtensions 及对应 .meta，不留兼容空壳；其余既有 .meta 不变，新增测试附 .meta，主包 330 GUID 唯一。文档路径、现行入口和 diff 空白检查 PASS；未启子代理。
- [x] 实现提交 `dbc640821b56c99ea8064bfe6b43c46a1f30d3d7` 已推送，创建 [任务 2.1 PR #6](https://github.com/wulato/Wulato.UIKit/pull/6)（main ← refactor/uikit-0.5-default-composition）；本次仅补执行记录，不自动合并。
- [ ] 等待用户确认合并 PR #6 后，从更新 main 开始 2.2。

Ruling：UIRegistry/默认服务/Host 及 Context.CloseAsync 属于 2.1；不提前实现 2.2 页面新根/Replace/PopTo、2.3 Back 路由或 2.4 DialogAsync/泛型 PopupAsync。普通 Open/Close 无需 GetService；当前模块高级入口保留 GetService，不临时添加随后废弃的服务属性。

Ruling：原 Engine 单元夹具继续直接验证 Engine；业务模块公共夹具改用同一个生产 Facade 包装 Engine，不增加第二个 IUIContext 实现或生命周期。生产 Builder 在用户工厂前装配 Navigator/Dialogs/PopupQueue，Lifetime 先取消、服务逆序释放；新基础事务默认开启，公开协议仍 internal。WithNavigationTransition 仅配置配对动画，传入依赖不转移所有权。

失败修复记录：首轮 integration 23/24 PASS，缓存重开归属用例揭示模块夹具分别生成 Facade/Engine Id；改为共享 Id 和 Lifetime，并在内部 Initialize 校验身份一致（同原因修复 1 轮）。生产 Builder 原本已共享 Id；保留失败原始 `20261105T135734555175-tests-8441f5`。

最终运行环境：Unity 2022.3.62f2，base 无 SDK/Legacy 与 sdk 双 SDK/Input 两隔离宿主；Addressables 1.21.21 / SBP 1.21.25 / Input System 1.17.0。命令从仓库根目录调用既有 run.py，host-root 分别 `Artifacts/uikit/ci-selected/hosts/phase2-1`、`.../phase2-1-sdk`，脚本/逐条日志/退出码、首次失败及修复全部保留在 `Artifacts/uikit/phase2-1/`。首轮契约/预备通过记录不与最终 362 用例重复计数。当前未解决失败 0；仅夹具身份原因自动修复 1 轮，既有类别筛选历史修复计数 1 保留，不夹带原目录未提交修复。

未运行 suite all、额外编辑器、远端 CI、Player/IL2CPP、真机，不把候选 SDK 范围当作全区间兼容证明；未修改 ProjectSettings、根 manifest/lock 或 CI 工作流。本任务只推进 2.1；2.2 仍须本 PR 经用户确认合并后从更新 main 开始。
