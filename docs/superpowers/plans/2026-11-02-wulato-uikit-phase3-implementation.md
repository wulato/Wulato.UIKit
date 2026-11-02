# Wulato.UIKit 第三阶段实施计划：Addressables 首批适配

> 使用 superpowers:executing-plans；当前 Agent 连续执行，集中自审一次，不启动子代理，不自动创建 worktree。2026-11-02 用户已明确授权执行 P0–P7，包括以下公共契约及版本变更；不授权推送、合并、tag、发布或远端 CI。

**Spec：** ../specs/2026-10-31-wulato-uikit-design.md §3、7.1、9、11、12。
**基线：** f955667；保留根 packages-lock、ProjectSettings、AGENTS.md、.vscode 既有修改。
**目标：** 独立 Addressables 包、本地真实 Bundle、External 描述、生成、缓存/取消/迟到清理及四宿主专项和八项固定 Git 干净接入。

## 已确认契约

- UIViewAssetKind 追加 External，保留 Prefab=0/Resources=1；External(providerId,assetId) 与只读 ProviderId/AssetId；拒绝空白、不 Trim、Ordinal。
- provider 固定 addressables；支持明确 address/GUID，不提供 label/多资源/子资产入口。默认加载器拒绝 External。AddressablesUIViewLoader 无参构造，WithLoader 显式注入，路由旧两类及自身 External。
- 唯一 GameObject location 查询句柄独立释放；LoadAssetAsync<GameObject> 后在非激活父节点手动实例化。每次独立持有句柄；缓存保留，淘汰释放。Dispose 撤销所有权、禁用脱离销毁；PlayMode 实际销毁后释放句柄。重复 Dispose/外部销毁只释放一次，不使用 ReleaseInstance。
- 底层操作单次观察至完成；核心现有取消竞争及 orphan 清理使 FIFO 继续；取消后不迟到创建实例。异步清理独立于 View/UIRoot。
- Marker externalProviderId/externalAssetId 成对填写，默认 Direct；生成描述不直接引用 Prefab，两字段纳入指纹。专属校验在扩展 Editor 中；普通构建只校验。
- 主包及输入适配 0.3.0，第二阶段四扩展 0.1.1 依赖核心 0.3.0，新扩展 0.1.0。Addressables 包最低基线 1.21.21；用户确认宿主矩阵为 2022.3.62f2：Addressables 1.21.21 / SBP 1.21.25，6000.3.22f1：Addressables 2.9.1 / SBP 2.6.1。UniTask 2.5.11、Input System 1.17.0 不变；不自动换版本绕过失败。
- 不改变 View/Session、生命周期、加载接口或导航事务签名。Core 不引用后端；不增加主包可选依赖。

## 执行任务与退出条件

- [x] P0 文档与双编辑器 legacy 前置验证：独立夹具、真实本地 Bundle、Existing Build、非激活顺序及句柄释放；通过后再实现生产加载。
- [x] P1 External 与版本：先契约 RED，再描述/快照/默认拒绝 GREEN；旧资源行为及版本程序集契约通过。
- [x] P2 独立 Addressables 包、加载与租约：成功失败取消、缓存淘汰、外部销毁、迟到完成、重复释放；测试接缝仅内部。
- [x] P3 Marker/生成/Editor 校验：External 不引用 Prefab；过期、Variant/重命名、绑定写回、幂等及旧 Direct 回归。
- [x] P4 既有 runner：白名单/依赖/testables/内容指纹/矩阵/CI 专项；错误版本、过期内容及缺失证据拒绝。
- [x] P5 样例及真实 Bundle 业务组合：Navigation Back/失败、Presenter 缓存新 Session、Dialog 取消、暂停队列 Shutdown。
- [x] P6 固定 Git clean prepare-only：双 legacy main/addressables 四项、四宿主 all 四项；独立消费目录，无 Player 构建。phase3 仅支持 clean prepare-only，不新增 release gate。
- [x] P7 集中审查、直接相关专项复验、文档与证据交付；无未解决代码失败。

顺序 P0→P1→P2→P3→P4→P5→P6→P7；最小验证接入随模块完成。按最新版 AGENTS.md 每一同类失败最多两轮修复。

## 验证要求

