# Wulato.UIKit P1 易用性改进实施计划

> **Superseded（实施任务）**：后续工作仅执行 [Master Optimization Plan](2026-11-03-wulato-uikit-master-optimization.md)。已完成成果、历史记录及其行为契约保留。

> **For agentic workers:** REQUIRED SUB-SKILL: 使用 superpowers:executing-plans，由当前 Agent 连续实施；不启动子代理，最后集中自审一次。用复选框记录实际进度，不自动提交、推送或发布。

**Goal:** 先用 Context 宿主降低装配成本，再通过工作台隔离运行单个已注册界面，并保留现有运行时契约。

**Architecture:** 宿主位于现有 UGUI 程序集，包装 Builder/Shutdown，不新增 Core 调度路径。预览位于 Editor Authoring，使用临时场景和真实 PlayMode，复用宿主、注册、场景配置和输入校验。

**Tech Stack:** 现有 Unity UGUI、UniTask 2.5.11、EditorWindow、现有 Python 验证 runner；不增加依赖。

**Spec:** [P1 设计](../specs/2026-11-03-wulato-uikit-usability-p1-design.md)，现行核心契约见 [主设计](../specs/2026-10-31-wulato-uikit-design.md)。用户已于 2026-11-03 确认首版采用真实 PlayMode 隔离预览。

## 全局约束

- STANDARD：既有架构内新增装配与工具层；如需修改 Core 生命周期或公共接口，停止该项并重新评估受影响决策。
- 保留 View/Session、Single、缓存、FIFO、取消、ShutdownBudget、迟到清理及注入依赖所有权。
- 保留工作区已有修改；不改根 ProjectSettings、依赖版本和锁文件；只逐项添加本任务文件。
- 固定验证编辑器 2022.3.62f2、6000.3.22f1，分别使用 Legacy/Input System 验证宿主。
- 新 Unity 资产附 .meta，保留已有 GUID；测试输出保存到 Artifacts，零匹配/跳过/证据缺失均失败。
- 同因最多两轮自动修复；不运行 suite all、性能专项、Player 或移动端构建。

## 集中审查重点

1. 外部 Builder 已占用同一 Root：新宿主失败不能终结外部 Context（T1）。
2. Root/Host 销毁与用户停止同时发生：清理幂等，终态和迟到清理真实可见（T1/T4）。
3. 参数为 typed null、提供者类型错误或依赖构建失败：禁止绕过注册及漏释放（T3/T4）。
4. 脏场景取消或用户已切换布局：恢复操作不能撤销用户后续编辑（T3/T4）。
5. 禁用 Reload 后第二次运行与编译中断：不沿用旧 Context/请求，不自动重开（T1/T4）。

## 文件分工

以下为计划新增名称；现有目录中的 asmdef 延续现有程序集边界。

| 文件 | 职责 |
| --- | --- |
| Runtime/UGUI/UIContextHost.cs | Inspector 引用、装配入口、单实例所有权及退出观察 |
| Runtime/UGUI/UIContextHostSettings.cs | 可序列化秒值与现有 UIContextSettings 的映射 |
| Editor/Authoring/UIContextHostInspector.cs | 配置验证、运行状态及关闭结果 |
| Editor/Authoring/UIPreviewProfile.cs | Editor 专属配置资产及 UIPreviewAdapter 扩展点 |
| Editor/Authoring/UIPreviewOperation.cs | Library 请求持久化、阶段、资产身份和恢复记录 |
| Editor/Authoring/UIPreviewController.cs | 场景切换、PlayMode 运行、命令与清理 |
| Editor/Authoring/UIPreviewPage.cs | 预览页面 UI；操作逻辑不写入窗口 |
| Tests/PlayMode/ContextHostTests.cs | 宿主装配、所有权、销毁与重复运行 |
| Tests/EditMode/PreviewAuthoringTests.cs | 配置、场景准备、取消和恢复策略 |
| Tests/PlayMode/PreviewRuntimeTests.cs | 真正打开/关闭、参数和失败清理 |

表中路径均相对 `Packages/com.wulato.uikit/`。新增文件及文件夹维护 .meta。Editor 类型测试沿用既有 AuthoringTests 的程序集访问方式。

## T1（P1.0）：独立交付 Context 宿主

**接口：** 产生设计定义的 Context/Initialize/ShutdownAsync；消费现有 UIContextBuilder、UIContextSettings 和 UIShutdownCompletion。

