# Wulato.UIKit 第二阶段执行计划

> **Superseded（实施任务）**：后续工作仅执行 [Master Optimization Plan](2026-11-03-wulato-uikit-master-optimization.md)。已完成成果、历史记录及其行为契约保留。

> 使用 `superpowers:executing-plans`，当前 Agent 连续执行；最新版 AGENTS.md 优先。不启用子代理，不逐任务多轮审查，不自动创建 worktree。

**授权：** 用户于 2026-11-01 明确授权执行审查修订计划 P0–P8，包括契约/版本变更和最终发布验收；不授权推送、合并、tag、发布或启用远端 CI。
**Spec：** [架构方案](../specs/2026-10-31-wulato-uikit-design.md) §12 第二阶段已确认契约。
**基线：** a6f55fa；T11 仅为历史证据。保留根 packages-lock、ProjectSettings、AGENTS.md、.vscode 原有修改。

## 已确认决策

- 对话调用方取消清理后抛 OperationCanceledException；结果只区分 Submitted/UserCanceled/ContextStopped，携带 UICompletion。
- 导航采用受控 Prepare/Commit/Rollback 参与者，提交前回滚，提交后调用方取消不撤销结果；Shutdown 仍能打断。
- 成对动画临时显示 prepared 页但不赋予输入资格，独立超时默认 2 秒；异常停止按既有隔离与迟到观察契约处理。
- Popup 请求为 ViewType/Arguments 描述，统一核心 OpenAsync；多暂停来源、key 去重、Closed 屏障、主动 StopAsync 与 Context 停止均闭环。
- 主包/输入适配 0.2.0，四扩展 0.1.0 依赖核心 0.2.0；不增 TMP、第三方动画/DI 或 Input System 强制依赖。
- Navigation、Presentation、Dialogs、Popup Queue 分包。Builder 扩展显式引用 UGUI，Core 不引用扩展；测试不引用仓库私有夹具。

## 执行任务

- [x] **P0 固化契约：** 同步 Spec 和本计划，删除旧 Action 提交/CallerCanceled/任意 OpenAsync 提案；检查引用和差异。
- [x] **P1 版本与验证：** 更新主包/适配/消费版本及验证硬编码；扩展来源白名单、testables、指纹、可选 suite，Python 定向 RED→GREEN。业务包随模块创建，不提前注册占位通过用例。
- [x] **P2 停止与组合：** lifetime、注册快照、工厂回滚、停止顺序、WithTransactions；contracts/integration/cleanup 定向验收。
- [x] **P3 Navigation：** 新页面事务接口、成对展示/Stop/Reset/隔离、受控提交和不可撤销收尾，随后历史/Push/Back、泛型编译探针、Modal 共存、缓存恢复、焦点/timeScale=0；navigation 专项及 transitions/sessions/cleanup/input-runtime 直接回归。
- [x] **P4 Presentation：** Create/Bind/Dispose，未匹配可跳过，每 Session 所有权、部分失败、外部 Model/cache 重开；presentation 专项与观察器 sessions 回归。
- [x] **P5 Dialogs：** 显式注册、结果通道、全等待期取消，提前提交后打开失败、竞争、关闭错误、Context 退出、旧 responder；dialogs 专项及 sessions/cleanup。
- [x] **P6 Popup Queue：** 描述式请求、优先级/FIFO/key、多暂停、共享 StopAsync/Dispose、停止/暂停/打开竞态、失败继续与跨 Context 隔离；popup-queue 专项及相关输入/清理回归。
- [x] **P7 集成：** 组合样例、主包单装/各扩展单装/组合固定 Git 安装、生成写回幂等、四阶段 Shutdown、XML/文档/迁移、CI 扩展路径和专项选择；集中作者自审后定向复验。
- [x] **P8 本轮 Editor/构建验收（2026-11-02 用户调整）：** 证据工具/移动夹具 RED→GREEN；四宿主 Editor/Reload/固定 Git 安装、移动八项 smoke 构建、两个 clean Android、四项 iOS 不签名原生编译及最终证据复核。真机与完整 release gate 不在本轮范围，原始完整发布要求保留如下。

依赖：P1→P2→P3；P4依赖P1，P5/P6依赖P2；P3–P6→P7→P8。实际按 P0–P8 顺序执行。

## 验证策略与必需行为

