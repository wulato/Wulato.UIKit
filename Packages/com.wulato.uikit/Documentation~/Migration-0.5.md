# 迁移到 Wulato.UIKit 0.5.0

0.5.0 汇总 Phase 1–5 的架构与业务 API 变更。按已审核的固定提交安装，先保存消费项目修改并在项目任务分支迁移；不要直接追踪 main。版本号不代表远端 CI、移动或 Release Gate 已完成。完整安装清单见 [Installation](Installation.md)。

## 迁移顺序

1. 记录旧 UIKit 提交、manifest/lock 和已导入样例/生成代码，保留消费项目原有修改。
2. 在同一次 manifest 修改中移除旧 `com.wulato.uikit.navigation`、`.presentation`、`.dialogs`、`.popup-queue`、`.addressables`、`.input-system` 依赖及 testables，只保留 `com.wulato.uikit`；按需保留 Unity SDK。不要手工复制旧 Runtime 来补编译。
3. 更新主包到已审核的 0.5.0 固定 SHA，提交 UniTask2.5.11 显式源；让 Unity 正常解析锁，不从本仓库验证宿主复制锁到业务项目。
4. 按下文更新业务 asmdef、Host 与调用点；重新生成绑定/注册并编译。已导入样例、View/Presenter 模板和 Usage 是项目文件，先比较再更新，不直接覆盖。
5. 选择项目实际输入后端，检查唯一 EventSystem/输入模块；运行项目涉及的导航/取消/缓存/资源专项，再提交项目自身的 manifest/lock。不要把 Editor 通过解释为移动验收。
6. 失败时消费项目保持旧确认提交；撤回迁移分支或恢复已记录的 manifest/lock/生成文件，不删除用户资产、不清缓存来掩盖版本冲突。

旧 [0.4 迁移](Migration-0.4.md) 只保留历史背景，其中独立适配包步骤已被本页替代。

## 消费程序集

从业务、Editor、测试及样例 asmdef 的 references 中移除下列七个旧名称，统一为一条 `Wulato.UIKit`：

```text
Wulato.UIKit.Core
Wulato.UIKit.Loading
Wulato.UIKit.UGUI
Wulato.UIKit.Navigation
Wulato.UIKit.Presentation
Wulato.UIKit.Dialogs
Wulato.UIKit.PopupQueue
```

保留业务自己的 asmdef GUID。程序集名变化不要求更改 `using Wulato.UIKit.Navigation` 等命名空间。原 Core asmdef GUID 随根定义迁移；若外部使用其他六个旧 asmdef 的 GUID 引用，应改为新根程序集引用，不保留旧定义或空壳程序集。

框架 MonoBehaviour/ScriptableObject 源码路径与脚本 GUID 保持；生成绑定仍通过业务 asmdef 的 asmref 编译，不反向引用业务程序集。消费项目若自行保存程序集限定类型名，需要自行更新旧名称；本任务不提供旧类型名兼容层或通用序列化数据迁移。

## 扩展边界

`IUITransactionAccess`、`IUITransactionSteps`、`IUIPageTransactionAccess`、`IUIPageTransactionSteps`、`IUITransactionParticipant`、`UITransitionOwner` 和框架输入跟踪组件改为 internal。外部代码使用 Navigator 与公开的 `INavigationTransition`，不直接调用低层事务或输入记录对象。

IUIContext、View/Session、Loader/Lease、布局、Input driver、单界面及配对 Transition 扩展点保持现有行为。Session/FIFO、取消、缓存、迟到清理、输入门控与 Shutdown 不引入第二套实现。

## 适配单 Package 迁移

升级前从消费 manifest 移除 `com.wulato.uikit.addressables` 与 `com.wulato.uikit.input-system`，随后让 UPM 正常重解析锁；保留按需使用的 `com.unity.addressables` / `com.unity.inputsystem` SDK。不要复制旧 Runtime 或手工安装两份 UIKit 适配源码，否则会发生重复程序集/类型；验证入口在启动 Unity 前检查并拒绝旧包引用；Editor Validate/构建校验也会给出移除提示。重复 asmdef 已导致 Unity 编译失败时，须先移除旧包才能加载 Editor 校验。

程序集名 `Wulato.UIKit.Addressables`、`Wulato.UIKit.InputSystem`、类型名及脚本 GUID 保留；现有业务 asmdef 对这些程序集的引用无需改名。代码、Editor 校验、测试和 LocalInventory 样例均在主包内，分别使用 Version Defines/Define Constraints 隔离 SDK 引用；符号只在各自 asmdef 内有效。主包 package.json 不增加可选 SDK 必需依赖。

