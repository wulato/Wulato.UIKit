# Workbench：模板、来源和初始化（0.5 / 4.1）

通过 Tools → Wulato → UIKit → Workbench 使用现有工具。无需额外编辑器、ViewModel 或模块装配。不会安装 SDK、修改 ProjectSettings 或静默迁移现有策略。

## 新建界面

在“界面”页选择目录、命名空间和模板：

| 模板 | 注册策略 | 业务代码 |
| --- | --- | --- |
| Page | Page / Single / Release | partial UIView，可选已编译参数类型 |
| Modal | Modal / Multi / Release | partial UIView，可选已编译参数类型 |
| Dialog | Modal / Multi / Release | 独立 Request / bool 结果、Submit / Cancel |

Dialog 创建后可由业务编辑请求和结果类型；生成器根据 DialogArguments 生成同一 Registry 中的 AddDialog，无单独 DialogRegistry。Submit/Cancel 只处理捕获的当前 Session；关闭后清空引用。组件绑定仍由单独的生成 partial 承担，不覆盖用户脚本。

“可选 Presenter”额外创建用户所有的 Presenter / Factory 文件；不自动强制启用。需要时在 `host.Initialize(builder => builder.UsePresentation(new ExamplePresenterFactory()))` 中配置，并按 Host 既有规则关闭自动初始化或在首次初始化前配置。Factory 保持外部所有；Presenter 每 Session 绑定/释放。示例 Bind/Dispose 中的业务事件由使用者填写，不代为释放外部 Model。

创建流程跨编译续跑。等待期间若 View/Presenter 被编辑，或目标 Prefab 出现/变更，将停止并保留用户内容；可用“已有 Prefab”流程明确配置。编译失败先修复错误，避免重复创建。已有模式不会套用新模板的策略；已有 Page/Modal/Dialog 的 CacheOne、Multiplicity、GUID 均保留。工具不创建业务 asmdef，绑定 partial 通过已有 asmref 路由回原程序集。

## 资源来源

选中已配置 View/Marker 的 Prefab 后选择 Prefab 直接引用、Addressables 或自定义外部来源，点击“校验并保存来源”，然后重新生成注册。

默认直接引用无需 Loader 装配。Addressables 必须已安装 Unity SDK，Prefab 已在 SDK 工具中配置为明确条目，填写唯一 address 或该 Prefab GUID；会在修改 Marker 前校验条目、Bundle Group 与 Catalog 配置。不自动创建 SDK 设置/条目、下载包或构建 Bundle。SDK 缺失或条目无效时不改变现有来源。修改已配置条目请使用 Addressables 自身工具。

自定义来源使用显式 Provider ID / Asset ID；后端注册/显式 Loader 的责任仍由项目承担。工具不推断远端资源、不包装第二套 Loader。来源修改保留 Prefab GUID 和业务策略，支持 Undo；修改后重新生成，沿用原指纹/构建校验。查看回退后的磁盘状态时按 Unity 正常流程保存资产。

## 场景与 Host

“场景”页选择已生成 Registry，输入驱动可按当前启用后端选择，也可明确选择 Legacy / Input System / 自定义驱动。Both 必须明确选择；不会按“装过某 SDK”猜测，不替换已有冲突模块。

点击“初始化场景与 Host”，在同一个 Undo 操作中配置 UIRoot、EventSystem 与 UIContextHost；冲突时先报错并回滚。导航、Dialog、Popup 由现有 Builder 默认装配，业务从 host.UI 使用。已有 Root/Host 重用，已有不同 Registry 或不同 Root 的宿主归属冲突需在 Inspector 显式处理。与业务启动脚本不能重复装配同一 Root。

可选择已有单界面/页面配对 Transition Preset；空值使用 Immediate。切换 Root 时读取已有 Host 的 Preset，明确修改后再初始化；Host 的其他 Settings 不受覆盖。不会安装 DOTween；已有可选 Preset 资产须先满足自身 SDK 依赖。

场景操作不自动保存场景。回退工具代码不会删除或重写已经生成的用户资产；如需调整已生成模板策略，请在 Marker 中显式修改后重新生成，而不是重新套模板覆盖业务文件。
