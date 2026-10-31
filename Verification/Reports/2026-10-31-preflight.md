# P0/P1 前置验证执行报告

执行日期：2026-10-31。结论：**G0 PASS**。范围仅为独立宿主、初始化夹具、双输入门控原型与验证工具；未进入 T2，未创建生产 UIKit 主包或 Input System 适配包。

批准架构方案 SHA-256：`3ad327283835ceeb8c63d7068e6a25d904856f5eff525392eec0b993420814bd`，执行后仍一致。根工程 Assets、Packages、ProjectSettings 的 Git diff 为空；原有暂存架构方案保留。

## 最终验收

以下两组结果均在最终 P1 依赖、源码与运行器指纹下生成，G0 校验八个组合全部与当前指纹匹配。

| 宿主 | 实际编辑器 | uGUI / Test Framework | 初始化 | 输入 |
| --- | --- | --- | --- | --- |
| u2022-legacy | 2022.3.62f2 | 1.0.0 / 1.1.33 | 4/4 PASS | 16/16 PASS |
| u2022-input | 2022.3.62f2 | 1.0.0 / 1.1.33 | 4/4 PASS | 16/16 PASS |
| u6000-legacy | 6000.3.22f1 | 2.0.0 / 1.6.0 | 4/4 PASS | 16/16 PASS |
| u6000-input | 6000.3.22f1 | 2.0.0 / 1.6.0 | 4/4 PASS | 16/16 PASS |

合计 Unity PlayMode 80 项通过，失败/跳过/Inconclusive 为 0；Python 结果解析、G0 与编辑器覆盖路径守卫测试 18/18 通过。input 宿主解析 Input System 1.17.0、activeInputHandler=1；legacy 未安装 Input System、activeInputHandler=0。实际 manifest、lock 与输入设置见各宿主 host-snapshot.json。

证据均位于仓库下 Artifacts/uikit（忽略入库，本机保留）：

- G0：`g0-20261031T145515/preflight-report.json`。
- 最终初始化：`20261031T145413-tests-7651b4/init-report.json`。
- 最终输入：`20261031T145452-tests-b050d5/summary.json`。
- 上述运行各宿主目录包含 `results.xml`、`editor.log`、`trace.json`、`host-snapshot.json`、`command.json`、`summary.json`。
- Python：`python-tests.log`（18/18），最终命令再次运行亦为 18/18。

## P0 实测

所有 trace 以克隆实例 ID 关联，序列化依赖存在。活跃父节点控制：`Awake → OnEnable → OnCreate`。非激活父节点下 Instantiate 后 trace 为空，手动 ProbeOnCreate 后为 `OnCreate`，激活后为 `OnCreate → Awake → OnEnable`。CanvasGroup 隐藏不延后 Awake；停用/再次激活只增加一次 OnEnable，Awake 仍一次。非激活时 Awake 初始化状态为 false，但序列化引用可访问。

`ProbeOnCreate` 是人为调用点，验证引擎时序；不是生产 UIView 生命周期测试。初始红控制 `p0-red/results.xml` 为 3 项通过、1 项预期顺序失败，`InactiveParent_BeforeOnCreate.trace.json` 实际记录提前的 Awake/OnEnable。

## P1 实测

16 项必需场景完整列于 Tools/Verification/matrix.json，包括未锁定正向控制、锁定 Submit/Cancel、跨锁鼠标 press、持续 Submit/Cancel、drag 所有权转移、touch 跨锁及新 touch、锁定 pointer/touch、持续 move 的释放屏障、有效/无效焦点恢复、timeScale=0，以及持续 Submit/Cancel 跨所有权和输入采集后 Process 前移交的四项补充场景。

输入由 legacy BaseInputModule.inputOverride 和 Input System InputTestFixture 设备注入，经过实际模块 UpdateModule/Process 与 EventSystem；没有用 ExecuteEvents 直接注入输入。节点取消通知使用 ExecuteEvents 发送 pointerUp/endDrag，不产生 click/drop。未使用私有反射或复制模块私有状态机。

门控策略为：撤销时取消节点按压/拖拽并清空选择，调用真实模块的 DeactivateModule；Input System 额外通过 enabled 生命周期触发模块自己的指针重置。阻断期间不 Process，持续检查原始 held 状态，全部释放后用中立帧重置/恢复有效焦点，再允许新边沿。

新模块会复用 PointerEventData：最初保留引用的原型在两个 input 宿主各有 2 项失败，证据 `20261031T143343-tests-49aacc`。修正为在真实回调中保存节点自己的取消快照后，按压和拖拽收尾通过。legacy 的 held-state 注入协议补足 BaseInput 只有按钮边沿的限制；默认 provider 使用公开 Unity Input.GetButton 等接口。

最终透传红控制证据 `20261031T145258-tests-bf961d`：四宿主均为未锁定正向控制 1 项通过、门控断言 15 项预期失败，新增所有权场景亦确实失败；启用门控后的最终结果全部通过。编译失败、无效分类或缺包均不当作输入红阶段证据。

G0 曾在运行器/源码变化后拒绝旧绿记录（例如 `g0-20261031T145052`），最终以两个 suite 的当前结果通过；没有复用旧 P0 依赖指纹。运行器也比较运行前后指纹，防止测试期间源码变化被错误归档为当前通过。

## 执行裁决与审阅闭环

1. 复用当前 checkout，并建立 `codex/p0-p1-preflight` 分支；保留现有暂存内容，没有创建额外 worktree、合并或推送。代价是验证文件在当前 checkout 可见；通过独立宿主及根工程 diff 检查隔离引擎改写。
2. TestAssemblies 自动注入 TestRunner；Unity 2022 实测拒绝重复显式引用，因此从测试 asmdef 去重，保留等效引用。错误时会导致编译失败，不能绕过用例发现关卡。
3. 两后端测试 asmdef 引用公共 PlayMode 程序集，复用场景和断言；Runtime 不引用测试程序集。代价是测试程序集之间存在显式依赖，不影响生产包边界。
4. NUnit 实测拒绝带连字符的分类。保留 CLI suite `input-preflight`，内部明确映射为 Category `input_preflight`。首次无效分类结果只保留诊断，不计为引擎能力证据；映射错误会由必需用例/零匹配校验拒绝。

已完成一次独立最终审阅；其重要发现为缺少 held Submit/Cancel 直接跨所有权验证。已补齐四项必需用例，以透传红控制和最终四宿主绿结果闭环。最终只验证修复和完整回归，没有将审阅扩展为 T2。

保留的非阻断覆盖限制：Input System 使用键盘 Enter/Escape 验证 Submit/Cancel、手柄摇杆验证 move，尚未直接注入手柄 Submit/Cancel 物理按钮；不声称覆盖该物理按钮组合。该项不削减本次 P1 命名动作验收，不代表后续 T7/T11 可免验。

## 授权停止点

G0 仅说明当前 P0/P1 原型的前置可行性验证通过。Android/iOS ARM64、IL2CPP、生产会话/生命周期与完整输入生产化属于后续阶段，本次没有执行。按用户要求停在这里，不自动进入 T2。
