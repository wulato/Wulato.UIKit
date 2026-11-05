# Wulato.UIKit P0 易用性改进实施计划

> **Superseded（实施任务）**：后续工作仅执行 [Master Optimization Plan](2026-11-03-wulato-uikit-master-optimization.md)。已完成成果、历史记录及其行为契约保留。

> 使用 superpowers:executing-plans，由当前 Agent 连续执行、集中自审，不启动子代理、不创建 worktree、不提交或发布。授权依据：本次用户完整批准的 P0 计划。

**目标：** 统一 Editor 工作台完成场景、界面、生成、样例配置；不改变运行时契约。
**依据：** ../specs/2026-10-31-wulato-uikit-design.md；本聊天批准的 P0 计划。
**约束：** 保留根 packages-lock、ProjectSettings、AGENTS.md、.vscode 修改；只改 Editor/样例/相关验证与文档。验证版本 2022.3.62f2、6000.3.22f1，legacy/input；不运行 all suite、性能、完整 Reload 或 Player。

## 任务

- [x] T1 文档：最短接入路径、实际扩展状态；随实现更新最终操作步骤。
- [x] T2 工作台：场景/界面/生成/样例；复用完整生成集合、Pending/指纹/所有权，跨重载状态、错误资产定位、幂等重试。
- [x] T3 场景：显式 Root/Registry/EventSystem/驱动；冲突阻止、Undo、仅标脏不自动保存，不自动发现后端。
- [x] T4 界面：新建/已有普通 Prefab；无参/已编译参数类型，跨编译请求，GUID/用户文件保护；Variant 拒绝，取消不删资产。
- [x] T5 样例：QuickStart 单独运行，默认版本/旧目录兼容，按脚本身份解析，歧义显式选择；一次配置跨编译，Usage 冲突拒绝，场景布局恢复，旧批处理兼容。
- [x] T6 集中自审、专项验证、进度/证据与交付。

## 验收

新增 authoring EditMode 专项：生成重入/过期/错误恢复，场景冲突/非法驱动/Undo/幂等，界面标识/类型/绑定/文件保护，样例目录/歧义/源码冲突。
输入稳定后运行四宿主 authoring/editor EditMode、prepare-samples、samples/generated-integration PlayMode、prepare-samples --check-only、check-generated；仅相关 Python 用例。四个独立干净消费工程仅导入 QuickStart，默认版本目录，无其他两样例程序集，验证生成、关闭、重开、重载、重复配置并保留原始证据。

## 实施裁定与记录

- 开始：分支 codex/p0-p1-preflight，只有已知四项既有差异。复用当前 checkout；以本计划兼任进度账本，不另建 ledger。
- P0 的场景 Registry 选择只提供配置/可复制 Builder 用法，不新增运行时持有组件。输入适配通过显式 MonoScript，不扫描可选包。
- T2/T4/T5 共用 Editor 持久请求和编译续跑调度；旧生成 Pending 仍是写回权限依据。T3 被样例场景配置复用。

- RED：10项新增 authoring 用例因服务缺失失败；证据 Artifacts/uikit/20261102T080911177294-tests-7ac8d6。首轮沙箱许可IPC挂起已取消，随后使用授权 runner 重试。

- 证据更正：最初两轮发现新夹具的 Additive 未保存场景前置失败，不能算功能RED。改为独立Single场景后，临时移出新服务再运行，10项均明确报 Authoring service missing（20261102T081547802140-tests-228f68），恢复实现后10/10通过（20261102T081904442935-tests-e0d2fc）。编译曾有 PackageInfo 命名歧义，已限定全名。
- 裁定：单独QuickStart干净接入通过扩展现有 prepare-samples 编排，仍使用 run.unity、run._run_locked、原始证据校验，不创建第二个测试执行器。独立项目只含主包、所选输入适配及独立QuickStart验收包。

- QuickStart最小干净消费验收通过：Artifacts/uikit/20261102T083245002660-quickstart-consumers-3701f2，重复配置指纹相同，实际按钮关闭/新Session重开通过且原始证据复核PASS。
- 资产保护新增负例RED：重复绑定和Editor-only参数未预先拒绝（20261102T083858385375-tests-0a608d）；修复提前校验，并补齐取消时禁止自动写回、现有Marker脚本回填、状态原子保存。

