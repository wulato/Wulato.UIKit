# Wulato.UIKit P1：Context 宿主与独立预览

状态：计划草案；未开始实现。用户已于 2026-11-03 确认首版采用真实 PlayMode 隔离预览。

## 目标

在 P0 工作台之上提供两个连续交付：Inspector 配置即可装配 Context；选择已注册界面即可在独立场景运行、关闭与重开。高级业务继续直接使用 Builder。

依据：现行 `2026-10-31-wulato-uikit-design.md`、现有 Builder/Engine/Registry 实现和 P0 工作台。现行设计对 FIFO、Session、Single、缓存、参数、所有权、超时及迟到清理的规定保持有效。

## 方案选择

| 方案 | 能验证的内容 | 取舍 |
| --- | --- | --- |
| PlayMode 隔离场景（用户已选定） | 真实注册、输入、生命周期、关闭与重开 | 需要进入/退出 PlayMode，并可靠恢复编辑现场 |
| EditMode 静态 Prefab | 外观与层级 | 无法证明 Context 和 Session 行为 |
| 两种模式同时提供 | 上述两类 | 增加两套入口和恢复路径，暂不纳入本轮 |

独立预览表示不依赖业务启动场景，不表示模拟业务完整数据环境。依赖业务服务、外部资源或参数的界面必须显式提供预览适配器。

## Context 宿主

新增主包 UGUI 层 `UIContextHost : MonoBehaviour`，不修改 `IUIContext` 或 `UIRoot` 的职责。宿主显式引用 Root、Registry，默认在 Start 初始化，也可关闭自动初始化并由业务调用。宿主不查找/创建 EventSystem，不安装驱动，不自动启用可选扩展。

公共表面：

```csharp
public IUIContext Context { get; } // 初始化前为 null；停止后保留诊断用引用
public IUIContext Initialize(Action<UIContextBuilder> configure = null);
public UniTask<UIShutdownCompletion> ShutdownAsync();
```

Inspector 配置 Root、Registry、Initialize On Start，以及全部现有 ContextSettings 的等价字段。超时以秒序列化，转换成 TimeSpan 后仍调用现有验证；默认值与现有设置完全一致。构建后编辑 Inspector 不改变正在运行的快照。只增加宿主的序列化映射，不改造 Core 设置为 Unity 序列化对象。

- Initialize 只在 PlayMode 主线程执行；先应用序列化设置，再调用可选 configure，最后 Build。定制代码必须在自动 Start 前调用，或关闭自动初始化。
- 成功后重复无参 Initialize 返回同一实例；再次传 configure 拒绝并提示，避免配置被静默忽略。Build 失败保留原异常与回滚结果，允许显式修复后重试，不自动循环。
- Shutdown 首次请求即封闭再次初始化；未初始化时返回空完成结果。多次调用共享终态，遵守既有 UniTask 消费约束，不能重复 await 普通内部任务。
- 禁用组件不隐式 Shutdown，重新启用不重建 Context。Root 禁用继续走既有可用性逻辑。
- Destroy/退出 PlayMode 触发自身 Context 的现有 Shutdown；不阻塞 Unity 回调，不等待超出原 ShutdownBudget 的无限清理。保留并展示 Errors 与 PendingCleanupCount。
- Root 与宿主同时销毁时合并清理请求；不重新解释 Engine 给出的关闭原因，不保证 Unity 对象回调先后顺序。
- 同 Root 的外部 Context 或另一个宿主由 Builder 原有唯一性校验拒绝；失败宿主不得关闭、接管或释放已有 Context。
- 不接管注入依赖的所有权。configure 使用 WithServiceFactory 时仍按既有 Context 所有权；共享 Loader/Arbiter 等仍由注入方持有。
- 不添加 DontDestroyOnLoad、全局默认 Context、自动重启、View 自动打开或 Show/Hide API。
- 连续进入退出 PlayMode，包括域/场景重载开关组合，不复用旧 Context，不遗留静态引用、订阅或锁；仅为宿主补充必要的会话重置，不改 Engine 契约。

工作台场景页新增显式“安装/更新 Context 宿主”操作；不改变原“仅配置场景”行为。冲突先检查，再一次 Undo 组应用；只标脏、不自动保存。已有外部启动脚本不能可靠自动识别，UI 明示同 Root 只能有一个装配者，运行时唯一性校验兜底。

## 独立预览