四宿主为 2022.3.62f2 / 6000.3.22f1 × legacy/input；legacy 无 Input System。
新增 EditMode addressables，PlayMode addressables-runtime/addressables-integration；先最小宿主 RED→GREEN，再四宿主。
直接回归 EditMode contracts/integration/editor/navigation/presentation/dialogs/popup-queue、input 两宿主 input-package；PlayMode loading-layout/cleanup/sessions/generated-integration。样例后四宿主 check-generated。
工具先定向 Python，输入稳定后集中一次完整 Python 回归。
真实内容使用 StandaloneOSX Bundle、Existing Build、GUID 收录，拒绝 AssetDatabase/Fast Mode 冒充；证据含实际版本、内容目标/哈希/来源/清理轨迹。
所有命令从根目录调用 Tools/Verification/run.py，日志在 Artifacts/uikit/phase3。保留锁、当前指纹、XML、必需用例、索引与哈希检查；零匹配/skip/陈旧证据不算通过。
不执行 suite all、Reload、Player 构建、真机或发布门禁；R3/DOTween/Collections/远端更新/预加载/对象池不在范围。

## 执行记录与裁定

- 2026-11-02：开始执行，工作区差异与两指定编辑器已核对；原修改保留。复用当前 codex/p0-p1-preflight 分支。
- 裁定：以本计划执行记录兼任进度账本，不额外创建重复 TASKS/ledger；按用户要求集中作者自审，不启动技能默认的审查子代理。

- P0：2022 legacy Bundle 构建通过；Unity6 自动解析 SBP 2.6.1 时缺少 IBundleExplictObjectLayout。用户确认保持 Addressables 1.21.21，显式锁定上游声明的 SBP 1.21.23 后复验。初次夹具命名冲突及 PlayMode 零匹配分别修正，保留失败证据。

- P0 复验：2022.3 legacy 的真实 Bundle 前置用例 1/1 PASS（Artifacts/uikit/20261102T032633208235-tests-242b06）。Unity6 显式 SBP 1.21.23 仍解析 2.6.1，真实内容构建失败（Artifacts/uikit/20261102T032543038583-prepare-f115e8/u6000-legacy）。编辑器 PackageManager/Editor/manifest.json 声明 minimumVersion=2.6.1。已请求用户确认双版本 Addressables 矩阵，未开始 P1–P7。

- 用户确认双版本矩阵并要求依据官方 4.0 changelog 判断 Unity6 版本。核对 6000.3.22f1 官方内置 manifest 后选 Addressables 2.9.1 / SBP 2.6.1；2022 保留 Addressables 1.21.21 / 编辑器实际解析的 SBP 1.21.25。2.9.1 的 Unity6.5 条款仅新增 TypeTree 提取，本阶段不使用。替代原统一 1.21.21 约束；新包最低依赖仍 1.21.21。

- P0 完成：双 legacy 真实 Bundle 测试各1项通过，Unity6 证据 Artifacts/uikit/20261102T033025312703-tests-893eaf；2022 见前记录。生产实现门禁已解除。

- P1 最小宿主 contracts RED（三个新契约失败）→GREEN，Python runtime 13/13。四宿主及直接回归在最终输入稳定后集中执行。

- P2 最小宿主 addressables-runtime 9项 RED→GREEN。P3 新生成契约 RED 后实现；旧宿主指纹过期经既有 codegen 九阶段全部通过；专属校验4项通过，测试夹具恢复设置序列化缓存哈希，防止改变验证输入。完整四宿主及最终生成仍待验收。

- P4 定向工具 RED→GREEN，完整 Python 158/158；随后发现 content prepare 会移除已导入的 acceptance.samples，增加保留用例 RED→GREEN（6项 addressables 工具通过），不降低原样例/性能用例覆盖。
- P5 真实 Bundle 八项业务组合通过；LocalInventory 导入/生成/场景/无 Prefab 硬引用验证通过。新增场景 RED（缺场景）→GREEN，当前最小宿主组合10项通过（Artifacts/uikit/20261102T054938841851-tests-fe8065）。补充 Direct/Resources 路由及清理轨迹。
- 集中自审：发现 runtime 将明确 address 内的方括号误判为子资产，与 Editor 接受行为不一致；新增精确 address 用例 RED，改按 location.PrimaryKey 的 Ordinal 精确匹配区分地址/label/子资产。原11项运行时所有权测试在该次 RED 均通过。
- 验收裁定：2022 legacy 已完成九阶段 codegen；其余宿主通过现有 prepare-samples 的生成/编译/写回及 editor/generated-integration/check-generated 验证新描述，不重复相同的全九阶段代码路由实验。仅本批专项与明确直接回归，不运行 all suite/Reload/Player。