开发验收锁定 Unity 2022.3.62f2、Addressables 1.21.21 / SBP 1.21.25、Input System 1.17.0；条件编译的版本区间仅控制候选代码是否参与编译，不代表区间内所有版本均已验证。消费项目应使用锁定版本；现有验证入口会拒绝与所选 editor profile 不符的依赖，并提示修复。其他编辑器兼容 profile 不作为本次通过声明。

默认 Context 按 Registry 的 Prefab / Resources / External 描述路由；已安装的 Addressables 适配自动注册，无需 `WithLoader(new AddressablesUIViewLoader())`。每 Context 拥有后端快照与独立 Loader，后续注册不改变既有 Context；缺少后端在 Context 分配资源前失败。自定义 `WithLoader` 覆盖整套路由，依赖仍归调用方所有。Input driver 继续由宿主/工作台明确选择，不自动安装 SDK、替换输入模块或更改 ProjectSettings。SDK 不存在时不要在业务非条件程序集里直接引用适配类型。

LocalInventory 现在从主包 Samples 导入；要求先安装 Addressables SDK。已导入样例不会被自动移动或覆盖，来源配置与租约所有权契约保持。

## 默认装配与所属 Context（2.1）

`UIContextHost.Context` 改为 `UI`，`Initialize()` 仍返回同一个 IUIContext。Navigation、Dialogs、PopupQueue 默认装配，移除 `UseNavigation` / `UseDialogs` / `UsePopupQueue` 与公开 `WithTransactions`；自定义配对动画改用 `WithNavigationTransition(pair, settings)`。Presenter 仍显式 `UsePresentation(factory)`，简单界面无需业务分层或服务查找。

`IUIContext.ContextId`、`IUISession.ContextId` 提供归属身份。`await ui.CloseAsync(session)` 只接受此 Context 创建的真实 Session（包括 typed 包装），拒绝其他 Context 或伪造句柄；旧 Session 重复关闭只返回自己的 Closed 结果，不关闭缓存重开后的新会话。

Dialog 的请求/结果映射与 View 一起注册：`builder.AddDialog<ConfirmView,ConfirmRequest,bool>(key, asset)`，默认 Modal 层；不再创建单独 DialogRegistry。重复请求类型在构建 Registry 时拒绝。生成器识别 DialogArguments 自动输出 AddDialog；更新后重新生成、编译和写回 Registry。普通 Add<View,DialogArguments<...>> 不声明业务请求映射。

业务扩展工厂执行前，三个默认服务和 Registry/Lifetime 已可查找；默认服务不可被 WithServiceFactory 覆盖。构建阶段只允许读取服务和 ContextId，命令须等 Build 返回；失败先取消 Lifetime，再逆序释放创建的服务并回滚 Root/Input 所有权。注入 Loader/动画/Presenter 工厂仍归调用方所有。

IUIContext 提供直接业务入口；高级导航/Dialog/Popup 服务仍可通过 GetService 访问，使用同一生命周期和事务实现。

## 页面操作（2.2）

Page 的 `OpenAsync` 现在经默认 Navigator 建立新根：成功提交后替换旧页并清旧历史，失败/提交前取消保留旧页和历史。需要追加历史时改用 `ui.PushAsync`；只替换顶项使用 `ReplaceAsync`，回到最近匹配历史项用 `PopToAsync<TView>`。泛型与 key/arguments 入口一致，普通 Modal/其他层 Open 保持普通生命周期，不入页面历史。

原直接 Page Open 的单界面 IUITransition 不再驱动页面 Enter/Exit；页面配对动画通过 WithNavigationTransition 配置。生成映射的页面取消测试也改用配对转场，仍验证绑定释放与迟到清理，不通过删除断言规避新契约。

`ui.BackAsync` 与 Navigator Back 统一为弹窗优先、CanClose(Back)、页面返回、根页 no-op。Cancel 不再直接关闭当前页清空历史；按住键不会重放，旧输入会话不会转投新页面。否决不穿透；Owner Close 与 Mask 仍独立。新 IUIContext/IUINavigator 成员是 Breaking Changes；本包不提供第二套历史或旧 Open(Page) 兼容开关。


## Dialog / Popup（2.4）

