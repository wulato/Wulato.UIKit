# P0/P1 前置验证执行报告（Review 修订版）

执行日期：2026-10-31。当前结论：**G0 v2 PASS**。本轮修正全部 5 项 review 问题并重新运行四宿主回归；未进入 T2，未创建生产 UIKit 主包或 Input System 适配包。

本报告取代此前基于 80 项 Unity 用例的 G0 结论。此前 XML 与 summary 的通过记录真实，但遗漏了同一 Process 内撤销等场景，且旧关卡存在证据/指纹假通过路径；不能继续作为当前验收依据。原始记录保留，新的 G0 只接受 schema 2 的完整运行证据。

批准架构方案 SHA-256 仍为 `3ad327283835ceeb8c63d7068e6a25d904856f5eff525392eec0b993420814bd`。根工程 Assets、Packages、ProjectSettings 未改动；原有暂存架构文档保留。工作分支 `codex/p0-p1-preflight`。

## 最终四宿主回归

| 宿主 | 实际编辑器 | uGUI / Test Framework | 初始化 | 输入 |
| --- | --- | --- | --- | --- |
| u2022-legacy | 2022.3.62f2 | 1.0.0 / 1.1.33 | 4/4 PASS | 20/20 PASS |
| u2022-input | 2022.3.62f2 | 1.0.0 / 1.1.33 | 4/4 PASS | 21/21 PASS |
| u6000-legacy | 6000.3.22f1 | 2.0.0 / 1.6.0 | 4/4 PASS | 20/20 PASS |
| u6000-input | 6000.3.22f1 | 2.0.0 / 1.6.0 | 4/4 PASS | 21/21 PASS |

合计初始化 16/16、输入 82/82，即 **98 项 Unity PlayMode 全部通过**，无失败、跳过或 Inconclusive。Python 结果解析、指纹、证据与 G0 集成测试 **47/47 通过**。input 宿主锁定 Input System 1.17.0、activeInputHandler=1；legacy 未安装 Input System、activeInputHandler=0。

最终两组运行使用相同的当前完整指纹，运行前后指纹一致。稳定导入阶段在测试前完成；新节点首次生成 meta 导致的早期指纹变化结果已作废，没有作为最终证据。

证据均位于仓库 Artifacts/uikit（忽略入库，本机保留）：

- 当前 G0：`g0-20261031T071642255082-26d533/preflight-report.json`。
- 初始化：`20261031T071251969288-tests-c371ea/init-report.json`。
- 输入：`20261031T071402551875-tests-d25598/summary.json`。
- 每个运行的宿主子目录包含 results.xml、editor.log、逐例 trace、trace.json、host-snapshot.json、command.json、run.json、evidence-index.json 和 summary.json。
- Python：`review-fixes-python-green.log`。
- 对真实完整证据副本进行破坏验证：`review-fixes-gate-audit.json`；可用 `python3 Artifacts/uikit/review-fixes-gate-audit.py` 重现，不修改原证据。

## 五项 Review 闭环

### 1. 同一 Process 内撤销后仍发送 click/drop

原实现只在 Process 入口检查门控。新增真实输入回调在 pointerUp 内调用 SetLocked/TransferOwner，原实现确实失败；红阶段记录为 `20261031T150605-tests-6fe714`。

现在由独立 InputGateNode 保存节点取消快照，InputEventSpy 只记录真实交付事件，不检查门控、不过滤计数。模块经 gate.Process 执行；节点在回调时登记当前 Process 中的实际 PointerEventData。撤销立即清除其 pointerClick、eligibleForClick、press/drag 状态，避免 Input System 在 pointerUp 前缓存 isClick 后继续点击；模块重置推迟至 Process finally，避免在模块遍历期间修改其内部指针集合。实际事件引用只保留至当前 Process 结束，跨帧取消仍使用独立快照。

新增必需场景 PointerUpLock_DiscardsSameProcessClick、PointerUpTransfer_DiscardsSameProcessClick 和 BeginDragLock_CancelsWithoutDrop_ThenFreshClickWorks，分别验证无旧点击/投放、取消收尾及后续新输入。四宿主全部通过。没有私有反射或复制输入模块私有状态机。

### 2. 原始证据缺失而 G0 仅凭 summary 通过

每次尝试启动前登记 run.json。结束后建立 evidence-index.json，保存 XML、日志、快照、命令、运行元数据和全部 trace 文件的 SHA-256，并将索引自身哈希记录于 summary。

