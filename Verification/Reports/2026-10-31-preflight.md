# P0/P1 前置验证执行报告（残留问题修订版）

日期：2026-10-31。当前结论：**G0 schema 3 PASS**。本轮已修正同帧多鼠标按钮撤销、运行登记缺失/未知 schema 两项 P1 残留，并重新完成四宿主回归。范围仍仅为 P0/P1 验证代码、宿主与工具；没有进入 T2，没有创建生产 UIKit 包。

本报告取代此前 G0 v2 结论。此前 98 项 Unity 通过记录真实，但遗漏同帧多按钮路径，且运行登记丢失时仍可能回退旧绿记录。历史原始证据保留，当前关卡要求新的批次清单和 schema-3 运行证据，不复用旧结论。

批准架构方案 SHA-256 保持 `3ad327283835ceeb8c63d7068e6a25d904856f5eff525392eec0b993420814bd`。根工程 Assets、Packages、ProjectSettings 未改动，原有暂存设计文档保留。分支 `codex/p0-p1-preflight`。

## 最终验收

| 宿主 | 实际编辑器 | uGUI / Test Framework | 初始化 | 输入 |
| --- | --- | --- | --- | --- |
| u2022-legacy | 2022.3.62f2 | 1.0.0 / 1.1.33 | 4/4 PASS | 24/24 PASS |
| u2022-input | 2022.3.62f2 | 1.0.0 / 1.1.33 | 4/4 PASS | 25/25 PASS |
| u6000-legacy | 6000.3.22f1 | 2.0.0 / 1.6.0 | 4/4 PASS | 24/24 PASS |
| u6000-input | 6000.3.22f1 | 2.0.0 / 1.6.0 | 4/4 PASS | 25/25 PASS |

合计初始化 **16/16**、输入 **98/98**，即 **114 项 Unity PlayMode 全部通过**，无失败、跳过或 Inconclusive。Python **63/63** 通过。input 宿主锁 Input System 1.17.0、activeInputHandler=1；legacy 不安装 Input System、activeInputHandler=0。

八组最终证据已重新解析，原始 XML、summary、宿主快照与当前源码/配置指纹一致；每个运行的前后指纹一致。稳定导入先于最终回归执行。

证据位于仓库 Artifacts/uikit（忽略入库，本机保留）：

- 当前 G0：`g0-20261031T074421750619-9311fd/preflight-report.json`。
- 初始化：`20261031T074156335030-tests-3de9c9/init-report.json`。
- 输入：`20261031T074311179584-tests-dd6419/summary.json`。
- 每个测试批次的父目录包含独立 batch.json；宿主目录包含 run.json、results.xml、editor.log、逐例 trace、trace.json、host-snapshot.json、command.json、evidence-index.json、summary.json。
- Python：`round3-python-green.log`；原实现负向失败：`round3-registration-red.log`、`round3-inventory-red.log`。
- 真实证据副本破坏测试：`round3-gate-audit.json`；重现命令为 `python3 Artifacts/uikit/round3-gate-audit.py Artifacts/uikit/g0-20261031T074421750619-9311fd/preflight-report.json`，不修改原证据。

## 残留一：同帧多鼠标按钮撤销

### 原问题与红阶段

原实现仅清除已经进入回调的 inFlight 数据。左键 pointerUp 回调锁定或转移所有权后，模块仍使用预先缓存的射线/按钮状态处理右键和中键，造成新的 down、晚到 click 或重复 up。

新增四个共享必需用例，在原实现上于两个 2022 后端全部失败，记录 `20261031T072930685662-tests-6fca25`：

- LeftUpLock_RejectsLaterSideButtons。
- LeftUpTransfer_RejectsLaterSideButtons。
- TripleReleaseLock_NoDuplicateUpOrLateClick。
- TripleReleaseTransfer_NoDuplicateUpOrLateClick。

### 修正与绿阶段

撤销当前事件时，除清除 pointerClick/eligibleForClick 外，还使缓存 pointerCurrentRaycast 与 delta 失效，防止后续按钮继续使用撤销前的目标。旧输入通过受保护的 m_PointerData 使尚未处理的右键/中键数据一起失效；Input System 在同一鼠标指针中复用已观察的公开事件数据。

处理期间不再提前对所有按钮发送取消 up。先允许原生释放路径移除已完成的节点快照；Process 结束后只取消剩余 press/drag，再执行模块重置。这样既阻断新 down/click/drop，又避免“取消 up + 原生 up”重复。

InputEventSpy 保持纯观测，不禁用输入接收组件或过滤计数。新增 pointerEvents 记录按钮身份；三键释放用例分别断言 Left/Right/Middle 各恰好一次 up。全部释放后，新的右键/中键点击必须交付一次，防止全局静默造成假通过。

