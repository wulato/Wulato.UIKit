# Wulato.UIKit 全新优化实施计划

**状态：M0–M6 工程交付完成；M6 快照完整性已修复并完成干净安装补验（2026-11-05）。正式移动发布门禁未执行。**

2026-11-05 后续 CI 配置补强已完成 Python/静态检查；新独立宿主的实际 Unity 编译、专项/Reload 与远端上传链路尚未运行，不能沿用旧宿主结果宣布新 CI 已验收。

> 当前唯一有效的优化实施计划。使用 superpowers:executing-plans，由当前 Agent 连续实施，不启动子代理，不自动提交、推送或发布。

## 目标与生效规则

以 codex/p0-p1-preflight / 427064d 为起点，降低安装、业务接入和验证成本；保留生命周期、Session、FIFO、取消、缓存、资源所有权及迟到清理契约。历史计划 Superseded；已完成成果与验收报告保留，不重写历史证据。正式移动发布另设门禁。

开发默认 Unity 2022.3.62f2，不再固定双版本矩阵；额外版本按兼容目标显式选择，记录准确版本。all 表示当前配置目标集合，不表示所有已知版本。保留 UniTask 2.5.11，不自动改变 UPM 最低版本、第三方依赖或已有项目编辑器。

## M0 基线与计划切换
- [x] 记录工作区、包版本、程序集依赖和验证入口；在本计划维护保留/待完善/待审计清单。
- [x] 废弃旧任务入口，更新 AGENTS、验证说明及文档。
- [x] 分离默认开发编辑器与可选兼容目标配置。
验收：后续任务关联具体源码和验证范围；不重新开发已有成果。

## M1 增量验证
- [x] 扩展 ci_suites/runner/CI；输出建议及 Suite、编辑器、后端、准备步骤和理由。
- [x] 覆盖基线提交差异、暂存、未暂存及相关未跟踪文件；未知相关路径提示。
- [x] 默认 2022.3.62f2/legacy，输入适配选 input；额外版本显式选择，缺配置报错。
- [x] 补齐 Authoring、生成、元数据、夹具及工具映射；CI 只准备选定范围。
- [x] 记录准备/编译/测试/汇总耗时，比较相同代表输入；保留锁、指纹、必需用例、原始证据及两轮失败上限。
验收：文档不启动 Unity，局部改动不扩展无关组合；相关 Python 测试与选择/准备行为有证据。

## M2 包整合
- [x] Navigation/Presentation/Dialogs/PopupQueue 的 Runtime、Tests、Documentation 和 BusinessFlow 移入主包；保留类型、命名空间、程序集及 GUID，Core 不反向引用。
- [x] 移除旧业务包，不保留空壳；说明一次性移除旧引用并升级的迁移路径。
- [x] 主包及 Input System 0.4.0、Addressables 0.2.0；适配器独立，第三方依赖不升级。
- [x] 更新生成/样例/探针/宿主/选择/安装假设；业务 suite 随主包适用。
- [x] 安装范围 main/addressables/all；旧单业务包范围明确报迁移错误，新证据绑定新身份。
验收：默认编辑器两后端业务专项、transitions/flow-integration/flow-samples、直接生成/Authoring/API/适配回归；基础安装不强制可选 SDK。

## M3 Context Host
- [x] UGUI 层新增 UIContextHost，仅包装 Builder/Shutdown：Context、Initialize(Action<UIContextBuilder> configure = null)、UniTask<UIShutdownCompletion> ShutdownAsync()。
- [x] Inspector 配置 Root/Registry/自动 Start/现有 Settings 等价字段与默认校验；成功重复无参 Initialize 返回同一实例，重复 configure 拒绝，失败可显式重试。
- [x] 首次 Shutdown 禁止再初始化；禁用不关闭；销毁/退出有界清理，重复请求共享终态，保留错误与 PendingCleanup；不接管外部资源或自动装驱动。
- [x] 工作台显式安装/更新，Undo、失败不留半成品、不自动保存；QuickStart 两条接入路径。
验收：两输入后端 context-host、直接 authoring/input-runtime；Legacy 四 Reload 组合及 Input 默认/双禁用连续两次，无旧 Context/订阅/锁。