先最小宿主行为 RED→GREEN，再适用宿主专项；非视觉逻辑 EditMode，激活/加载/布局/输入 PlayMode。四宿主为 2022.3.62f2、6000.3.22f1 × legacy/input，不能换为 c1。UniTask 2.5.11、Input System 1.17.0 保持不变。

| 模块 | 必需行为 |
| --- | --- |
| 组合 | 工厂可读 lifetime/快照、失败逆序回滚、先状态后停止信号、信号早于 Dispose、重复停止、Root 销毁、共享资源所有权 |
| 导航 | FIFO/排他、成对可见不可交互、提交前失败恢复、提交后取消不撤销、旧页关闭错误、Shutdown、动画隔离、Modal 共存、历史仅描述、淘汰后 Back |
| Presenter | Awake后OnOpen前、每Session独立、部分绑定回滚、释放错误不阻断、外部Model不Dispose |
| 对话 | 排队/加载/打开/等待期取消、提前提交后打开失败、竞争只完成一次、Closed屏障、关闭错误、Context退出、旧responder |
| 队列 | 优先级/FIFO/不抢占、等待与显示去重、多暂停、打开中暂停、主动注销、暂停中Shutdown、失败继续、Closed推进、key全释放 |
| 工具/资源 | 新源码指纹、可选包缺席、版本/程序集边界、编译探针、CI触发、旧证据拒绝、Release零租约/cache容量/真实隔离计数 |

新增 suite 随模块注册：navigation、presentation、dialogs、popup-queue、flow-integration。命令从仓库根调用 run.py；输出存 Artifacts/uikit/phase2 唯一日志。零匹配、skip、缺原始证据不算通过。单次 UniTask 不重复 await，多消费者采用稳定完成源。

## P8 完整命令（模块阶段不运行）

先实现 build/release gate 的 `--phase phase2`，默认保留 phase1；phase 绑定构建身份、证据及门禁，smoke 从准确构建读取。

```sh
python3 -m unittest discover -s Tools/Verification -p 'test_*.py'
python3 Tools/Verification/run.py prepare-samples --editor all --backend all --check-only
python3 Tools/Verification/run.py check-generated --editor all --backend all
python3 Tools/Verification/run.py compile-probe --editor all
python3 Tools/Verification/run.py tests --editor all --backend all --mode EditMode --suite all
python3 Tools/Verification/run.py tests --editor all --backend all --mode PlayMode --suite all
python3 Tools/Verification/run.py play-reset --editor all --backend all
python3 Tools/Verification/run.py gate --name preflight
python3 Tools/Verification/run.py build --phase phase2 --editor all --backend all --target Android --profile smoke --prepare-only
python3 Tools/Verification/run.py build --phase phase2 --editor all --backend all --target all --profile smoke
python3 Tools/Verification/run.py build --phase phase2 --editor all --backend input --target Android --profile clean
python3 Tools/Verification/run.py smoke --build <准确构建目录> --device <实体设备ID> --timeout 900
python3 Tools/Verification/run.py gate --name release --phase phase2
```

prepare-only 保留非零退出语义，四项 install_status 和原始证据必须 PASS。iOS 须 Xcode编译/签名/安装/真机启动；新增扩展移动用例不能以原7例代替。缺设备/许可/签名为环境阻塞，不降级验收。

## 执行记录与证据

同一失败最多自动修复两轮；超过后保留现场并报告。每模块通过专项可交付，不等于阶段发布通过。不改根 ProjectSettings；维护新资产.meta。未测Player性能不声称通过预算。