最终四宿主均通过四项新增场景和全部既有输入用例。没有复制私有状态机或使用私有反射，也没有通过切换 View/GameObject 生命周期隐藏事件。

## 残留二：登记文件缺失/未知版本使尝试消失

### 独立批次清单

CLI 在创建各宿主尝试目录、启动 Unity 前先写 batch.json，明确列出每个 run_id、目录、编辑器、后端、模式与 suite。schema-3 run.json 和 summary 绑定批次 ID 与清单 SHA-256；G0 校验清单内容、文件摘要、执行元数据及原始证据。

发现尝试时以清单为基础，即使 run.json 或整个尝试目录被删除，该尝试仍参与最新结果选择并判失败。未知运行 schema、未知清单 schema、缺失清单、孤立登记均拒绝，不通过 continue 将其丢弃。有效的新重试可以取代旧的、已列入清单但登记缺失的失败，不会永久卡在一个已被后续有效尝试取代的记录上。

历史 schema-2 登记只作为可读取历史保留，不能满足当前 schema-3 证据验证。

### 批次顺序与并发边界

独立审阅发现，不同 CLI 批次交错时，单纯以批次创建时间排序可能误选旧结果。因此 prepare/tests 在整个批次期间持有非阻塞独占文件锁；并发 CLI 批次明确失败，不交错执行四宿主。G0 持有共享读取锁，在批次执行期间返回 FAIL，不读取半完成状态作为通过。

有有效登记时使用宿主实际开始时间；登记缺失时保留清单时间和上下文。批次互斥使跨批次的清单顺序与执行顺序一致。代价是不能并行启动多个验证 CLI 批次；本计划的四宿主运行本来按批次串行，此限制不改变生产框架能力。

### 负向验证

63 项 Python 测试覆盖：缺失/未知 run schema、清单声明但尚无目录、整个尝试目录删除、清单缺失/未知版本/哈希变化、较新有效重试恢复通过，以及锁冲突、异常释放锁、共享读锁和真实子进程的批次互斥。

还对最终真实证据的临时副本实测：

| 证据状态 | 实际 G0 |
| --- | --- |
| 完整真实证据副本 | PASS |
| 新增最新失败尝试，保留旧绿记录 | FAIL |
| 删除该失败尝试的 run.json | FAIL |
| 将该失败尝试的 schema 改为 99 | FAIL |
| 删除该失败尝试的整个目录、保留批次清单 | FAIL |
| 再加入更新且完整有效的重试 | PASS |

最终初始化批次运行期间直接调用 G0，也实际返回 `verification-batch-in-progress` 的 FAIL，记录 `g0-busy-b013cab76fcb42ae8c68138843359da0/preflight-report.json`。

## 既有五项修正与验收继续保留

- 同一 Process 内当前事件的 pointerUp 锁定/移交、BeginDrag 锁定、拖拽取消及新输入恢复继续通过；本轮补齐同一 Process 内后续鼠标按钮的覆盖。
- G0 继续核对原始文件集合/哈希、重新解析 XML/日志、对比 summary 和命令、当前宿主快照、运行终态及逐例 trace；required_cases 为完整 fixture-qualified 名称，父 suite 失败也拒绝。
- 指纹继续覆盖宿主 Assets/ProjectSettings/Packages、全部验证包内容、运行器和 matrix，拒绝同名异源包与 lock 来源不一致。
- 未绑定 UI 的 Z 键持续按住时，UI 解锁后的新输入仍可用；释放屏障仅观察 UI actions 实际 controls。
- 初始所有权在首个输入前应用，非所有者无法 down/click，所有者新点击有效。

P0 的非激活父节点 Awake 顺序、CanvasGroup 不延后 Awake、序列化引用和重新激活不重复 Awake 均保持通过。ProbeOnCreate 仍为人工引擎时序标记，不是生产 UIView 测试。

四宿主还保留 pointer/touch、move、Submit/Cancel、press/drag、有效焦点恢复、timeScale=0、持续按键跨所有权及输入采集后 Process 前移交等场景。原有 asmdef TestAssemblies 去重、后端测试复用公共 PlayMode 程序集、CLI input-preflight 到 NUnit input_preflight 映射不变。

## 审阅与授权停止点

独立最终审阅发现的批次交错排序问题已通过互斥策略及相关测试闭环。最终四宿主回归、63 项 Python 测试、八组原始证据重验及真实副本破坏验证均通过预期检查。

本结论仅覆盖当前 P0/P1 原型和必需用例。Input System Submit/Cancel 仍使用键盘 Enter/Escape，move 使用手柄摇杆，未直接注入手柄 Submit/Cancel 按钮；没有执行 Android/iOS ARM64、IL2CPP 或生产生命周期验收。继续停在 P0/P1，未进入 T2。