## M4 PlayMode 独立预览
- [x] Editor UIPreviewProfile：Registry、Key、输入驱动、可选 Root 模板及 Adapter；Adapter Configure/CreateArguments/Release 按运行克隆，遵守参数和共享所有权。
- [x] 只读生成/注册/参数/配置预检，临时隔离场景通过 Host/Open 运行；关闭/重开串行，新 Session，显示诊断。
- [x] 保存取消即终止，Prefab Stage 拒绝；持久化布局/选择/请求身份，停止/关闭窗口/编译/退出统一清理恢复。
- [x] 只删除身份及内容匹配的工具资产；崩溃提供恢复/保留，不自动重开或覆盖后续编辑。
验收：两后端 preview-authoring/preview-runtime，真实 Editor 退出/恢复记录及上述 Reload；无参/带参/显式依赖、typed null、错误参数、迟到清理、修改资产、损坏状态均覆盖。

## M5 核心审计
- [x] 定向审查 Engine/Facade/Session/Transaction/Cleanup/InputArbiter，复用测试及性能记录；结论记本文件，包含位置/证据/影响/建议。
- [x] 无证据不改；缺陷先复现再修订任务，公共契约改变单独确认，不授权整体重构。
验收：建议可追溯，不以必须修改为目标。

## M6 工程交付
- [x] 统一 README/QuickStart/迁移/预览/扩展/生命周期、API 弃用/版本/Changelog 政策。
- [x] 固定源码快照验证默认编辑器 main/addressables/all 干净安装、生成、样例、实际程序集依赖隔离；区分开发默认/已验证/支持范围。2026-11-03 安装记录的快照漏收样例/文档，完整交付结论由下方 2026-11-05 M6 补验取代。
- [x] CI 专项和显式门禁统一证据规则；集中自审、记录已验收/未验证/环境阻塞。
验收：新项目按文档接入，日常最小验证可解释；不声称移动发布通过。

## 验证与发布
优先模块专项；不因阶段完成启动 suite all、全部编辑器或 Player。新增兼容声明/特定版本修复补对应专项。零匹配/跳过/缺证据失败，日志存文件，输入稳定后验证，不在运行中改输入。正式发布另选明确编辑器集合，执行适用完整 Editor/Reload、Android/iOS ARM64 IL2CPP、实体设备及真实输入门禁；Host 纳入消费夹具，预览仅 Editor，旧移动报告不能替代。

## 基线、决策及执行记录
- 起点：427064d；实施前仅未跟踪 AGENTS.md 与 .vscode/，不清理或重新归类。
- 保留：Core/Session/FIFO/Cache/Lease/Shutdown、四业务模块、Addressables/Input、Workbench/Codegen；待完善：增量选择、包分发、Host、Preview；待审计：核心复杂度/性能。
- 程序集：Core→UniTask；Loading→Core/UniTask；UGUI→Core/Loading/UniTask/UnityEngine.UI；四业务模块→Core/UGUI/UniTask；适配器保持各自 SDK 边界。
- Ruling：复用当前 codex 分支工作区，不新建 worktree；执行记录在本计划维护，不额外创建重复 ledger；遵循用户默认不使用子代理且不自动提交的约束。

### 执行中记录
- M0：旧计划已标 Superseded；AGENTS 和 Verification 入口已切换。编辑器配置在 Tools/Verification/editors.json，默认及 targets 为 2022.3.62f2，6000.3.22f1 仅为可显式选择的已知 profile。
- M1：新增选择/变更发现/目标配置测试；2026-11-03 Python 回归 182/182 PASS，日志 Artifacts/uikit/master/python-regression-2.log。不将此结果作为后续修改的通过证明。
- M2：主包模块已迁移、保留程序集和资产 GUID；新增包结构测试 RED→GREEN。默认 Legacy prepare PASS：Artifacts/uikit/20261103T021051948573-prepare-462674。
- M3：六项宿主测试已建立；自动启动夹具在 UnityTest 前已执行 Start，首次调整等待帧不足以解决，第二轮改为在测试体创建自动宿主；其他五项已通过。最终结果待记录。
- M4：预览作者测试 RED→GREEN：Artifacts/uikit/20261103T022253797727-tests-1370bc。PlayMode 测试已从 Editor 测试程序集移入运行测试程序集；零匹配不计 RED，后续实际匹配失败才作为缺功能基线。
- 环境：受限执行无法连接 Unity LicenseClient，已中断等待并通过正常权限运行既有 runner；不更换编辑器或弱化许可/证据检查。