G0 按 UTC started_at 与 run_id 选择最新注册尝试，重新检查原始文件集合/哈希、解析 XML/日志、对比 summary 的用例和计数、当前宿主快照、命令目标、执行状态与逐例 trace；缺失、不完整或损坏的新尝试不会回退到旧绿记录。required_cases 使用完整 fixture-qualified 名称，避免同名方法抵消缺失用例；父 suite 失败也会判失败。

47 项 Python 测试包含缺 XML/日志/快照/命令/索引/逐例 trace、哈希变化、重封装失败 XML、错误程序集用例名、编译错误、summary 不一致、错误模式、快照不一致和未完成运行等负向路径。

另外在最终真实证据的临时副本上实测：完整副本 PASS；删除 XML 后 FAIL；改动日志后 FAIL；保留旧绿记录但新增未完成尝试后 FAIL。原证据未改动。

### 3. 指纹遗漏宿主输入和实际安装源码

指纹现在覆盖宿主 Assets、ProjectSettings、Packages 的全部文件（不含 .DS_Store），包括 InputManager、C#、asmdef、资源及 meta；还覆盖全部参与验证包的内容、验证工具与 matrix。摘要使用带长度的路径/内容编码。

宿主 file dependency 必须解析到 V/Packages 下对应的标准验证包目录，校验包名与 lock 的 local source/version；同名替代目录被明确拒绝。摘要从已解析且校验通过的来源读取，不再仅凭 manifest key 猜测源码。

负向测试已证明：InputManager 修改、添加宿主 C# 或 asmdef 均改变指纹；同名异源包被拒绝；lock 源路径不匹配被拒绝。原实现的上述断言确实失败，记录 `review-fixes-python-red.log`；修正后全部通过。

### 4. 无关按键使 UI 一直无法恢复

Input System 的释放屏障改为枚举 left/middle/right click、submit、cancel、move 动作实际解析的 controls，不再扫描设备上所有按钮。新增 input 宿主必需场景 UnboundKey_DoesNotBlockFreshInputAfterUnlock：Z 键全程保持按下且未绑定 UI，解锁后新左键点击交付一次，并断言 Z 仍按住。两编辑器 input 宿主均通过；原实现红阶段失败。

### 5. 初始非所有者仍能收到点击

夹具在任何输入处理前调用 Initialize，将门控节点连接到 gate 并应用初始所有权。新增四宿主必需场景 InitialNonOwnerCannotClick_OwnerStillWorks：OwnerId=0 时向 Owner1 点击，down/click 均为零；再向 Owner0 全新点击恰好成功一次。原实现红阶段失败，修正后四宿主通过。

## 保留的初始化与输入验收

P0 仍验证 activeSelf=true Prefab 在非激活父节点下延后 Awake，OnCreate 人工标记先于 Awake/OnEnable，序列化引用可访问；CanvasGroup 隐藏不延后 Awake，重新激活不重复 Awake。ProbeOnCreate 仅是引擎时序夹具，不是生产 UIView 测试。

P1 保留原有 pointer/touch、move、Submit/Cancel、跨锁 press、drag 所有权转移、焦点恢复、timeScale=0、持续按键跨所有权、采集输入后 Process 前移交等 16 个共享场景，再增加 4 个共享场景及 1 个 Input System 专属场景。完整名单在 matrix.json。所有输入仍经实际模块 UpdateModule/Process 路由；ExecuteEvents 只用于框架取消通知，不用于伪造测试输入。

保留原执行裁决：TestAssemblies 自动提供 TestRunner/NUnit，测试 asmdef 不重复引用；后端测试引用公共 PlayMode 程序集复用场景，Runtime 不引用测试程序集；CLI input-preflight 映射至 NUnit 分类 input_preflight，因为 NUnit 不接受连字符。复用当前 checkout 和任务分支，独立四宿主保护根工程，没有额外 worktree、合并或推送。

独立最终审阅未发现这五项修正仍有重要问题；最终结果由四宿主完整回归、47 项 Python 测试和真实证据副本破坏测试确认。

## 范围与停止点

G0 v2 仅说明当前 P0/P1 验证原型通过前置关卡。Input System 的 Submit/Cancel 仍使用键盘 Enter/Escape，move 使用手柄摇杆，未直接注入手柄 Submit/Cancel 按钮。Android/iOS ARM64、IL2CPP、生产会话/生命周期和输入生产化属于后续验收，本轮没有执行。继续停在 P0/P1，不自动进入 T2。