工作台新增“预览”页，保持原四页功能。入口要求生成/编译/写回已完成、输入指纹有效；调用既有只读验证，失败指向生成页，不自动生成或修改注册。

Editor 资产 `UIPreviewProfile` 明确记录 Registry、UIViewTypeKey、输入驱动 MonoScript、可选 Root 模板 Prefab和可选 `UIPreviewAdapter`。普通界面使用默认适配器行为；高级 Editor ScriptableObject 继承适配器：

```csharp
public virtual void Configure(UIContextBuilder builder);
public virtual UIArguments CreateArguments(); // 默认 None
public virtual void Release(); // 释放适配器自己创建的共享依赖
```

适配器方法仅在 PlayMode 主线程调用；实例按一次预览运行克隆，configure 后由宿主 Build。使用 `UIRegistration.Accepts` 校验参数，带参缺提供者/类型不匹配时拒绝 Open；typed null 必须通过 UIArguments.From<T> 明确构造。不提供任意对象 JSON 反射编辑器。Release 每个运行实例至多一次；即使 Build/Open 失败也执行，异常不得覆盖主错误。预算终态存在 PendingCleanup 时，不能立刻释放仍供迟到任务使用的共享依赖；这种依赖由适配器自行遵循既有迟到清理所有权，必须在文档明确说明。

预览运行流程：

1. EditMode 预检；已在 PlayMode、编译中、作者操作未完成或另一预览活动时拒绝启动。驱动仍明确选择 Legacy 或已安装适配包脚本，不扫描安装后端。
2. 先保存/取消当前脏场景；保存对话框取消则不创建预览资产、不修改布局。检查 Prefab Stage，要求退出 Prefab Stage 后重试，避免丢失未保存 Prefab 内容。
3. 保存原场景布局、活动场景、选择与运行请求身份到 Library 下独立状态文件；不与 P0 authoring.json/Pending 混用。
4. 在工具拥有的 `Assets/Wulato.UIKit.Preview/<request-id>/` 建立独立临时场景，包含 Camera、Root、EventSystem 与明确驱动。复用 UISceneAuthoring 和样例 Camera 规则；业务场景不参与运行，不修改 Build Settings/ProjectSettings。
5. 进入 PlayMode，由 Editor 控制器克隆适配器，调用宿主 Initialize，按现有 Registry 和 `OpenAsync(key, args)` 打开；不直接 Instantiate View 绕过注册，不强制改变 Single/CacheOne 策略。Root 模板必须含唯一 UIRoot，不允许自带业务启动组件或另一个 EventSystem/Host。
6. UI 展示阶段、异常、关联资产、Context/Session 身份与诊断；“关闭”“再次打开”串行执行，同一 Context 重开得到新 Session。View 已自行关闭时更新按钮状态，不拿已关闭句柄重复控制缓存 View。
7. “停止预览”先取消尚未完成的打开，调用宿主 Shutdown，记录有界终态后退出 PlayMode。用户直接停止、编译或窗口关闭同样进入清理路径；窗口关闭可先清理后恢复，不留后台预览。
8. 确认已回到 EditMode后恢复原布局及选择。只删除带本请求所有权记录且未被用户修改的临时资产；用户修改的内容保留并定位提示。取消/重载恢复不得擅自覆盖用户后来打开的其他场景。

恢复规则：状态文件只保存 GUID/路径、步骤及请求身份，不保存运行时对象引用；崩溃/重启后的未完成请求只提供“恢复原场景”“保留现场”选择，不自动再次进入 PlayMode。恢复或删除前重新核验资产身份及布局所有权。预览进行中发生脚本重载则终止此次运行并恢复，不自动重新打开业务界面。

## 验收边界

先独立验收宿主，再验收默认无参预览，最后验收参数/自定义依赖与恢复。固定 Unity 2022.3.62f2、6000.3.22f1，两种输入后端；使用现有 runner 新增专项及原始证据，不新建执行器。

宿主的四种 Reload 组合仅验证新宿主的连续两次 PlayMode，不扩展成全项目完整 Reload 矩阵。真实 Editor 进出模式、保存取消、窗口关闭、崩溃恢复不能仅用普通 PlayMode 单测冒充，必须有独立消费工程操作记录。

本轮不含静态预览、业务场景多 Context 预览、任意参数自动造数、可选包安装、导航/弹窗新增能力及 Player/移动端验收。文档不将 P0 原有证据描述为 P1 通过证据。