- [ ] 在现有 runner/matrix 注册 PlayMode `context-host` 专项、必需用例及证据检查。先写失败用例：Start 自动构建、关闭自动初始化后显式构建、configure 注入服务、同实例重复 Initialize、二次 configure 拒绝、构建失败显式重试、非 PlayMode/非主线程拒绝。
- [ ] 最小宿主运行 RED，保存命令、退出码和用例匹配证据；不存在组件/功能失败才是预期，不把编译器或环境失败记为行为 RED。
- [ ] 实现 Host 与设置映射；覆盖默认 30/10/2/2/2/5 秒、缓存 8、Basic；拒绝 NaN/Infinity/非正秒值及转换溢出，交由现有设置校验确认快照。
- [ ] 补齐并实现所有权用例：重复 Root 不接管外部 Context；共享 Loader 不被 Dispose，工厂服务按原语义逆序释放；禁用/启用不重建；Shutdown-before-Initialize 不再启动；停止后不自动重启。
- [ ] 补齐并实现销毁用例：Host-only、Root-only、两者同帧、未完成 Open、关闭异常与超预算迟到任务；重复 Shutdown 得同一终态，不重复 await 单次任务，PendingCleanupCount 不隐藏。
- [ ] 为四种 Reload 设置各验证连续两次 PlayMode 的宿主身份、Session/输入锁清零；使用现有 Reload 验证基础设施（先定位再复用），仅增加宿主夹具与必需证据，不另写执行器。若普通 PlayMode 用例无法真实改变进出模式，使用既有 Editor 阶段编排并单列证据，不能伪装成单测覆盖。
- [ ] 最小宿主 GREEN 后运行受影响宿主矩阵；更新 Lifecycle 中宿主所有权/退出说明。

**退出条件：** 无工作台即可通过 Inspector 装配和业务 `host.Initialize().OpenAsync<T>()` 使用；失败、重复运行和退出均有上述行为证据。无需预览即可独立交付。

## T2（P1.0）：工作台宿主安装与最短接入

**修改：** `Editor/Authoring/UISceneAuthoring.cs`、`UIAuthoringWorkbench.cs`、新增 Host Inspector；`Tests/EditMode/AuthoringTests.cs`；`Documentation~/QuickStart.md`、`Lifecycle.md`。

**接口：** 新增可直接测试的 `UIContextHostAuthoring.Apply(UIRoot root, UIRegistry registry)`，返回安装/更新的 UIContextHost。与原场景配置操作分别触发，调用前完成冲突校验。

- [ ] 写失败测试：首次安装、同宿主重复更新、Root/Registry 缺失、跨场景引用、同 Root 两宿主、不同 Root 共存；失败不留下半套组件。
- [ ] 最小 `authoring` RED 后实现显式安装按钮、对象定位、已有装配者提醒；只对选定场景生效，一次 Undo 组，不自动保存。
- [ ] 补齐 Apply/Undo/Redo、非目标对象不变及运行中禁止编辑装配的测试；在 Host Inspector 展示 Context 是否建立、错误与 Shutdown 结果。
- [ ] 文档提供 Inspector 自动启动及手动 Initialize(configure) 两条路径，说明业务 Awake 时序、configure 必须早于自动 Start、可继续直接使用 Builder。
- [ ] 用新增测试样例演示宿主；不改写全部已导入样例、Usage partial 或消费者源码。运行 `authoring` 专项及 T1 的直接相关回归。

**退出条件：** 空场景配置后可明确安装宿主并运行；原仅配置场景与手动 Builder 流程保持有效。

## T3（P1.1）：默认独立预览闭环

**修改：** 新增 PreviewProfile/Operation/Controller/Page，工作台接入第五页；复用 UISceneAuthoring 和 UISampleAuthoring 的 Camera 创建逻辑，必要时仅抽出共用 Camera 方法；不复制生成流水线。

**接口：** `UIPreviewController.Start(UIPreviewProfile profile)`、`CloseAsync()`、`ReopenAsync()`、`StopAsync()`；操作服务为 Editor internal，窗口仅展示状态与提交命令。Profile 和 Adapter 为 Editor public 扩展点，签名见设计。

- [ ] 注册 EditMode `preview-authoring` 与 PlayMode `preview-runtime` 专项、必需列表和原始证据；写失败测试：未完成生成/过期输入拒绝、未注册 key、错误驱动、非合法 Root 模板、正在编译/PlayMode/作者操作中拒绝、多次 Start 不并行。
- [ ] 最小 RED 后实现只读预检、Profile Inspector 和选中已生成界面创建 Profile 入口；创建资产必须由用户显式确认路径，不覆盖文件，不写 Registry。
- [ ] 写取消/准备失败测试：脏场景保存取消、Prefab Stage 拒绝、创建 EventSystem 失败、Camera/Root 缺失；断言原布局、场景内容和生成输入未变。
- [ ] 实现场景准备与请求持久化；临时路径按 request-id 隔离，记录 GUID/内容指纹及原场景 setup/选择；状态持久化失败不得进入 PlayMode。
- [ ] 实现真实 PlayMode 运行：构建宿主、无参 Open、显示会话状态、关闭后重开；按钮命令串行且可取消，不添加防重复打开运行时 API。
- [ ] 增加参数与依赖测试：正确 TArgs、typed null、None/type mismatch、Configure/Build/Open/Release 异常；验证错误上下文、工厂与共享资源所有权。高级适配器提供外部 Loader 的组合测试，不要求安装 Addressables 才能预览普通界面。
- [ ] 同 Context 重开验证 SessionId 变化；分别测 Release 与 CacheOne，不把 CacheOne 重用 View 当作同一 Session。
- [ ] 最小 GREEN；先检查实际匹配和证据，再补齐四宿主专项。更新预览文档并列明业务服务仍需适配器。