### M5 定向审计结论（2026-11-03）

| 位置 | 证据与判断 | 处理 |
| --- | --- | --- |
| Core/Sessions/UIContextEngine.cs：SubmissionError/BeginShutdown/GetDiagnostics | 状态及命令提交集中协调；先 Stopping 后取消、拒绝排队请求，关闭共享完成源；停止诊断是终态快照，不能用其 PendingCount 轮询迟到完成 | 保留；预览 Adapter 文档明确共享依赖由真实任务完成驱动释放 |
| Core/Sessions/UISessionHandle.cs | 每次打开独立身份及非池化完成源，缓存实例不复用 Session；原始异步任务经 UIOperationRecord 单一观察 | 保留，不用去掉 Session 概念来简化 API |
| Core/Async/UICommandQueue.cs、UIOperationRecord.cs | 通知 Drain 时仍维持 running，避免同步 continuation 重入；取消竞争消费稳定记录，不二次 await 原始任务 | 保留；没有可复现的冗余协调缺陷 |
| Core/Transactions/UIContextEngine.Pages.cs | 提交门与取消共享 gate；提交后屏蔽调用方取消，未完成步骤等待后再清理；事务输入锁 finally 释放 | 保留，不拆出第二套调度器 |
| Core/Async/UIOrphanRegistry.cs、UIShutdownBudget.cs | 迟到任务回主线程后交付/清理，finally 释放计数；Shutdown 使用共享截止预算，超预算不伪称物理完成 | 保留；Host/Preview 仅消费既有终态 |
| UGUI/Input/UIInputArbiter.cs | 注册租约幂等；撤销时先清候选/锁再重仲裁；generation 防止过期 grant；候选过滤/排序存在 LINQ 分配，但未证实实际瓶颈 | 记录观察，不作风格或猜测式优化 |
| Verification/Reports/2026-11-01-t10.md：性能表与采样边界 | 已有 Editor 50 次循环/模式、真实创建和租约计数；GC 是整个采样窗口，含 Editor/Test Runner，不能解释成单命令分配 | 复用历史测量背景，不声称本轮性能改善，不重跑未改变的 Core 性能套件 |

本轮未修改 Core 运行时实现；无已确认、需要新架构或公共生命周期变更的缺陷。审计范围限上述协调/所有权路径，不宣称逐类完整证明或 Player 性能通过。