- 首次指定矩阵62组合794用例执行通过；严格原始证据复核发现4个 preflight 组合使用固定 trace 名，与 evidence.verify 的逐例 trace 要求不符，未计为整体验收通过。其余58组合原始证据检查通过。
- 修正 preflight trace 为真实用例名。该验证包属于四宿主共享指纹输入；按本计划当前输入证据要求刷新同一62专项组合，保持锁/指纹/必需用例/原始证据检查，不扩大 all suite、Reload 或 Player。Bundle源指纹不含测试代码且未变化，不重复内容构建。

- P6 首轮 main 双编辑器、Addressables 单装2022通过；Unity6 单装编译/生成完成，但 GetAssemblies(Player) 返回未引用的 UnityEngine.TestRunner 候选，旧“候选列表即实际依赖”断言误报。SBP 引入的 Test Framework asmdef autoReferenced=false。保留失败现场，补充实际编译依赖图，以消费源码及所选UIKit程序集为根验证完整引用闭包，仍拒绝任何可达测试/验收程序集；不按名称直接豁免，不将安装检查冒充 Player 构建。新增3项 Python RED 用例覆盖未引用候选、真实可达测试依赖和缺图/缺引用。

- 编译闭包补充预编译 DLL 检查：新增 NUnit 可达负例 RED→GREEN。实际2022编译器默认引用 UnityEngine.UnityTestProtocolModule（内建引擎模块），不能用任意 test 子串把它误判为 NUnit/TestRunner；追加真实模块名正例 RED，按框架/测试程序集命名分类，保留可达 NUnit、TestRunner、测试/验收程序集负例。生产运行时实现无修改。

- P6 元数据修复：Unity6 的 NUnit 插件 auto-reference 也会进入全部编译器候选引用；新增“候选存在但真实IL不导入”的正例 RED，同时保留真实IL导入NUnit负例。采集安装编译后的Runtime DLL导入元数据、复制原始DLL并封存哈希；缺证据/实际测试依赖仍失败。严格限定Editor安装编译，不执行Player构建；此次为预编译候选误判问题第二轮修复，若同因仍失败则按AGENTS停止该问题。

- P6 第二轮元数据修复定向验证通过：Unity6/legacy Addressables单装生成编译及严格元数据闭包通过，状态仅BLOCKED(no mobile Player built)，无其他错误；原始DLL/哈希/导入清单在 Artifacts/uikit/install-checks/20261102T064845784303-c34af2。现在冻结采集器，执行最终八项接入后再刷新当前指纹专项。

- P6 最终八项固定Git接入通过（main双legacy、addressables双legacy、all四宿主）；原始记录经 assess_install_record 校验，machine报告 Artifacts/uikit/phase3/install-acceptance.json。仅预期BLOCKED(no mobile Player built)，install_status=PASS；实际DLL元数据/哈希/依赖闭包已归档。工具完整回归165/165通过。冻结所有实现/采集器输入，最终刷新同一62专项组合及只读生成校验，P7尚未完成。

- P7 完成：最终当前输入62组合794项、Python165项、真实内容4宿主、只读生成4宿主、固定Git接入8项均通过。现有 evidence.verify 严格复核全部原始XML/逐例trace/索引/必需用例/当前快照；assess_install_record 复核安装原始文件/唯一预期阻塞/当前源指纹。生产/工具/文档diff检查及新包meta完整性通过。
- 最终源指纹 `6311d8437b5971a56508d5ab6c44784226fd8dfa70ea4ad59c23a0ac0557c893`；[交付报告](../../../Verification/Reports/2026-11-02-phase3.md)、[机器汇总](../../../Artifacts/uikit/phase3/acceptance.json)。仅本批Editor专项与安装编译验收完成，不构成Player/移动端/完整发布验收；未提交主仓库或发布。

## 后续实现与验收覆盖审查（2026-11-02）