**退出条件：** 单独已注册无参界面从工作台可见、可交互、可关闭重开；带参/外部依赖有显式可运行扩展路径及失败说明。

## T4（P1.1）：退出、重载及编辑现场保护

**修改：** PreviewOperation/Controller；PreviewAuthoringTests/PreviewRuntimeTests；如需真实 Editor 阶段编排，扩展现有验证准备阶段与记录 schema，不创建另一 runner。

- [ ] 写失败测试：准备阶段取消、打开中停止、打开失败后停止、窗口关闭、用户直接退出 PlayMode、编译中断、重复停止、恢复步骤再次执行。
- [ ] 实现统一停止路径：取消打开→现有 Shutdown 有界完成→记录 Errors/PendingCleanup→退出 PlayMode→恢复场景；所有异常仍执行后续必要清理，原始错误不丢失。
- [ ] 验证 Reload 开关组合：只覆盖此次预览请求恢复/终止，重载后不自动重开；与 T1 共用已有阶段证据避免重复启动无关矩阵。
- [ ] 写失败恢复测试：临时资产被修改、路径同名 GUID 不同、状态文件损坏、原场景丢失、用户已切换布局；断言不删除用户资产、不静默覆盖布局，提供定位与恢复/保留选择。
- [ ] 在独立消费工程实际操作“有脏场景→取消”“启动→关闭窗口”“启动→Stop”“启动→编译”“重开编辑器→恢复/保留”；用可控未完成状态模拟重启恢复，不以强制杀进程冒险替代必要证据。
- [ ] 验证结束恢复 Editor 原 Reload 设置与布局，不提交 ProjectSettings 改动；扩展工具时先运行对应 Python 单测。

**退出条件：** 各停止/恢复入口均不留活动 Context 或输入锁；允许 PendingCleanup 的路径如实报告，不把有界终态当成全部异步任务已物理释放。

## T5：集中审查与交付

- [ ] 对照五项审查重点逐项检查所有权、程序集依赖、GUID、Undo、用户源码保护、静态订阅及任务消费。
- [ ] 稳定输入后完成以下相关专项；已有同指纹通过记录不重复执行，不通过的不能用历史 P0 证据顶替。
- [ ] 更新 QuickStart/Lifecycle，新增 `Documentation~/Preview.md` 及 .meta；记录命令、版本、匹配数、结果与原始证据目录。
- [ ] 交付实际通过项、未验证项及环境限制；不声称全项目/Player/移动端验收通过。

## 验证命令与门禁

实施各任务先用 `--editor 2022.3.62f2 --backend legacy` 的相应专项 RED/GREEN；新专项注册后才能运行。最终分别完成：

```bash
python3 Tools/Verification/run.py tests --editor all --backend all --mode PlayMode --suite context-host
python3 Tools/Verification/run.py tests --editor all --backend all --mode EditMode --suite authoring
python3 Tools/Verification/run.py tests --editor all --backend all --mode EditMode --suite preview-authoring
python3 Tools/Verification/run.py tests --editor all --backend all --mode PlayMode --suite preview-runtime
python3 Tools/Verification/run.py tests --editor all --backend all --mode PlayMode --suite input-runtime
```

input-runtime 用于宿主退出和独立 EventSystem 对输入所有权的直接影响；只有实际修改清理/会话实现时才增加对应 cleanup/sessions 专项，不能默改 Core 后仍沿用本范围。

四个独立干净消费工程各完成：仅主包与相应输入适配，默认无参及带参预览、按钮关闭与同 Context 重开、脏场景取消、停止后恢复布局；收集截图、Editor 版本、Console、诊断和资产差异。四种 Reload 组合在两编辑器 Legacy 宿主执行；Input System 宿主另跑默认及双禁用组合，重点核对旧模块/锁未残留。这是新宿主/预览专项组合，不扩展为项目完整 Reload 验收。

若改 Python 工具，先跑相关现有 unittest；确需工具完整回归才使用项目规定的 discover 命令。所有命令通过现有 runner 保存原始输出，并将终端摘要输出重定向到本任务 Artifacts 日志；零匹配、跳过、缺失证据或输入指纹变化不得计为通过。

## 当前进度

- [x] 核对 Builder 的显式装配、Root 唯一性、设置快照、Shutdown 有界终态及 P0 操作边界。
- [x] 形成按宿主→工作台→预览→恢复的实施与验证任务。
- [x] 用户确认预览模式：真实 PlayMode 隔离预览。
- [ ] T1–T5 实施及真实验证；本轮尚未开始，也未执行 Unity/Python 验证。