- [x] T1 接入文档已更新；工作台保持无运行时宿主、无新增主包依赖。
- T2–T5 实现及最小宿主已完成，新增15项authoring覆盖；最终专项矩阵与自审待完成。
- 工具完整回归169项通过；追加 check-only 与 quickstart-only 互斥负例RED，避免只读选项触发独立工程写入。

- 四宿主authoring首次：双legacy通过；input三项夹具仍硬编码Legacy（第二次Apply及两个冲突路径），被生产校验正确拒绝。仅修正测试明确传入宿主驱动，不更改生产后端校验；此同因第1轮修复。最终Python170项通过。

- 集中审查补充旧Generate菜单重入负例RED（20261102T084942516135-tests-8dd2ba），新增明确拒绝并让内部工作流使用同一全量生成实现。自审完成后冻结实现，执行最终16项authoring/原editor/样例及生成集成专项。

- 样例准备四宿主全部通过：20261102T085454476426-prepare-samples-c14fc7。
- 恢复路径自审新增两项RED（20261102T090102088964-tests-93fccf）：诊断资产覆盖样例目标、取消会恢复用户后来切换的场景。独立保存sampleDirectory；取消不改场景，真正接线前检查脏场景并捕获当前布局，接线finally恢复。已有打开的样例场景不再关闭；QuickStart第二轮干净验收显式覆盖已打开场景。

## 当前最终专项证据（实现已冻结）

- [x] T2 工作台、持久状态、取消/重入/错误恢复完成。
- [x] T3 场景校验、显式驱动、Undo/回滚完成。
- [x] T4 无参/带参源生成、已有Prefab保护、绑定与路由校验完成。
- T5 代码完成，最终四宿主干净QuickStart验收待完成；先前最小宿主已通过。
- authoring：18项×4宿主=72，`Artifacts/uikit/20261102T090310659955-tests-7c9d84`。
- editor：16项×4宿主=64，`Artifacts/uikit/20261102T090425536191-tests-40e7a6`。
- samples：实际6项×4宿主=24（必需5项/宿主），`Artifacts/uikit/20261102T090807918277-tests-d35e70`。
- generated-integration：3项×4宿主=12，`Artifacts/uikit/20261102T090921098633-tests-003a4a`。
- 上述16组合已通过 evidence.verify 当前快照、XML/逐例trace/索引/指纹/锁文件/库存原始证据复核，零缺失。
- Python工具完整170项通过（`/tmp/uikit-authoring-python-final.log`）；后续QuickStart/样例编排修改的相关9项通过（`/tmp/uikit-authoring-tools-targeted.log`）。


## 最终交付

- [x] T5 四个独立干净消费工程通过，仅导入默认版本目录 QuickStart；无 Modals/Inventory 程序集。每宿主8个准备阶段，包括已打开样例场景下重复配置；两轮完整输入指纹一致。
- [x] T6 集中自审与交付完成；无未解决代码失败。同类失败遵守最多两轮修复，夹具/环境失败与功能失败分别记录。
- 最终20组合共176个Unity测试通过（authoring72、editor64、samples24、generated-integration12、quickstart-only4），全部经 evidence.verify 当前快照/原始XML/逐例trace/库存/索引/哈希复核通过。
- 干净消费准备及逐例证据：[QuickStart验收](../../../Artifacts/uikit/20261102T091355630203-quickstart-consumers-5f4ae2/acceptance.json)。
- 四宿主样例只读：[准备校验](../../../Artifacts/uikit/20261102T091102966315-prepare-samples-b5b71f/summary.json)；生成只读：[check-generated](../../../Artifacts/uikit/20261102T091233548356-check-generated-0e2973/summary.json)。
- [最终机器汇总](../../../Artifacts/uikit/authoring/acceptance.json)及CLI/Python摘要日志位于 Artifacts/uikit/authoring。完整Python170项及后续相关9项通过。
- 既有 prepare-samples 在 u2022-legacy 的宿主 manifest/lock 中补挂现有 acceptance.samples 包，这是样例专项需要的验证宿主配置；没有改变根 manifest、依赖版本、根锁文件或根ProjectSettings。
- 新资产meta成对，未移动现有Unity资产；保留最初根锁、ProjectSettings、AGENTS.md、.vscode差异。不提交、不推送、不发布。
- 范围限制：仅本轮Editor制作、干净接入及直接相关运行专项；未进行工作台真人交互截图验收、Player、移动端或IL2CPP发布验收。