- [x] 按 Spec 和最新版 AGENTS.md 审查实现及验收选择；不启动子代理，不执行 Player/真机/完整发布门禁。
- [x] 修复 Editor 实例销毁后释放异常进入 fallback、导致句柄被再次释放的问题。最小新例 RED（期望1次、实际2次）→GREEN，加入四宿主 addressables 必需用例；公共契约保持。
- [x] 修复 CI 对 Addressables 验证夹具、External 生成/Marker/ContextBuilder 和内容证据工具变更漏选专项的问题；新增两项选择器回归 RED→GREEN。
- [x] 当前输入复验：四宿主真实内容刷新；addressables EditMode 20项、addressables-runtime 48项、addressables-integration 44项，共12组合112项 PASS；Python167项 PASS。原始XML/逐例轨迹/索引哈希/必需用例/当前快照及Bundle内容证据严格复核通过。
- [x] 更新既有[报告](../../../Verification/Reports/2026-11-02-phase3.md)及[本轮机器汇总](../../../Artifacts/uikit/phase3/review-acceptance.json)。源指纹 `8103f3ad775beff34ab3d1bafe167dcc10eda1ae4e5dcdfdba53b03284c5cfb9`。审查当次未重复历史62组合及8项固定Git接入（八项接入已在后续补验节完成），历史记录仍仅证明修复前快照；不以历史记录扩大本次修复后的验收范围。无未解决的已确认问题。

## 修复后八项固定 Git 干净接入补验（2026-11-02）

用户要求仅运行 phase3 clean prepare-only。冻结源码及验证工具输入，沿用审查后源指纹 `8103f3ad775beff34ab3d1bafe167dcc10eda1ae4e5dcdfdba53b03284c5cfb9`；保留历史安装原始记录。

- [x] main：双编辑器 legacy，两项安装/生成/编译及原始证据检查通过，未引入 Addressables。
- [x] addressables：双编辑器 legacy，两项安装/生成/编译及原始证据检查通过，验证各自固定的 Addressables/SBP 版本和依赖隔离。
- [x] all：四宿主全部扩展组合四项通过。
- [x] 严格复核八项当前指纹、固定 Git 快照内修复代码、安装/生成/编译、依赖隔离/版本、程序集闭包、真实 DLL 元数据哈希、inventory 和原始文件索引；八项均 install_status=PASS，唯一 BLOCKED 原因为未构建 Player。没有执行 Build 方法或生成 Player 构建报告。
- [x] 已更新既有[交付报告](../../../Verification/Reports/2026-11-02-phase3.md)、[八项接入汇总](../../../Artifacts/uikit/phase3/review-install-acceptance.json)、[复核日志](../../../Artifacts/uikit/phase3/review-install-verify.log)和[修复后机器汇总](../../../Artifacts/uikit/phase3/review-acceptance.json)。既有12组合112项专项原始证据仍匹配当前输入，仅做只读复核，没有重复运行测试。历史62组合794项未刷新；不执行 Player、真机或完整发布门禁。
- 汇总脚本初次误从限定范围的 PackageInfo 快照读取 Input System；按采集器既有契约改读封存的实际 packages-lock，保留[初次日志](../../../Artifacts/uikit/phase3/review-install-verify-attempt1.log)。未修改源码、采集器、依赖版本或验收断言。

## 本地提交整理（2026-11-02）

- [x] 按最新版 AGENTS.md 核对提交范围：主包 External、独立 Addressables 包/样例/测试、验证工具与 CI 选择、四宿主夹具/生成产物/依赖配置，以及 Spec/计划/交付报告。
- [x] `.gitignore` 忽略宿主 AddressablesContent.json、OSX 内容更新构建输出及对应目录 meta；本地原始内容和证据保留。显式保留新包 Samples~/Documentation~，避免机器全局 `*~` 忽略导致漏交付。
- [x] 根 packages-lock、整个根 ProjectSettings、AGENTS.md 和 .vscode 均排除于提交，保留字节及既有工作区状态。Unity 资产及对应 .meta 成对整理，保留 GUID 和现有生成资产序列化格式。
- 提交前复核当前源码与12组合112项专项、八项固定Git接入及167项工具用例的已有证据；未变更实现/验证工具，不重复运行 Unity、Player、真机或完整发布门禁。仅作本地 Git 归档，不推送、不合并、不打 tag；提交标识以本地 Git 记录为准。