普通调用改为 `ui.DialogAsync(request, ct)`、`ui.PopupAsync<TView>(cancellationToken: ct)` 或 `ui.PopupAsync<TView,TArgs>(args, priority: 0, deduplicationKey: key, cancellationToken: ct)`。使用既有统一 UIRegistry / AddDialog 注册；无需 IPopupRequest、单独 DialogRegistry 或 Use*。高级 IUIDialogs / IUIPopupQueue 入口保留，后者增加 key/arguments 重载及逐项 token。

Popup 只接受非 Page，避免队列意外改变导航根。等待取消移除本项；打开取消回滚；显示取消等待 Closed 后抛取消，key 在逻辑终结屏障后释放，其他请求继续推进。停止覆盖未完成取消；错误和未决隔离仍沿用原结果/Diagnostics。新增 IUIContext 成员及 IUIPopupQueue 签名属于 Breaking Changes；维护自定义接口实现或反射调用时应同步更新。Dialog 的 Submitted/UserCanceled/ContextStopped、false/null、旧 responder 与取消语义保持原契约。

## 转场（3.1）

移除 UITransitionRunner；原自定义 IUITransition / INavigationTransition 可继续使用，默认 Immediate 不需要 SDK。Host 新增单界面/页面配对 Preset 字段，configure 可覆盖；DOTween 使用主包内显式启用的可选程序集，参见 [DOTween](Integrations/DOTween.md)。原 Runner 的四项专项迁往实际 DOTween 验证，未保留通用帧动画引擎。

## R3 示例（3.2）

无核心 API Breaking Changes。可选导入 R3 Session Binding，按 [R3 集成文档](Integrations/R3.md) 安装 SDK 并显式启用符号。订阅从 GameObject 生命周期移到每次 Session.Track；业务继续持有并释放 Model。

## Workbench（4.1）

新模板 Page 使用 Single/Release，Modal/Dialog 使用 Multi/Release；已有资产不自动迁移。新增 Dialog/可选 Presenter 源码是用户所有文件，撤回工具不删除它们。来源、输入和 Host 初始化见 [Authoring-0.5](Authoring-0.5.md)。

## Diagnostics / 预览（4.2）

诊断快照新增只读 History（类型键）、Errors（有界值摘要）及 TotalErrorCount；不返回业务参数或异常对象。预览使用默认资源路由，Addressables 不再需要 Loader Adapter；UIPreviewAdapter 继续用于明确参数/业务依赖。空输入驱动依据已启用后端，Both 必须明确选择；可选已有 Host Preset。旧 Profile 新字段为空即保持 Immediate，恢复记录 schema1 可读取旧记录，新附加终态字段不参与资产删除权判断。详见 [Preview](Preview.md)。


## 可选控件与本地化（3.3 / 5.1）

无核心 API Breaking Changes。FancyScrollView 固定尺寸列表、Localization Session 为独立可选示例；SDK 与显式宏见 [安装能力表](Installation.md)。原生 ScrollRect/Selectable 继续用于普通 UI，不自动替换业务控件。Theme 不提供全局引擎，不升级 Evo 或迁移主题资产；不允许 Styler 绕过 Shell 输入门控。

## Breaking Changes 核对表

| 旧用法 | 0.5 处理 |
| --- | --- |
| 七个基础 Runtime asmdef | references 收敛到 Wulato.UIKit；GUID规则见上文 |
| 独立业务/适配 UPM包 | 移除旧 UIKit 包和 testables，SDK 本身按需保留 |
| Host.Context / UseNavigation、UseDialogs、UsePopupQueue | Host.UI / 默认装配 |
| WithTransactions / UITransitionRunner | 已移除；使用现有转场接口与 Host Preset |
| Page Open 追加历史 | Open 新根；Push 追加，Replace 顶项，PopTo 最近匹配 |
| Cancel 直接关闭页面 | 统一 Back；弹窗优先，CanClose 否决不穿透，根页 no-op |
| 独立 DialogRegistry / IPopupRequest | AddDialog / DialogAsync / 泛型 PopupAsync |
| 组件销毁才退订 | 每次 Session.Track，缓存重开重新订阅 |

自定义 IUIContext / IUINavigator / IUIPopupQueue 实现需按当前接口重新编译；不提供旧 API 兼容层。取消仍抛 OperationCanceledException，共享关闭结果/单次 UniTask、Session 隔离、租约所有权、迟到清理、事务回滚和 Shutdown 契约仍有效。