| 任务 | 状态 | 证据/裁定 |
| --- | --- | --- |
| P0 | 完成 | 用户本轮已明确授权全部契约修订，不重复请求批准 |
| P1 | 完成 | Artifacts/uikit/phase2/p1-green.log (12), p1-runner.log (2), p1-boundary.log (2)；业务扩展随所属模块接入 |
| P2 | 完成 | Artifacts/uikit/phase2/p2-green.log；p2-matrix-stable.log，四宿主 contracts/integration/cleanup 专项全部 PASS |
| P3 | 完成（P7 已补生产组合证据） | p3-pair-isolation.log、p3-matrix.log；四宿主 navigation/transitions/sessions/cleanup/input-runtime，双版本正负编译探针 PASS |
| P4 | 完成 | p4-final.log（四宿主6例各通过），p4-matrix-r1.log（sessions四宿主）；重复注册 RED→GREEN 保证每Session仅一份Presenter |
| P5 | 完成 | p5-red.log→p5-green.log；p5-matrix.log：四宿主12例 dialogs + sessions/cleanup PlayMode 均PASS，含提交/取消/Shutdown竞争 |
| P6 | 完成 | p6-red.log→p6-green.log；停止顺序竞态 p6-matrix.log RED→p6-race-r1.log GREEN；p6-final.log 四宿主9例+cleanup PASS |
| P7 | 完成 | p7-matrix.log 中生产组合四宿主 PASS；样例两个 legacy PASS + p7-sample-input-r1-stable.log 两个 input PASS；p7-consumer-matrix.log 14/14；p7-idempotence.log 指纹相同；p7-final-regressions.log input-runtime/generated-integration 四宿主 PASS；自审修复 p7-review-green.log/p7-reentry-green.log |
| P8 | 本轮范围完成，非完整发布验收 | [正式报告](../../../Verification/Reports/2026-11-02-phase2.md)；[最终证据](../../../Artifacts/uikit/phase2/build-acceptance.json) PASS：Edit 502、Play 794、Reload 16、移动构建10、iOS原生编译4、最终组合安装4；真机与完整release gate未执行 |

P6 失败处理（第1轮）：p6-matrix.log 发现开始停止信号的回调顺序竞态：核心打开取消先于队列停止回调完成，当前项误报Failed。以 lifetime.Stopping 状态修正发布边界，不依赖注册顺序；按同用例定向复验，保留失败原始XML。

P7 第1轮：p7-flow.log 暴露同Context锁绕过Modal屏障，以 p7-modal-red.log 直接回归复现；只修正仲裁器的同域提议分支。Shutdown夹具在动画回调内同步唤醒后需 Yield 至安全提交边界，不放宽嵌套命令检测。样例Author只创建缺失资源，避免覆盖消费场景与破坏幂等。

P7 Input System 宿主样例失败：共享场景携带legacy序列化模块，被生产Builder拒绝。Author增加幂等SelectInput，只按宿主替换场景输入模块，不修改根ProjectSettings，不降级输入检查；单独复验两个input宿主。

P7集中自审：p7-review-red.log 复现未注册导航key/成对转场异常被归为LifecycleFailed，以及Build失败吞掉停止/Dispose错误；分别修正InvalidRequest/TransitionFailed分类与聚合回滚错误，保留Root解除占用和共享资源所有权。P8安装所需phase工具前移P7，完整门禁不提前运行。

P7集中自审补充：p7-reentry-red.log 证明关闭钩子内回应对话/注销队列时重复提交 Close 会触发重入拒绝；只在 Session.State==Open 时请求关闭，Closing 时等待现有 Closed，保持核心重入守卫。

当前已确认 Spec SHA-256：`241b2c320ab11a35f003be454f76e27c651095febfff473b991506379e6b8af0`。
P7安装范围裁定：无可选后端依赖的单包隔离在两个指定编辑器的legacy消费工程验证；四包组合覆盖四宿主，输入行为以真实双驱动专项及P8移动矩阵证明，避免重复无差异的输入适配安装组合。


## 2026-11-02 用户更新的本轮验收范围

用户明确要求继续且不需要真机测试验证。本轮 P8 仅执行完整 Editor/Reload/固定 Git 安装与 ARM64 IL2CPP 构建；iOS 在 Unity 导出后以 generic iphoneos、CODE_SIGNING_ALLOWED=NO 完成 Xcode 编译，不签名、不安装、不启动设备、不进行触摸测试。保留既有完整 release gate 的真机要求，不运行/不标记完整发布门禁通过；本轮结果单独报告为构建验收，不降低后续完整发布标准。

P7 固定 Git 消费 14 项 PASS（组合四宿主；单包两编辑器 legacy），证据 p7-consumer-matrix.log；样例流水线重复执行前后完整宿主指纹相同，p7-idempotence.log。Python 工具完整回归 148/148 PASS，p8-tools-final.log。代码及验收夹具已完成集中自审和定向修复，剩余专项后冻结输入进入 P8。

P8全量EditMode发现适配包input-package测试仍断言0.1.0（P1遗漏），按已授权版本升级同步为0.2.0，保留Input System 1.17.0严格断言；两个input宿主专项先复验。该改动仅为测试断言，既有单包Runtime隔离证据不重复；最终组合安装/构建重新绑定当前源指纹。