### 后续专项与审查记录
- M1：代表输入选择对比见 Artifacts/uikit/master/selection-comparison.json。Navigation/Authoring 的计划测试进程数从 8 到 2，Input 适配从 6 到 3；仅为选择策略与微基准，不声称整轮 Unity 验证中位耗时已按比例下降。每次 Unity 调用新增 timing.json，标明含启动/编译/请求工作的墙钟时间及日志报告的编译样本。
- M2：53 份迁移资产 .meta 内容与 HEAD 对应来源逐一比对一致；默认两后端代码生成 9 个阶段/宿主通过，证据 Artifacts/uikit/20261103T023714576784-codegen-618e9a；样例准备通过，证据 Artifacts/uikit/20261103T025639086891-prepare-samples-525374。
- M3：真实 Host Reload 六组合、每组合两次进出通过，证据 Artifacts/uikit/20261103T030530246620-host-reset-18cecf。不是完整 integration Reload 或发布验收。
- M4：集中检查复现取消回调抛异常导致跳过 Context Shutdown（Running 而非 Stopped）；分开取消和 Shutdown 的异常捕获，保留主错误并仍执行有界关闭。两后端 preview-runtime 通过：Artifacts/uikit/20261103T030429847127-tests-7b41ce，包含 typed null/错参/Configure 失败/取消异常/Adapter 一次释放。
- M4：真实 Editor 初次预览场景切换后 Registry 引用失效，证据 Artifacts/uikit/20261103T030702207265-preview-reset-6485a1。第一轮修复保持配置引用、切换后按路径重新解析 Registry，并补同步准备失败回滚。失败 fixture 先验证 GUID 与文件清单，再归档到 Artifacts/uikit/master/preview-failure-preserved；无所有权记录的空临时目录保留，不假装已安全删除。复验进行中。
- M4 真实预览：配置引用问题解决后，发现初始化时安排的延迟恢复回调会在状态已进入 running 后误判为中断并停止。现改为在注册回调时捕获是否为合法进入请求；新增停止后 EditMode 观察，并用窗口 OnDestroy 处理关闭，避免把域重载 OnDisable 当窗口关闭。六个 Reload 组合、每组两次真实打开/关闭/新 Session/停止/恢复通过，第二轮实际关闭 Workbench 窗口：Artifacts/uikit/20261103T031439472490-preview-reset-62883c。编译中断另做定向验证，不扩大为全项目 Reload。
- M4 实际 UI 保存取消通过：在独立 Unity 验证工程原生保存对话框点击 Cancel，断言脏场景保留、未创建预览、未进入 PlayMode，随后正常两次运行/恢复；证据 Artifacts/uikit/20261103T032334530047-preview-reset-3a244f。初次在启动 executeMethod 内触发时对话框未正常出现，改为等待 Editor 就绪后触发，未将初次失败记为通过。
- M4 实际编译中断通过：现有 play-reset 的 preview action=compile、option=0，观察真实 compilationStarted 和 assembly reload 后恢复，证据 Artifacts/uikit/20261103T032004454106-preview-reset-34a729。夹具初轮漏发编译请求已保留失败记录并修复。
- 集中自审修复：关闭恢复窗口不隐式选择恢复（RED→GREEN）；停止时写入恢复记录失败仍继续清理（真实文件系统阻塞目录 RED→GREEN）；自动构建失败只允许显式重试（RED→GREEN），新场景从 Start 装配，保留场景组件在 Start 后补装配，代际校验避免旧帧回调跨 PlaySession。
- 最终 context-host 两后端专项通过，含加载中销毁 Host/Root、取消及迟到租约一次释放；最终 preview-authoring 两后端专项通过，含损坏记录、用户新增文件、关闭恢复窗口及持久化失败。对应日志 Artifacts/uikit/master/host-final-2.log、preview-authoring-final-2.log；后续模块回归进行中。


## 最终验收与交付（2026-11-03）

- M0：起点七包为主包/Input 0.3.0、四业务包 0.1.1、Addressables 0.1.0；现为主包/Input 0.4.0、Addressables 0.2.0。旧计划统一 Superseded，原始报告未改。开发默认 2022.3.62f2，targets 默认只含此版本，已知其他 profile 不表示本轮已验证。
- M1：最终 Python verifier 186/186 PASS，日志 [python-verified.log](../../../Artifacts/uikit/master/python-verified.log)。Git 提交/暂存/未暂存/未跟踪路径选择、模块/工具/元数据/后端选择、未知目标拒绝与已有锁/证据检查均保留；CI 准备改为按选定范围。远端 GitHub Actions 本轮未触发。
- M2：四业务模块、BusinessFlow 及测试已并入主包，保留程序集名和类型；53 份已移动资产元数据与对应 HEAD 来源逐一相同。contracts/navigation/presentation/dialogs/popup-queue/editor、input-package、transitions/flow-integration/flow-samples/input-runtime/generated-integration、Addressables 三专项均完成适用两后端回归；编译正负探针、生成/写回/路由及只读校验通过。
- M3：最终 context-host 10 例/后端通过；包括显式/自动启动、工厂及共享依赖所有权、外部 Root 占用、失败重试、重复 Shutdown、工作线程拒绝、加载中销毁 Host/Root、迟到租约一次释放、configure/工厂期间请求 Shutdown。最后一种先 RED 复现发布活动 Context，再改为等待装配结束完成共享关闭，已 GREEN；未改 Core 实现。
- M4：最终 preview-authoring 7 例/后端、preview-runtime 2 例/后端（其中包含 typed null、错参、Configure 失败、取消回调异常及一次 Release 等多个场景）通过。真实预览六个 Reload 组合每组连续两次；正常关闭重开、关闭 Workbench、恢复场景、实际保存取消及真实脚本编译中断均有独立 Editor 记录。目录文件清单/内容/资产 GUID 及目录自身 .meta 哈希均参与删除检查；最后的目录元数据改动保护经 RED→GREEN。
- M5：审计结论及源码位置见上表；无新增核心重构或性能优化。历史性能只作背景，本轮不声称性能中位耗时或 Player 性能提升。
- M6：四个固定源码快照干净安装通过：main/legacy、addressables/legacy、all/legacy、all/input。每项均实际生成、编译、校验 Git 来源/版本与 Runtime DLL 引用图；原始文件哈希独立复核通过。prepare-only 的进程退出码 1、status=BLOCKED 仅因未构建 Player，唯一错误为 `prepare-only; no mobile Player built`，install_status=PASS；没有将其描述为 Player 构建通过。

