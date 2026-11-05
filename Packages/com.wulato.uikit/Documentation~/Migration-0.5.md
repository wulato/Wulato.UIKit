# 0.5 开发迁移：任务 1.1–1.2

当前完成基础 Runtime 收敛与可选适配迁移，尚不是 0.5 发布。package.json 仍保持原版本，消费开发树时固定 Git 提交，不把本分支变化解释为已发布的 0.4 patch。后续资源路由、业务 API 和版本升级按计划交付。

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

当前仍显式 `WithLoader(new AddressablesUIViewLoader())`，Input driver 由宿主/工作台明确选择，不自动安装 SDK、替换输入模块或更改 ProjectSettings。资源自动路由属于 1.3。SDK 不存在时不要在业务非条件程序集里直接引用适配类型。

LocalInventory 现在从主包 Samples 导入；要求先安装 Addressables SDK。已导入样例不会被自动移动或覆盖，来源配置与租约所有权契约保持。