## 实际交互验证进行中（2026-11-02）

- 用户授权直接操作已打开的 Unity 2022.3.62f2 根工程，测试资产放 Assets/WorkbenchValidation；不改根ProjectSettings/依赖，不提交。
- CUA 的 Unity 主窗口/UIElements 内容坐标点击报 windowNotFoundAtPosition，Workbench IMGUI浮窗及原生菜单/键盘可用。QuickStart 导入使用验证菜单调用官方 Sample.Import API，后续工作台动作直接点击；运行 Close 使用 Game 焦点下真实 Return/Submit。
- 已实际通过：默认目录样例识别、预览/应用、编译续跑、保持已打开样例场景重复配置；无参View创建；已有Prefab回填/生成；空Target绑定失败提示及修复后重试。
- 发现并修复：Overlay样例无Camera导致Game提示遮挡（幂等背景Camera）；编译重载后缓存Editor失效引发NullReference/GUILayout错误（改用新建SerializedObject绘制）；自动选中新Prefab但未回填脚本（统一SelectPrefab及完成请求同步）；重试未保存Marker导致内存/磁盘绑定不一致（工作台重试/生成先SaveAssets）；设置720×440最小窗口尺寸。
- Camera负例RED：20261102T132625591275-tests-8d4204；双编辑器legacy authoring19项/宿主通过：20261102T132742823967-tests-ccc9a7。
- Inspector回填RED：20261102T134752620363-tests-fbf2a2；第一轮使用受保护Unity API编译失败，改用公开SerializedProperty迭代器后双编辑器legacy authoring20项/宿主通过：20261102T135159035145-tests-7bd9d4。
- 修复后Game无No-camera遮挡；真实Submit关闭已确认Session1→0、租约1→0。快照及Game截图位于 Artifacts/uikit/authoring-interactive/observed-20261102T135838389.json 与 observed-20261102T135905209.json。
- 当前剩余：同Context重开（显式验证菜单调用Demo公开OpenAsync，需注明与Inspector上下文菜单不同）；场景Apply/Undo/Redo；取消保护。辅助脚本 Assets/WorkbenchValidation/Editor/WorkbenchValidationSetup.cs 仅导入/创建空测试场景/调用已有Demo公开入口/读诊断，不替代工作台UI操作。


## 实际交互最终结果

- 本轮实际交互完成；前文“进行中/剩余”均为历史记录，以本节及最终报告为准。
- 已完成同Context重开（1→0→1，Session1→2）、场景Apply/Undo/Redo（0→2→0→2且不自动保存）、编译准备阶段取消（脚本保留、Prefab未生成、重载后场景不变）。
- 上述修复全部实际复验；最终定向回归双编辑器legacy authoring40+samples12=52项、4组合通过，并经当前原始证据复核。没有刷新完整历史矩阵。
- 根ProjectSettings/manifest/锁文件SHA-256与本轮开始时完全一致；未提交、推送或发布。
- [人工交互报告及明确限制](../../../Artifacts/uikit/authoring-interactive/summary.md)、[机器证据汇总](../../../Artifacts/uikit/authoring-interactive/acceptance.json)、[重开截图](../../../Artifacts/uikit/authoring-interactive/game-20261102T140741118.png)。
- 工具替代及未验证：导入使用Unity官方API；Close为真实键盘Submit，重开调用Demo公开OpenAsync；鼠标点击/Inspector上下文菜单、Unity6与InputSystem人工操作、切换另一脏场景的人工取消竞态、Player/移动端未验证。现有相关专项不扩大为上述人工/发布验收。

- 收尾：只移除本轮Unity新生成的SceneTemplateSettings.json（已备份），根设置/依赖保留原状；Editor退出PlayMode，保留验证资产便于复现。


## Git归档授权（2026-11-02）

- 用户明确要求“提交git”，授权本地提交本轮实现、验证工具/用例、根交互验证资产/生成结果及验收摘要；覆盖此前不提交的约定，不推送或发布。
- [可随仓库审阅的验收摘要](../../../Verification/Reports/2026-11-02-authoring.md)。提交前再次复核最终4组合52项原始证据通过，不重复运行Unity。
- 排除根锁文件、根ProjectSettings、AGENTS.md、.vscode既有修改；按文件显式暂存，保留所有资产GUID和meta。