汇总索引：[delivery-evidence.json](../../../Artifacts/uikit/master/delivery-evidence.json)，列出 31 组已通过检查及 4 项干净安装的原始目录。每条证据仍绑定自己的源码指纹；它不是完整发布 Gate。后续仅修改 Host 初始化所有权和预览目录保护，补齐对应专项、Host Reload、真实预览及最新内容集成，不重复未受影响的业务模块专项。

最终集中作者自审完成（按项目约束未启动子代理）：确认主包外部依赖仍仅 uGUI/UniTask、Core 不反向引用业务模块/SDK、程序集/资产 GUID、共享任务消费、失败/迟到清理、Undo/用户文件保护、迁移和版本约束。差异空白检查通过；未修改根 ProjectSettings，默认宿主锁文件由 Unity 实际解析更新。原有 .vscode/ 与未暂存状态保留，AGENTS 仅按授权更新计划/版本政策；没有提交、推送、打 tag 或发布。

未执行：其他 Unity 编辑器、本项目 suite all/完整发布 Gate、Player/IL2CPP 构建、实体移动设备及远端 CI。恢复记录损坏/用户改动有测试，未进行破坏性的强制杀进程崩溃试验。失败及中断记录保留，已确认属于本轮夹具的现场先校验并归档；没有所有权证明的旧空临时目录保留在验证宿主中，不冒充已清除。

## M6 快照完整性补验（2026-11-05）

- 已核对四份历史安装快照：每份有 128 个包文件未进入快照提交，Samples~/Documentation~ 均缺席。原因是隔离快照的 `git add Packages` 受用户全局 `*~` 忽略规则影响；消费端又从快照工作目录复制样例，掩盖遗漏。历史编译/运行时隔离证据保留，但不再作为完整 Git 分发验收通过的依据。
- 修复范围：仅在隔离快照强制收录授权包输入，逐文件核对固定提交的清单与 SHA-256，保存 package-snapshot.json；消费端样例及其 .meta 从固定提交读取，禁止工作目录脏文件或未跟踪文件补齐缺失样例。不修改用户全局 Git 配置、运行时契约及包依赖。
- 针对性 Python：test_build.py 29/29 PASS，包含全局忽略规则、样例/文档/元数据完整收录、缺失/多出/内容变化拒绝、脏工作目录隔离和提交缺失样例拒绝。日志 Artifacts/uikit/m6-snapshot/python-build.log。
- 补验完成：默认 2022.3.62f2 的 main/legacy、addressables/legacy、all/legacy、all/input，均使用现有 phase3 clean prepare-only。三个调用退出码均为预期 1，四项 install_status=PASS、generation_compiled=true、status=BLOCKED，唯一阻塞均为 `prepare-only; no mobile Player built`。现有 assess_install_record 独立复核原始文件哈希、当前源码指纹、状态及阻塞原因，全部无错误。
- 四份快照均完整收录 605 个文件（样例目录内 97、文档目录内 31），逐文件验证固定提交与 package-snapshot.json 一致；快照清单纳入原始证据索引。实际已安装的主包/适配包全部文件再与该提交清单核对 SHA-256：四组合分别 533、585、585、605 个文件一致。每份仍具有自己的固定 Git revision，不将其冒充工作分支提交。
- 新补验索引：[delivery-evidence.json](../../../Artifacts/uikit/m6-snapshot/delivery-evidence.json)。main/legacy 原始目录 Artifacts/uikit/install-checks/20261105T024645915198-2e18b8；addressables/legacy 为 20261105T024847680022-6bce12；all 两后端为 20261105T025131128031-c50505。调用日志为 Artifacts/uikit/m6-snapshot/install-main-legacy.log、install-addressables-legacy.log、install-all-backends.log。安装证据检查的针对性 Python 另有 4/4 PASS，日志 python-install-evidence.log；本次合计运行 33 项针对性 Python 检查，没有重跑全部 Python/Unity suite。
- 2026-11-03 优化已按后续用户授权提交并推送为 87b938e；本次补验不自动提交推送。验证现场已归档并清理，记录 Artifacts/uikit/cleanup/20261105T103659 与 preview-20261105T103903；四个预览目录/元数据 GUID 由原始 Editor 导入日志及历史请求确认。.vscode 文件保留，仅加入本地 .git/info/exclude。以上为后续状态，不改写先前执行记录。
- 未启动全量 Editor、移动发布门禁、额外编辑器及远端 CI；无 Player 构建或实体设备通过声明。修复轮次：快照遗漏原因第一轮修复及专项复验通过，无第二轮修复。本次改动未提交或推送。

