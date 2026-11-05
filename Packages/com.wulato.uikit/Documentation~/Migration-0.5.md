# 0.5 开发迁移：任务 1.1

当前仅完成基础 Runtime 程序集收敛，尚不是 0.5 发布。package.json 仍保持原版本，消费开发树时固定 Git 提交，不把本分支变化解释为已发布的 0.4 patch。后续 API、单 Package 适配整合和版本升级分别按计划交付。

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

Addressables/Input System 适配包仍单独安装，仅将它们的 Runtime 引用更新为新基础程序集；不改变第三方 SDK 版本或自动注册方式。任务 1.2 之前不要删除消费项目中的独立适配包引用。