P8 最终复核完成：`Artifacts/uikit/phase2/p8-build-scope-final.log`，源码指纹 `4d2cfac1fd3633548516e2f9cc8ff723e60239012742d2b73138261b57b6ed13`。机器报告明确 `scope=editor-and-build-only`、`release_accepted=false`、`device_validation=not_run_user_excluded`，复用既有严格验证函数重新解析原始 XML、哈希、构建身份、程序集与固定 Git 依赖，没有放宽 release gate。四项 prepare-only 保留非零退出，逐项严格检查 install_status=PASS、generation_compiled 和唯一预期阻塞原因。最终集中差异检查及新包 meta 完整性通过；未推送、合并、打 tag、发布或触发远端 CI。

## 2026-11-02 第二阶段实现审查与修复

用户要求核对 Spec、执行计划和最新版 AGENTS.md，发现问题直接修复，不做真机验证、暂不提交。本次按 DEEP 审查，由当前 Agent 连续执行，未启动子代理。没有修改公共签名、包版本或根 ProjectSettings。

审查发现并修复 7 项问题：

| 优先级 | 原问题 | 修复与验收 |
| --- | --- | --- |
| P1 | Dialog 提交意图后，关闭屏障内的调用方取消被旧 intent 丢弃；原测试还断言错误行为 | 在最终主线程发布前检查调用方 Token，仍先等待 Closed；更正原测试为关闭屏障后抛 OperationCanceledException，ContextStopped 继续优先 |
| P1 | 输入提交回调中取消可能内联唤醒事务收尾，任务返回取消但参与者状态已发布 | 同步替换提交加入 InFlight 屏障，输入准备完成后、参与者 Commit 前重查取消；验证旧页/旧输入恢复、目标关闭、参与者未发布 |
| P1 | 请求描述 getter 内注销 Queue 后，Enqueue 仍添加项，产生永不完成的请求 | 读取描述后重查停止和去重状态，验证已停止项立即返回 QueueStopped 且不加载 |
| P2 | 配对动画 PlayAsync 入口仍看到隐藏 target，展示窗口开启顺序错误 | 先在输入锁下显示且禁交互，再调用动画；验证 PlayAsync 入口以及双表面基线/业务布局 |
| P2 | 泛型导航未注册类型同步抛 ArgumentException，违反 Spec §7.4 | 泛型便利入口通过异步任务交付 InvalidRequest，正负泛型约束保留 |
| P2 | Queue 的 Enqueue/StopAsync 在非主线程同步抛错，违反 Spec §7.4 | 返回 faulted UniTask/InvalidRequest，验证调用时不抛、消费时失败且不创建 View |
| P2 | CI 的输入适配包路径只选 PlayMode，漏测元数据/版本契约 | 增加仅 input 宿主 EditMode input-package，新增 Python 复现测试 |

新增9项 Unity 行为/竞态用例、1项 Python CI 选择测试；纠正1项原有对话竞态测试。另补齐已发布参与者状态回滚、旧页关闭错误不撤销新历史、缓存淘汰后参数恢复及成对写集复位的验收覆盖，均注册到必需用例矩阵。

RED 证据：`review-dialogs-red.log`、`review-navigation-red.log`、`review-races-red.log`、`review-commit-red.log`、`review-ci-red.log`。首轮修复 GREEN：`review-green-r1.log`；补充覆盖：`review-coverage.log`。取消回调复现的断言在 NextFrame 后执行，避免测试自身在后端回调栈内 Dispose 掩盖真实错误；未放宽核心重入守卫。

最终验证：`review-matrix.log`，工具149/149，四宿主 dialogs/navigation/popup-queue EditMode + transitions/sessions/cleanup/input-runtime/flow-integration PlayMode 全部 PASS；32组合共664项。`review-acceptance.json` 复用既有严格验证函数，检查当前快照、必需用例、原始XML和证据索引，无错误。指纹：`7eb7a42a501e4f93bce4fc37a6ea8c4fd99dca08e3410150a2e10a397204a757`。

本次是实现审查及定向回归，不重开 P8 全量构建门禁。此前1296项全量Editor、10项移动构建和4项iOS原生编译保留为审查前历史证据，不能作为本次修改后最终指纹的发布通过证明。未重跑全量 Editor/Reload/消费安装/Player构建，未执行真机或完整release gate；无推送、提交、合并、tag或发布。见[现有正式报告的审查补充](../../../Verification/Reports/2026-11-02-phase2.md)。