## CI 专项链路补强（2026-11-05）

- [x] 修复准备和测试重新扫描工作区导致范围漂移：ci_suites 只生成一次 selection.json，记录基线、HEAD、源码/配置指纹与计划摘要；准备、测试、上传检查均读取同一计划。生成宿主文件不重新纳入选择；计划损坏、源码或配置变化则失败。
- [x] 补齐现有带 Category 的主包测试源文件及 .meta 映射；测试程序集元数据映射其拥有的 Suite。Input、Transition、GeneratedRegistryIntegration 等测试修改不再遗漏自身专项。
- [x] Host/preview/Workbench 相关修改默认两输入后端，加入既有 play-reset 的 context-host/preview scope；Legacy 四种、Input 默认/双禁用，每种连续两次。显式 full 复用同一选择/执行链路，追加 integration/Host/preview Reload 与原 G0；不隐含固定双编辑器集合。
- [x] 专项通过现有 runner 的 host-root 选择入口，从空 Assets 和对应版本 ProjectSettings 准备独立宿主，保留原宿主及源码包的所有权与校验。主包相关专项不安装 Addressables SDK；仅相关范围安装适配/SDK。普通 flow-integration 不生成或导入样例；生成器运行时专项和预览 Reload 仅准备必要生成夹具。样例范围 base/business/addressables/all 保留现有验收程序集必需的三个基础样例，按需增加业务/Addressables 样例。
- [x] 选择计划、Python 日志/退出码、命令日志、准备/测试耗时及失败部分记录保存到 Artifacts。上传前检查执行清单完整性，复用原始证据索引/文件哈希、tests 必需用例/trace/源码快照及 Reload 报告验证。失败仍上传，缺文件报错；排除独立宿主及其 Library，不排除原始验证报告。
- [x] 针对性 Python 共 99 项通过：选择/冻结/执行/上传检查 29，运行时依赖与独立宿主 19，样例 8，runner 6，Addressables 8，QuickStart 3，Reload 5，原始证据 21。Unity 调用仅在 Python 测试中模拟；没有启动编辑器。YAML、5 个 Bash 步骤语法及相关 Python 语法检查通过。记录 Artifacts/uikit/ci-hardening/python-evidence.json、config-static.json；实际 CLI 冻结/只读加载记录 freeze-cli.log、read-cli.log，均未执行计划命令。
- [ ] 后续显式授权后，用新宿主补最小真实 Unity 专项与 Host/preview Reload；重点确认主包无可选 SDK 时编译、生成夹具、样例分组及原始证据上传前复核。当前仅证明配置和 Python 行为，不以模拟进程证明 Unity 通过。
- [ ] 后续显式授权后运行远端专项 CI，下载 artifact 复核完整性和失败材料；本次未触发远端 CI、全量 Unity、移动发布门禁，不自动提交推送。M6 补验已按后续用户授权提交为 dab3a9e，本节为之后的新修改，历史验收继续绑定当时指纹。

本次未遇到需自动修复重试的测试失败；源码未改运行时契约、包版本、第三方依赖或根 ProjectSettings。所有输出仍位于忽略的 Artifacts；不将本次工具源码指纹变化解释为旧模块已重新验收。
