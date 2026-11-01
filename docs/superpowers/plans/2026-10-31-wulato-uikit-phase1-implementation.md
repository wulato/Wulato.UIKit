# Wulato.UIKit 第一阶段 Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** 先验证初始化顺序与双输入驱动，再交付可跨项目安装、经过竞态和移动端验收的 uGUI 核心闭环。

**Architecture:** 小核心、默认 UGUI/Loading 实现和独立 Input System 适配包。命令 FIFO 串行，View 与 Session 分离，租约与独立运行时宿主负责回收和迟到清理；验证原型不成为运行时依赖。

**Tech Stack:** Unity 2022.3.62f2 / 6000.3.22f1、uGUI 1.0.0 / 2.0.0、UniTask 2.5.11、Input System 1.17.0、Unity Test Framework、Python 3 验证入口、macOS 自托管 CI。

**Spec:** [最新版架构方案](../specs/2026-10-31-wulato-uikit-design.md)。批准基线 SHA-256：`3ad327283835ceeb8c63d7068e6a25d904856f5eff525392eec0b993420814bd`；执行前核对文件指纹，若变化先同步计划。

**状态与授权边界：** 本文件仅为计划。所有复选框未执行；没有建立验证工程、安装包、运行 Unity、编写生产代码或完成任何 spike。用户后续明确启动执行后才进入 P0；当前不选择执行方式，也不自动衔接实现。

## Global Constraints

- 包名 `com.wulato.uikit`，命名空间 `Wulato.UIKit`，首版 0.1.0；输入适配包 `com.wulato.uikit.input-system`。
- 最低 Unity 2022.3.62f2；指定验收编辑器为 2022.3.62f2 与 6000.3.22f1，不能替换为 2022.3.62f2c1。
- 主包 `unity: "2022.3"`、`unityRelease: "62f2"`，依赖 uGUI 1.0.0 与 UniTask 2.5.11；Unity 6 验证宿主锁 uGUI 2.0.0。
- UniTask Git URL 固定 `https://github.com/Cysharp/UniTask.git?path=src/UniTask/Assets/Plugins/UniTask#2.5.11`；输入适配包依赖 Input System 1.17.0。提交锁文件，不使用移动分支。
- 主包不依赖 TMP、Input System 或其他可选后端；示例使用 uGUI Text。核心不反射发现可选包，不依赖运行时类型扫描。
- 核心公开 API、View 钩子及生命周期顺序严格实现 Spec §5–9；首版单界面转场，不交付 Navigation、Dialogs、Popup Queue、Presentation、Addressables、R3、DOTween、虚拟列表或 Source Generator。
- 首版仍实现会话扩展、事务转场所有权的接入协议和测试替身，不提前实现这些业务扩展。
- 单次 UniTask 只消费一次；Closed、重复 Close、重复 Shutdown 使用非池化 UniTaskCompletionSource 共享结果；主线程通知在完整状态提交后泵送。
- 加载 30 秒、打开生命周期 10 秒、关闭生命周期 2 秒、进入/退出各 2 秒、Shutdown 总预算 5 秒、缓存容量 8、诊断 Basic；均为上限/配置，不添加等长固定 Delay。
- 单 Context FIFO、Single 重复拒绝、Multi 关闭释放、CacheOne 允许 LRU；旧 Session 不操作新会话。关闭接受后无调用方取消。
- 两编辑器 × Android/iOS ARM64 × 旧输入/Input System，完整 8 组合均为第一阶段发布要求；iOS 真机可人工触发，不免验。
- 原有工作区、暂存区与 Review 文件不清理、不重排；提交时逐项加入本任务文件，不使用 `git add .`。验证输出不包含签名凭据。

## Review Focus

1. 取消与成功交付发生在同一帧：必须只有一个终态、一次消费和一次租约释放；测试归 T3/T5。
2. 屏幕尺寸在缓存期间改变、Prefab 根为非 stretch 且 pivot 特殊：重开不漂移、不恢复缓存根尺寸；测试归 T4/T6。
3. 按住指针、Submit 或 Cancel 跨越锁定/焦点移交：解锁不补发点击或按键；测试归 P1/T7。
4. 完成源续体同步提交重开/Shutdown，且错误接收器自身抛错：不重入、不饿死通知泵；测试归 T3/T5。
5. 干净接入已有低版本 UniTask、Unity 6 未锁 uGUI、Domain/Scene Reload 关闭：明确拒绝错误依赖、没有跨 PlayMode 脏状态；测试归 T2/T8/T11。

---

## 路径、当前基线与执行入口

仓库根 `R=/Users/tian/Projects/Tian/Wulato.UIKit`。下文路径别名是精确前缀，不是待决定目录：

| 别名 | 完整目录 | 职责 |
| --- | --- | --- |
| P | R/Packages/com.wulato.uikit | 主包 |
| I | R/Packages/com.wulato.uikit.input-system | 输入适配包 |
| V | R/Verification | 独立宿主、原型与接入测试包 |
| C | R/Tools/Verification | 验证 CLI、结果解析和构建编排 |
| A | R/Artifacts/uikit | 未入库日志、XML、构建和报告 |

文件清单中的 Core/UGUI/Loading 分别指 P/Runtime 下的对应目录；Tests 默认相对 P，写明 I/V 的条目除外。同一目录分组的短文件名沿用该分组目录，含目录前缀的条目相对分组根目录解析。

当前仅有初始场景与 Unity 项目设置，没有框架代码、asmdef、生成器或 CI。P0/P1 只创建 V/C 的验证代码与必要忽略规则，不能先创建 P/I 的生产实现。

V/Projects 使用四个独立宿主目录：`u2022-legacy`、`u2022-input`、`u6000-legacy`、`u6000-input`。固定各自 ProjectVersion、uGUI 和 Active Input Handling，legacy 项目不安装 Input System，input 项目只启用新输入后端；不让两个编辑器打开同一目录或根工程。2022 使用 Test Framework 1.1.33，6000 使用编辑器随附的 1.6.0；锁定各自解析结果。

验证代码分为 V/Packages 下的 `com.wulato.uikit.verification`（公共夹具/初始化）、`com.wulato.uikit.verification.legacy`、`com.wulato.uikit.verification.input-system`。宿主必须在 dependencies 中挂载包，再在 testables 中启用其测试；testables 不能代替安装或路径解析。T2 开始新增 `com.wulato.uikit.acceptance`，负向编译与跨程序集契约测试不混进正式包。

### P0/P1 宿主接入清单

下表的 dependency/testables 都是宿主 manifest 内容；不通过验证包的 package.json 写本地 Git/file 传递依赖。P0/P1 验证包版本均为 0.1.0；本地包目录中的 package.json name 必须与 manifest key 一致。

| 宿主 | 固定运行环境 | P0 dependencies / testables | P1 增量 dependencies / testables |
| --- | --- | --- | --- |
| u2022-legacy | Unity 2022.3.62f2；uGUI 1.0.0；Test Framework 1.1.33；activeInputHandler=0 | 只挂公共验证包并启用其测试；不安装 Input System | 加 legacy 验证包并启用其测试 |
| u2022-input | Unity 2022.3.62f2；uGUI 1.0.0；Test Framework 1.1.33；activeInputHandler=1 | 挂公共验证包并启用其测试；安装 Input System 1.17.0，但此时不启用它的测试框架 | 加 input-system 验证包；在 testables 同时加入该包及 com.unity.inputsystem |
| u6000-legacy | Unity 6000.3.22f1；uGUI 2.0.0；Test Framework 1.6.0；activeInputHandler=0 | 只挂公共验证包并启用其测试；不安装 Input System | 加 legacy 验证包并启用其测试 |
| u6000-input | Unity 6000.3.22f1；uGUI 2.0.0；Test Framework 1.6.0；activeInputHandler=1 | 挂公共验证包并启用其测试；安装 Input System 1.17.0，但此时不启用它的测试框架 | 加 input-system 验证包；在 testables 同时加入该包及 com.unity.inputsystem |

三个本地 dependency 值分别固定为：

```json
{
  "com.wulato.uikit.verification": "file:../../../Packages/com.wulato.uikit.verification",
  "com.wulato.uikit.verification.legacy": "file:../../../Packages/com.wulato.uikit.verification.legacy",
  "com.wulato.uikit.verification.input-system": "file:../../../Packages/com.wulato.uikit.verification.input-system"
}
```

这些路径以各宿主的 `Packages/manifest.json` 所在目录为基准，解析到 V/Packages；这是路径字典，不是让每个宿主同时安装三个包。P0 只写公共包，P1 再按后端写入一个增量包，绝不引用尚未创建的 P1 包或生产 P/I。input 宿主的 P1 testables 最终为公共包、input-system 验证包、com.unity.inputsystem；legacy 宿主为公共包、legacy 验证包。

| 验证 asmdef（包内相对位置） | 引用 | 平台/约束 |
| --- | --- | --- |
| 公共 Runtime/Wulato.UIKit.Verification.asmdef | UnityEngine.UI | 全平台，仅基础 trace/spy/gate 夹具；不引用 UIKit、NUnit 或 Input System |
| 公共 Editor/Wulato.UIKit.Verification.Editor.asmdef | 公共 Runtime、UnityEngine.UI | Editor-only，包含 FixtureBuilder；不依赖 P1 类型 |
| 公共 Tests/PlayMode/Wulato.UIKit.Verification.PlayMode.asmdef | 公共 Runtime、UnityEngine.UI、UnityEngine.TestRunner、UnityEditor.TestRunner | TestAssemblies；PlayMode 测试，includePlatforms 不设为 Editor-only |
| legacy Runtime/Wulato.UIKit.Verification.Legacy.asmdef | 公共 Runtime、UnityEngine.UI | 全平台；不引用 Unity.InputSystem |
| legacy Tests/PlayMode/Wulato.UIKit.Verification.Legacy.PlayMode.asmdef | legacy/public Runtime、UnityEngine.UI、UnityEngine.TestRunner、UnityEditor.TestRunner | TestAssemblies；仅 legacy 宿主安装 |
| input Runtime/Wulato.UIKit.Verification.InputSystem.asmdef | 公共 Runtime、UnityEngine.UI、Unity.InputSystem | 全平台；仅 input 宿主安装 |
| input Tests/PlayMode/Wulato.UIKit.Verification.InputSystem.PlayMode.asmdef | input/public Runtime、UnityEngine.UI、Unity.InputSystem、Unity.InputSystem.TestFramework、UnityEngine.TestRunner、UnityEditor.TestRunner | TestAssemblies；通过 InputTestFixture 隔离并注入设备，仅 input 宿主安装 |

表中的 TestAssemblies 提供 NUnit 测试引用；TestFramework/Editor 引用不得泄漏到 Runtime。测试夹具每例建立新 EventSystem，结束后恢复 Input System 状态并销毁自己创建的对象。各验证包 package.json 数字依赖显式声明其 Runtime 使用的 uGUI、公共验证包以及对应 Input System；宿主的 file dependency 覆盖公共包来源，不能要求包内传递本地路径。

`prepare --phase p0|p1|runtime` 是显式阶段参数，不默认安装未来包。阶段 p0 只生成/解析本表 P0 输入；阶段 p1 在创建两后端包后应用增量，验证 local path/package name/已解析版本、Active Input Handling、asmdef 引用与 testables，并输出实际接入快照。阶段 runtime 仅在 T2 后挂 P、acceptance 和 UniTask，I 到 T7 创建后才加到 input 宿主；legacy 宿主始终不装 I。每阶段先导入并确认编译，再以测试发现/required_cases 校验排除零匹配。缺包、引用错误或后端配置不符均非零退出。

阶段 runtime 的主包路径为 `file:../../../../Packages/com.wulato.uikit`，acceptance 路径为 `file:../../../Packages/com.wulato.uikit.acceptance`，并将这两个包加入 testables。T7 在 input 宿主增加 `file:../../../../Packages/com.wulato.uikit.input-system` 及相应 testables；P1 的验证包保留以便回归。prepare 分别写入测试专用符号 UIKIT_VERIFY_LEGACY / UIKIT_VERIFY_INPUT_SYSTEM，后端专属用例只在匹配宿主编译；共享逻辑用例在四宿主编译。不能让不适用用例以 Skip 通过，也不能要求 legacy 宿主解析 Input System 测试类型。

所有后续命令从 R 运行。下列是 P0 要交付的 CLI 契约，不是现在运行的命令：

```text
python3 Tools/Verification/run.py prepare --phase p0 --editor all --backend all
python3 Tools/Verification/run.py tests --editor all --backend all --mode PlayMode --suite init
python3 Tools/Verification/run.py prepare --phase p1 --editor all --backend all
python3 Tools/Verification/run.py tests --editor all --backend all --mode PlayMode --suite input-preflight
python3 Tools/Verification/run.py tests --editor all --backend all --mode EditMode --suite <suite>
python3 Tools/Verification/run.py gate --name preflight
```

`--editor` 接受 `2022.3.62f2`、`6000.3.22f1`、`all`；`--backend` 接受 `legacy`、`input`、`all`。suite 对应 NUnit Category，不能靠易漂移的方法名模糊匹配。`tests` 默认只测试指定 suite，整阶段回归使用 `--suite all`。

后文“运行 suite X（某模式）”统一展开为 `python3 Tools/Verification/run.py tests --editor all --backend all --mode <EditMode或PlayMode> --suite X`；两模式分别启动一次。每 suite 的具体用例名及预期终态写入 matrix.json 按 editor/backend/mode/suite 分组的 required_cases；缺任何必需用例不能靠同 suite 的其他通过数抵消。

CLI 采用参数数组调用编辑器，Editor 路径默认 `/Applications/Unity/Hub/Editor/<version>/Unity.app/Contents/MacOS/Unity`，允许 `--editor-path` 覆盖但核验实际版本。测试调用 `-batchmode -runTests -projectPath ... -testPlatform ... -testCategory ... -testResults ... -logFile ...`；测试调用不加 `-quit`，布局/输入测试不强制 `-nographics`。初始化/生成/构建使用独立 executeMethod 调用，可加 `-quit`，不能与测试启动混为一个命令。

每次运行在 A 下用唯一 run-id 保存 editor.log、NUnit XML、事件 trace.json、实际宿主接入快照及 summary.json。指纹包含编辑器版本、manifest/lock、输入设置、参与的验证源/asmdef、matrix.json 与运行器内容；不只记录 git commit 或包版本号。XML 缺失、零匹配、编译失败、超时、Skip/Inconclusive、任一失败均非零退出，不能仅凭编辑器进程返回 0 判通过。人工项必须记录设备、构建指纹与证据，不能自动填 PASS。P0 的 Python 结果解析测试必须覆盖这些假绿路径。

## 先后顺序与停止规则

```text
P0 初始化验证 → P1 双输入验证 → G0 前置验证关卡
→ T2 契约/配置 → T3 核心执行与会话
→ T4 实例加载/布局 → T5 回收/Shutdown → T6 转场/事务接缝
→ T7 输入生产化 → T8 Context 接入/诊断
→ T9 Editor 生成 → T10 示例/性能/文档 → T11 CI/完整平台验收
```

P0/P1 使用测试替身，不等待完整框架。两项必须都通过 G0 才开始 T2；P1 从架构的 1c 入口前移，以免生产核心完成后才发现输入接缝不可行。任一编辑器/后端失败，停止所有生产任务，保留实测证据并在对话确认方案变更；不复制输入模块私有状态机、不反射私有字段、不删验收组合。

G0 记录所有必需用例及实际结果，不以“半天已到”判通过。设备/许可/网络缺失标记环境阻塞，不能当成功，也不能据此推断架构失败。P0/P1 结果经审阅后才进入后续任务；本计划生成不等于 G0 已通过。

每个任务均先提交失败测试/断言并确认其确实失败，再实现、验证并提交独立变更。P0/P1 是引擎能力验证，红阶段使用正向控制暴露的违规顺序/输入泄漏，而不故意改坏引擎或删包制造无关失败。

## P0：初始化顺序前置验证与可审计运行器

**阶段/依赖：** 最先执行；只使用上述宿主的引擎/uGUI/Test Framework，input 宿主安装匹配输入设置的 Input System Runtime；不依赖 UIKit 或 P1 驱动原型。

**Files:** Create `C/run.py`、`C/results.py`、`C/test_results.py`、`C/matrix.json`；Create 公共验证包 `package.json`、`Runtime/LifecycleProbe.cs`、`Editor/FixtureBuilder.cs`、`Tests/PlayMode/InitializationOrderTests.cs` 与接入表列出的三个 asmdef；Create 四宿主的 Packages/manifest.json、Packages/packages-lock.json、ProjectSettings/ProjectVersion.txt、ProjectSettings/ProjectSettings.asset，按 P0 清单写入依赖和输入配置；Modify `R/.gitignore`，忽略四宿主 Library/Temp/Logs/UserSettings、A 和构建输出。Unity 资产与生成的 meta 一并提交。

**Interfaces:** `LifecycleProbe.ProbeOnCreate()` 只记录人为调用点，不伪装为实际 UIView；`FixtureBuilder.Create()` 生成 activeSelf=true 的 Prefab 与序列化依赖。CLI/证据格式按上节，后续任务直接使用。

- [ ] **1. 写解析器失败测试及引擎断言。** 同时建立四宿主最小配置、测试 asmdef 和 LifecycleProbe/FixtureBuilder 测试夹具；这只是运行引擎断言所需的脚手架。解析器测试为 `test_missing_xml_fails`、`test_zero_tests_fails`、`test_skipped_gate_fails`、`test_compilation_error_fails`；正向控制在活跃父节点实例化必须记录 Awake/OnEnable 先发生。`InactiveParent_DefersAwakeUntilActivation` 对克隆实例断言：
  ```csharp
  Assert.That(probe.gameObject.activeInHierarchy, Is.False);
  Assert.That(trace.Events, Is.Empty); // Instantiate 后、ProbeOnCreate 前
  probe.ProbeOnCreate();
  Assert.That(trace.Events, Is.EqualTo(new[] { "OnCreate" }));
  // 激活后断言该实例依次为 OnCreate、Awake、OnEnable，序列化引用非空。
  ```
- [ ] **2. 运行红阶段。** `python3 -m unittest discover -s Tools/Verification -p 'test_results.py'`；CLI 尚未实现，直接用上文原生 Unity 参数运行 init。先以独立 executeMethod 调用 FixtureBuilder.Create，再启动 TestRunner；将待验路径的父节点暂设为活跃，确认期望 OnCreate 在 Awake 前的断言失败，且 trace 有实际事件。结果目录先创建，原生命令例：
  ```sh
  /Applications/Unity/Hub/Editor/2022.3.62f2/Unity.app/Contents/MacOS/Unity -batchmode -runTests -projectPath /Users/tian/Projects/Tian/Wulato.UIKit/Verification/Projects/u2022-legacy -testPlatform PlayMode -testCategory init -testResults /Users/tian/Projects/Tian/Wulato.UIKit/Artifacts/uikit/p0-red/results.xml -logFile /Users/tian/Projects/Tian/Wulato.UIKit/Artifacts/uikit/p0-red/editor.log
  ```
- [ ] **3. 实现准备/测试运行器与非激活夹具。** 所有宿主独立生成；测试再覆盖隐藏 CanvasGroup 不推迟 Awake、非激活父节点才推迟、停用/重启只重跑 OnEnable、非激活时不能依赖组件 Awake 状态。清理只清理本 run 创建的对象。
- [ ] **4. 运行绿阶段。** `prepare --phase p0 --editor all --backend all` 后先验证接入快照、编译与测试发现，再运行 suite init（PlayMode）；每编辑器必须产生实际顺序 trace，克隆首次 Awake=1、重启后 Awake 仍=1。异常/错误日志通过 LogAssert 精确匹配，不吞全局异常。
- [ ] **5. 审阅证据并提交。** 输出 A/<run-id>/init-report.json，记录事件与同一实例 ID；仅提交夹具、宿主配置、工具和解析器测试，建议提交名 `test: verify inactive UI initialization on both editors`。

**退出：** 两编辑器初始化序列成立；当前根工程及其 Packages/manifest.json 不被升级或改写。失败即停止，不写 UIView 生命周期生产代码。

## P1：双输入驱动前置验证

**阶段/依赖：** P0 后、生产任务前。覆盖四宿主，原型不引用 P/I。

**Files:** Create legacy/input 两验证包各自的 package.json、接入表对应的 Runtime/PlayMode asmdef；Create legacy `Runtime/LegacyProbeInputModule.cs`、`Tests/PlayMode/LegacyInputGateTests.cs`，input `Runtime/InputSystemProbeModule.cs`、`Tests/PlayMode/InputSystemGateTests.cs`；公共包 `Runtime/InputEventSpy.cs`、`Runtime/InputGateProbe.cs`、`Tests/PlayMode/InputProbeSceneFactory.cs`；Modify 四宿主 manifest/lock，按 P1 增量清单挂载；Create `C/gates.py`、`C/test_gates.py`，扩展 run.py 的 phase p1 准备与 gate 子命令。

**Interfaces:** 原型控制面统一 `InputGateProbe.SetLocked(bool locked)`、`TransferOwner(int ownerId)`；事件 spy 分别记录 pointerDown/up/click、beginDrag/endDrag/drop、move/submit/cancel、select/deselect。新驱动使用 Input System 测试设备注入；旧驱动通过 BaseInputModule.inputOverride 注入自定义 BaseInput 测试替身，必须经过真实模块 Process 与 EventSystem 路由，不直接 ExecuteEvents 冒充拦截验证。

- [ ] **1. 写泄漏控制与 gate 断言。** 先让两个最小原型只透传真实模块事件，spy/设备注入可运行但门控未实现。不启用 gate 时 Submit/Cancel/点击计数必须 >0；启用后 `Locked_SubmitCancelAreDiscarded`、`PressedBeforeLock_ReleaseAfterUnlockDoesNotClick`、`HeldSubmit_DoesNotReplayOnUnlock`、`DragOwnerTransfer_DoesNotDropOnNewOwner` 均断言旧/新所有者无越权事件。
  ```csharp
  Assert.That(spy.ClickCount, Is.Zero); // 跨锁定的旧 press 不形成 click
  Assert.That(spy.SubmitCount, Is.Zero); // 包含锁中按下、解锁仍按住
  // 全部释放再全新按下后，目标所有者计数应恰好增加一次。
  ```
- [ ] **2. 运行红阶段。** 先执行 `prepare --phase p1 --editor all --backend all`，确认四宿主包路径、引用、InputTestFixture 和 required_cases 均可解析；再运行 `tests --editor all --backend all --mode PlayMode --suite input-preflight`。禁用原型 gate 时对应断言失败，且 spy 的正向控制能接收输入，不能把缺包/编译失败当输入泄漏的红阶段。
- [ ] **3. 实现最小驱动原型。** 只用公开/受保护接口与框架节点门控，验证 Process/DeactivateModule 路径、选择清理和输入 epoch 直到释放；保留原模块源码，不重写其私有状态机。不得仅屏蔽 raycast 后宣布键盘/手柄通过。
- [ ] **4. 运行完整绿阶段。** 测试 pointer/touch、move、Submit、Cancel、press/drag、所有权转移、有效焦点恢复、timeScale=0；两个编辑器及两个后端均通过。恢复后的新输入必须能工作，防止“全局禁用所以没有泄漏”的假通过。
- [ ] **5. 执行 G0 并提交。** P1 改变了公共包及宿主依赖，因此在最终 P1 接入状态重新运行四宿主 suite init 和 input-preflight，再执行 `gate --name preflight`；校验两 suite 与当前指纹匹配、全部必需用例齐全，生成 preflight-report.json，不复用 P0 旧依赖状态的绿结果。建议提交名 `test: verify both UI input gates before runtime work`。原型保留为回归证据，后续仅提取已验证策略进入生产包。

**退出：** G0 全绿才能进入 T2。记录具体拦截 API 与限制；限制若改变 Spec 的输入承诺，先停下确认，不把“人工操作看起来可用”当自动化结论。

## T2：包契约、参数类型与配置快照

**阶段/依赖：** 1a，G0 通过。

**Files:** Create P/package.json、Runtime/Core/Wulato.UIKit.Core.asmdef、Runtime/Core/AssemblyInfo.cs；Create Loading/UGUI 两 Runtime 目录各自的 asmdef 与 AssemblyInfo.cs，先建立可编译引用边界、不填生产行为；Core 下 `IUIContext.cs`、`IUISession.cs`、`UIViewBase.cs`、`UIView.cs`、`UIViewOfT.cs`、`UIIdentity.cs`、`UIArguments.cs`、`UICompletion.cs`、`UIContextSettings.cs`、`UIRegistry.cs`、`UIContracts.cs`、`UIRuntimeDependencies.cs`、`IUISessionLifecycleObserver.cs`、`Scopes/UIResourceScope.cs`、`Scopes/UISessionScope.cs`、`Scopes/UIForegroundScope.cs`、`Diagnostics/UIDiagnosticsSnapshot.cs`、`Transitions/UITransitionContext.cs`、`Input/IUIInputArbiter.cs`、`Input/UIInputPolicy.cs`；Create `P/Tests/EditMode/ContractTests.cs`、`SettingsTests.cs` 及测试 asmdef；Create acceptance 包 `Tests/ContractCompileProbe.cs.txt`、`C/contract_probe.py`，扩展依赖校验和 CLI compile-probe。

**Interfaces:** 公开签名逐字采用 Spec §5、6、6.1 和 loader §9；UIContextSettings 提供 `UIContextSettingsSnapshot CopyValidated()`。设置属性为 LoadTimeout/OpenTimeout/CloseTimeout/EnterTimeout/ExitTimeout/ShutdownBudget(TimeSpan)、CacheCapacity(int)、DiagnosticsLevel；快照为不可变数据。IUIAsyncScheduler 提供 `UniTask SwitchToMainThreadAsync(CancellationToken)`、`UniTask NextFrameAsync(CancellationToken)`、`UniTask DelayAsync(TimeSpan, bool ignoreTimeScale, CancellationToken)` 及单调 `double UnscaledTime`。IUILayoutReady 提供 `UniTask WaitLayoutAsync(UIViewBase, CancellationToken)`；IUITransition 按 Spec 的 EnterAsync/ExitAsync/Stop/Reset 接收同一 UITransitionContext。该 context 包含 Session、AnimationRoot(RectTransform)、AnimationOpacity(CanvasGroup)；诊断快照及注册快照数据形状按 Spec，本任务只定义不可变数据。

UIResourceScope 为抽象 IDisposable 契约，提供 Token、Track(IDisposable)；UISessionScope 额外提供 Id、ContextId、SessionToken、IsCurrent、Scheduler、WaitLayoutAsync(CancellationToken)，UIForegroundScope 使用基础作用域契约。T3 实现内部派生类，不在 T2 用 NotImplementedException 填运行时占位。IUIInputArbiter 本任务先定义 `IDisposable Register(UIContextId, UIInputRegistration)`、`IDisposable AcquireLock(UIContextId)`、`UIInputGrant Prepare(UIInputCandidate)`、`void Commit(UIInputGrant)`、`void Revoke(UIContextId, UISessionId)`；registration 包含优先级及前台变化/用户关闭回调，candidate 包含 Context/Session、View 与策略，grant 含候选资格及所有权代次。T7 只实现协议，不到 T7 才首次声明 T3 需要的类型。

**跨任务内部接缝：** `IUIViewHostFactory.Create(UILayer) -> IUIViewHost`；host 提供 InactiveParent(Transform)、AnimationRoot(RectTransform)、AnimationOpacity(CanvasGroup)，以及 void AttachHidden(UIViewBase)、SetVisible(bool)、SetInteractive(bool)、MoveToCache()、DetachView()、Dispose()。它是第一方 Core↔UGUI 协议，不反向引用 UGUI 程序集；原始 Unity Transform/CanvasGroup 属引擎类型，Core 不引用 UnityEngine.UI 或具体 Canvas 后端。T3 用内存/最小 GameObject 替身，T4 实现实际 Shell。UIRuntimeDependencies 包含 Registry(UIRegistrySnapshot)、Settings(快照)、Loader、Scheduler、HostFactory、LayoutReady、Transition、InputArbiter、ErrorSink、Observers 和只读服务字典；这些类型均在本任务声明，不前向引用后续具体实现。

- [ ] **1. 写测试。** ContractTests 检查两 View 分支不互相继承、None 与带参容器可区分；SettingsTests 断言 30/10/2/2/2/5 秒、容量 8、Basic，拒绝非有限/非正时长与负容量，修改源配置不改变快照。
- [ ] **2. 运行红阶段。** suite contracts（EditMode）；缺实现应为缺类型/断言失败，不能由测试引用遗漏而零匹配。
- [ ] **3. 实现契约和 asmdef。** UIErrorCode、Reason、状态和三个结果类型按 Spec；稳定 key/ID 不依赖 Type.GetType。内部实现只向第一方 UGUI、测试程序集开放，第三方扩展消费公开能力。
- [ ] **4. 验证。** 先执行 `prepare --phase runtime --editor all --backend all`，仅挂已创建的主包、acceptance 和固定 UniTask，再运行 suite contracts；`compile-probe --editor all` 在临时宿主注入 `ui.OpenAsync<ArgsView>()` 必须得到 CS0311，移除非法源后重新编译成功。本任务只验主包依赖、Unity 实际版本、旧 UniTask 拒绝及 Unity 6 的 uGUI 接入；不读取尚未创建的 I/package.json/asmdef。适配包依赖验收归 T7，不能用“宿主已装 Input System”代替其包声明验证。
- [ ] **5. 提交。** `feat: define UIKit contracts and validated settings`；仅契约与测试，没有提前引入 Presenter 或导航实现。

## T3：单次消费、作用域与 FIFO 会话执行

**阶段/依赖：** 1a，T2。

**Files:** Create Core 下 `Async/UIOperationRecord.cs`、`Async/UICompletionQueue.cs`、`Async/UICommandQueue.cs`、`Scopes/DefaultUISessionScope.cs`、`Scopes/DefaultUIForegroundScope.cs`、`Scopes/UIInstanceScope.cs`、`Sessions/UIContextEngine.cs`、`Sessions/UISessionHandle.cs`、`Sessions/UISessionExtensions.cs`；Create `Tests/EditMode/QueueTests.cs`、`ScopeTests.cs`、`SingleConsumptionTests.cs`、`TypeErasedOpenTests.cs`；Create `Tests/PlayMode/SessionLifecycleTests.cs`、`ClosePolicyTests.cs` 及 PlayMode asmdef；测试支持 `Tests/Support/ManualScheduler.cs`、`FakeViewHost.cs`、`ControlledLoader.cs`、`SingleAwaitSource.cs`、`FakeInputArbiter.cs` 与 Wulato.UIKit.Tests.Support.asmdef。Support 标记 TestAssemblies，由 EditMode/PlayMode 测试程序集引用，运行时不引用它。

**Interfaces:** `UIContextEngine(UIContextId id, UIRuntimeDependencies dependencies)` 实现 IUIContext；测试替身实现 T2 协议，不依赖 UIRoot 类型。每原始操作只有 UIOperationRecord 的一次观察，通知队列提供 `void Post(Action)` / `void Drain()`，队列内不执行递归命令。GetService 查询 dependencies 的只读服务视图，GetDiagnostics 从真实引擎状态构建 T2 快照；服务工厂及默认后端的生产组装留 T8，不返回伪造成功值。

- [ ] **1. 写测试。** `CloseThenOpen_UsesFifo`、`FirstOpenFails_SecondMayRetry`、`OpeningTokenCanceledAfterCommit_DoesNotClose`、`ClosedContinuation_ReopensWithoutReentry`、`TwoClosedWaiters_ReceiveSameResult`、`DisposeThrows_OtherResourcesStillDispose`、`PreparingFails_ReleasesEarlierBindings`、`ForegroundEntryFails_OpenFails`。SingleAwaitSource 的 GetResult 次数必须为 1。
- [ ] **1a. 写类型擦除负向测试。** TypeErasedOpenTests 使用真实 UIContextEngine、已注册的带参 ArgsView、在本文件定义的测试参数 ProbeArgs 和 ControlledLoader，调用非泛型 OpenAsync(key,args,ct)。`TypedView_WithNone_FailsBeforeLoad` 使用 UIArguments.None；`TypedView_WithWrongArgumentType_FailsBeforeLoad` 使用 UIArguments.From<string> 而注册要求 ProbeArgs；`PlainView_WithTypedArguments_FailsBeforeLoad` 给无参 View 传非 None 参数。每例同时断言调用本身不抛、await 得到 UIException/InvalidRequest、loader.CreateCount=0、hostFactory.CreateCount=0、无活跃 Session、无缓存或租约遗留、View/会话观察者钩子均未调用。不得依赖 T10 才创建的 InventoryArgs 或示例程序集。
  ```csharp
  UniTask<IUISession> pending = default;
  Assert.DoesNotThrow(() => pending = ui.OpenAsync(argsViewKey, UIArguments.None));
  // pending 只 await 一次，捕获 UIException，断言 Code == UIErrorCode.InvalidRequest。
  Assert.That(loader.CreateCount, Is.Zero);
  Assert.That(hostFactory.CreateCount, Is.Zero);
  Assert.That(viewHookCount, Is.Zero);
  Assert.That(activeSessionCount, Is.Zero);
  Assert.That(outstandingLeaseCount, Is.Zero);
  ```
  正向对照 `TypedView_WithMatchingArguments_OpensOnce` 和 `PlainView_WithNone_OpensOnce` 各得到一次加载、一次 OnOpen 与正确参数，避免把所有类型擦除请求都拒绝而假通过。负向用例必须进入 matrix.json 的 sessions/EditMode 必需列表，不以 CS0311 编译探针替代。
- [ ] **2. 运行红阶段。** suite sessions（EditMode/PlayMode）；使用手动完成源控制顺序，不加碰运气的 Delay。
- [ ] **3. 实现状态机。** 保持 Spec §9 唯一准备顺序，由 fake host 触发激活边界；Single 在执行时判断，参数在加载前校验。取消链接按 Spec §6，scope.Track 在终结后立即释放新资源；观察者逆序终结并聚合错误。
- [ ] **4. 验证。** suite sessions；先验全部类型擦除正/负向案例，错误必须在分配宿主/加载前结束，再逐阶段注入异常与取消，断言钩子配对、Closed 前索引解除、首次原因保留/Shutdown 覆盖、CanClose 否决与确认去重、批量快照不包含后续打开、精确类型关闭拒绝基类。同步钩子/回调嵌套命令快速失败；外部合法排队请求不能因另一异步钩子正在等待而被误判为嵌套。
- [ ] **5. 提交。** `feat: implement scoped FIFO UI sessions`；本阶段仅证明逻辑和真实 View 钩子，不能把 fake host 测试当 Canvas/输入验收。

## T4：实际 Shell、默认加载与布局屏障

> **T4 已完成（2026-11-01）**：提交 `c261ebf`。四宿主 T4 104/104、Unity 全量 490/490、Python 76/76，G0 schema 3 PASS；[验收报告与原始证据](../../../Verification/Reports/2026-11-01-t4.md)。独立审阅的 2 项 Important 已补失败回归并修复。本次止于 T4，T5 未开始。

**阶段/依赖：** 1b，T3；P0 已证明初始化顺序。

**Files:** Modify T2 的 Loading/UGUI asmdef；Create P/Runtime/Loading 下 `PrefabUIViewLoader.cs`、`ResourcesUIViewLoader.cs`、`UIViewLease.cs`；Create P/Runtime/UGUI 下 `UIRoot.cs`、`UIViewHost.cs`、`UIViewHostFactory.cs`、`UILayoutReady.cs`；Create `Tests/PlayMode/LoadingTests.cs`、`ShellLayoutTests.cs`、`LayoutReadyTests.cs` 及对应 prefab 夹具。

**Interfaces:** loaders 实现 `CreateAsync(UIViewAsset asset, Transform inactiveParent, CancellationToken) -> UniTask<IUIViewLease>`；UIViewHostFactory 实现 T2 接缝，UILayoutReady 实现 T2 协议；不在 loader 内创建第二套包装。运行时 Core、Loading、UGUI 引用方向采用 Spec §3.2。

- [x] **1. 写测试。** `LeaseDisposeTwice_ReleasesOnce`、`LoaderFailsBeforeHandoff_OwnsPartialResources`、`InactiveLoad_DoesNotRunAwake`、`NonStretchPrefab_WrapperPreservesRect`、`LayoutInsideOpen_DoesNotQueueCommand`、`CacheParentResize_UsesCurrentLayerRect`。
  ```csharp
  Assert.That(actualRect, Is.EqualTo(directParentReferenceRect));
  Assert.That(loader.ReleaseCount, Is.EqualTo(1));
  Assert.That(probe.OnCreateBeforeAwake, Is.True);
  ```
- [x] **2. 运行红阶段。** suite loading-layout（PlayMode），在四宿主的空 Canvas 夹具中执行，不能依赖 SampleScene 原有内容。
- [x] **3. 实现。** Shell→AnimationRoot→PrefabRoot；包装 stretch/零 offset/单位缩放，Prefab 作者 anchors/pivot/sizeDelta 不变。SetParent/Instantiate 使用 worldPositionStays=false；调用 OnCreate 后才隐藏激活。OnOpen 完成后 NextFrame + ForceUpdateCanvases，再同步捕获基准；不把任意多等帧当就绪保证。
- [x] **4. 验证。** suite loading-layout；覆盖 ContentSizeFitter/LayoutGroup、特殊 pivot、不同 SafeArea/Camera 模式，激活但隐藏时无 raycast。释放时先 DetachView、执行 lease.Dispose，再销毁空包装，不能通过父级 Destroy 越过 loader 的释放职责。
- [x] **5. 提交。** `feat: add leased view hosts and deterministic layout preparation`。

## T5：CacheOne、迟到收容与有界 Shutdown

**阶段/依赖：** 1b，T4；扩展 T3 的清理与状态执行，不另造第二个关闭管线。

**Files:** Create Core 下 `Caching/UIViewCache.cs`、`Async/UIOrphanRegistry.cs`、`Async/UIShutdownBudget.cs`；Create UGUI 下 `UIRuntimeHost.cs`、`UniTaskUIAsyncScheduler.cs`；Modify UIContextEngine、UIOperationRecord、UIViewHost；Create `Tests/EditMode/CachePolicyTests.cs`、`BudgetTests.cs`、`Tests/PlayMode/LateCleanupTests.cs`、`ShutdownTests.cs`。

**Interfaces:** UIViewCache 按 key 取还并以容量作硬上限；UIOrphanRegistry 接收 UIOperationRecord，不接收需再次 await 的原始 UniTask。UIRuntimeHost 持有通知泵和非激活隔离根，生命周期独立于 UIRoot；scheduler 实现 T2，预算使用 UnscaledTime。

- [x] **1. 写测试。** `CapacityZero_Releases`、`CacheOne_LruEvictsOldest`、`CancelAndLeaseHandoffSameFrame_ReleasesOnce`、`NeverEndingLoader_ShutdownDoesNotWait`、`HookFaultsAfterRootDestroyed_IsObserved`、`NotificationPump_SurvivesRootDestruction`、`ErrorSinkThrows_CleanupCompletes`。
- [x] **2. 运行红阶段。** suite cleanup；手动推进时钟，验证总预算是剩余时间而非每界面重新得到 5 秒。
- [x] **3. 实现。** 不健康实例不缓存；隔离任务/租约移交独立所有者，终态结果标记 HasPendingCleanup，错误和未决数量保留。Closed/Shutdown 多方等待均稳定；根销毁、原始打开取消和时限是不同原因，不能互相误报。
- [x] **4. 验证。** suite cleanup（两模式）；关闭后再开得到新 SessionId，旧回调不能改新 View；Late 操作迟到成功/失败各消费一次且释放一次；timeScale=0 不阻止预算推进。Root 销毁后通知队列仍可完成所有句柄，末项 Dispose 抛错也不影响其他租约。
- [x] **5. 提交。** `feat: bound UI shutdown and isolate late cleanup`。

完成记录（2026-11-01）：提交 `aa2aad5`；四宿主全量 Unity 610/610（其中 T5 120/120），Python 76/76，G0 PASS；原始证据和集中作者自审见 [T5 验收记录](../../../Verification/Reports/2026-11-01-t5.md)。仅完成 T5，未进入 T6。

## T6：转场停止、属性复位与事务所有权接缝

**阶段/依赖：** 1c，T5；完整导航包不在本任务。

**Files:** Modify Core/Transitions/UITransitionContext.cs；Create Core 下 `Transactions/IUITransactionAccess.cs`、`Transactions/UITransitionOwner.cs`；Create UGUI 下 `Transitions/ImmediateUITransition.cs`、`Transitions/UITransitionRunner.cs`；Create `Tests/PlayMode/TransitionTests.cs`、`TransactionBoundaryTests.cs`；Modify UIContextEngine 的单一打开/关闭步骤以支持内部事务调用。

**Interfaces:** public 单界面 IUITransition 按 T2/Spec；`UITransitionOwner` 只有 CoreDefault、External。第一方事务接缝提供准备、提交、回滚与关闭步骤，持有同一个 Context 调度占用；CoreDefault 才调用 Enter/Exit，External 交给外部动画。事务能力经显式注册供扩展获取，不要求扩展访问任意内部字段。

- [x] **1. 写测试。** `StopReturns_OperationCanceledAndNoMoreWrites`、`StopCompletedTransition_PreservesResult`、`TransitionCancel_DoesNotWaitTwoSecondLimit`、`OnOpenLayoutChange_IsBaseline`、`ExternalOwner_DoesNotPlayDefaultTransition`、`Rollback_RestoresOldVisibilityAndInput`。
- [x] **2. 运行红阶段。** suite transitions（PlayMode）；受控动画逐帧写入，Stop 后再推进帧，计数及 Transform 必须不再变化。
- [x] **3. 实现。** 只写 AnimationRoot 的声明写集，默认 immediate 无固定等待，动画使用 unscaled 时间。进入初态施加后才打开 Shell 可见门控，结束后提交交互；取消 Stop 同步完成活跃完成源，失败隔离，不再消费原始操作。
- [x] **4. 验证。** suite transitions；进入/退出各 0%、中段、末段取消/失败，业务布局不被 Reset 覆盖；External 不重复钩子/动画，普通命令不能插入事务，Shutdown 仍可打断，嵌套步骤不入队等待自身。
- [x] **5. 提交。** `feat: make UI transitions stoppable and transaction-owned`。

完成记录（2026-11-01）：四宿主 T6 transitions 120/120；直接受影响的 sessions、cleanup、loading-layout 回归合计 428/428；Python 76/76。集中作者自审补失败回归并修复 Stop 同步重播及半提交会话两项问题；[T6 验收记录与原始证据](../../../Verification/Reports/2026-11-01-t6.md)。未运行全量矩阵、G0 刷新或移动端构建；仅完成 T6，未进入 T7。提交 `be8db61`。

## T7：输入仲裁、模态与双驱动生产化

**阶段/依赖：** 1c，P1 和 T6。

**Files:** 实现 T2 Core/Input 契约的 UGUI 后端；Create UGUI 下 `Input/UIInputDomain.cs`、`UIInputArbiter.cs`、`LegacyUIInputDriver.cs`、`UIModalMask.cs`；Create I/package.json、Runtime/Wulato.UIKit.InputSystem.asmdef、Runtime/InputSystemUIInputDriver.cs；Create P/Tests/PlayMode/InputArbitrationTests.cs、ForegroundTests.cs；Create I/Tests/EditMode/AdapterPackageTests.cs、I/Tests/PlayMode/InputSystemArbitrationTests.cs 及两测试 asmdef；Modify input 宿主的 manifest/lock/testables，首次接入实际 I 包。

**Interfaces:** UIInputDomain 在具体 EventSystem 上登记唯一仲裁器；仲裁器负责 Context 注册/注销、候选资格、锁租约、原因路由和所有权代次。UIContextEngine 只提交意图；legacy/new 驱动通过同一门控契约承载 P1 已验证策略，输入后端差异不进入 Core。

- [x] **1. 写测试。** `SecondArbiterForSameDomain_RegisterRejected`、`ModalUnderlay_CannotSubmit`、`TransitionOwner_RetainsBarrier`、`EmergencyTransfer_OldFocusNotRestored`、`LostForeground_CancelsScope`、`RegainedForeground_CreatesNewScope`、`ForegroundCallbackFails_UsesCorrectTerminalPath`；复用 P1 全套跨 epoch 事件断言。此处直接使用生产 UIInputDomain 的登记能力，断言第二仲裁器被拒绝、原仲裁器及原所有者仍有效；不调用尚未实现的 UIContextBuilder.Build，真实 Build 失败用例放 T8。
- [x] **1a. 写适配包声明测试。** `AdapterManifest_PinsCoreAndInputVersions` 检查 I 的 name/version、主包 0.1.0 和 Input System 1.17.0 数字依赖；`AdapterAssemblyReferences_OnlyPointToAllowedRuntimeAssemblies` 检查 I Runtime asmdef，主包/Core 不出现 I/Unity.InputSystem 硬引用，Editor/Test 引用只在测试 asmdef。必须读取本任务实际创建的 I 文件，不能用宿主已安装 Input System 推断通过。
- [x] **2. 运行红阶段。** suite input-runtime（四宿主、PlayMode），正向控制确认解锁和新鲜输入仍工作。
- [x] **3. 实现。** 优先级/最近交互排序、共享 mask、非所有者门控、锁租约、指针状态清理和选择快照；不为锁单 Context 而直接禁用共享 EventSystem。适配包显式注入，不由核心检测包名自动注册。
- [x] **4. 验证。** I 创建后重新运行 `prepare --phase runtime --editor all --backend all`，仅向 input 宿主增加 I；执行 `tests --editor all --backend input --mode EditMode --suite input-package` 验适配包依赖及引用，再跑四宿主 suite input-runtime。验证 Mouse/Touch、键盘/虚拟手柄输入、Submit/Cancel、模态叠加、System 抢占、Root 销毁释放锁、共享输入域的登记拒绝及首次前台/退出通知异常；不把尚未实现的 Builder 测试加入本任务绿阶段。
- [x] **5. 提交。** `feat: integrate modal input ownership across both backends`；保留 P1 测试，不能以原型通过替代生产驱动测试。

完成记录（2026-11-01）：T6 已由用户验收确认。提交 `d5f7262`；四宿主 T7 input-runtime 172/172，两个 input 宿主 input-package 4/4；直接相关 input-preflight、sessions PlayMode、transitions 回归 334/334；Python 定向 18/18。集中作者自审补充失败回归并修复模态叠加、原生拖拽路由、同帧缓存重开及锁/指针清理边界；四宿主负向控制验证防护断言会失败且正向控制仍通过，源码逐字恢复并核对绿色证据指纹。[T7 验收记录与原始证据](../../../Verification/Reports/2026-11-01-t7.md)。未运行全量矩阵、G0 刷新或移动端/IL2CPP 构建；仅完成 T7，未进入 T8。

T7 验收确认（2026-11-01）：用户已确认验收，授权按最新版计划执行 T8，暂不进入 T9。

## T8：Builder、服务/扩展注册与只读诊断

**阶段/依赖：** 1d 的接入部分，T7。

**Files:** Create UGUI/UIContextBuilder.cs、UIContextFacade.cs、Diagnostics/UIDiagnostics.cs；Create Core/Services/UIServiceRegistry.cs；Modify Core/Diagnostics/UIDiagnosticsSnapshot.cs 的快照组装与 UIRuntimeHost；Create Tests/EditMode/BuilderTests.cs、DiagnosticsTests.cs、Tests/PlayMode/RuntimeResetTests.cs；Create 公共验证包 Editor/UIPlayModeResetCheck.cs（沿用 P0 Editor-only asmdef）、C/play_reset.py，扩展 run.py 的 play-reset 子命令。

**Interfaces:** UIContextBuilder(UIRoot, UIRegistry)、WithSettings/WithLoader/WithScheduler/WithTransition/WithLayoutReady/WithErrorSink/WithInputArbiter/WithSessionObserver 返回 builder；WithServiceFactory<TService>(Func<IUIContext,TService>) 按登记顺序提供已创建依赖。Build() -> IUIContext，GetService<TService>() / GetDiagnostics() 完全按 Spec。facade 只组装/转发 T3 引擎，不复制状态机；Build 期间 factory 仅可查询已有服务，不能打开 UI，Build 完成后冻结服务集合。

- [x] **1. 写测试。** `SharedInjectedService_IsNotDisposedByContext`、`FactoryFails_PreviousServicesDisposeReverseOrder`、`UnregisteredService_Throws`、`StoppedContext_ServiceLookupRejectedButSnapshotReadable`、`SourceSettingsMutated_RuntimeUnchanged`、`TwoPlaySessions_NoOldLocksOrSubscriptions`。BuilderTests 另加入 `SecondDefaultArbiter_BuildFailsAndRollsBack`、`DifferentInjectedArbiterForSameDomain_BuildFailsAndRollsBack`、`SharedInjectedArbiter_BuildsTwoContexts`：使用真实 Build，重复/冲突构建失败后本次临时资源释放，首个 Context 的输入与登记不受影响；显式共享同一仲裁器时两个 Context 成功独立运行。
- [x] **2. 运行红阶段。** suite integration（EditMode/PlayMode）；RuntimeResetTests 通过独立 Editor 入口切换 Domain/Scene Reload 的四种组合，各进退 PlayMode 两次，不能仅在单次 TestRunner 生命周期内模拟。
- [x] **3. 实现。** Build 同步校验 Root 独占、实际依赖和输入模块、配置及注册；失败回滚本次资源。诊断快照无 Unity 对象/可操作租约，Basic/关闭采集仍有基础状态；静态桥接 SubsystemRegistration 清理。
- [x] **4. 验证。** suite integration 验真实 Builder 的重复仲裁器拒绝、失败回滚与正确共享路径；附加 `run.py play-reset --editor all --backend all`，确认所有组合完成、退出后活跃计数与旧输入锁为零。未决隔离任务按报告移交，不能通过清空统计隐藏。
- [x] **5. 提交。** `feat: provide explicit UI context composition and diagnostics`。

完成记录（2026-11-01）：T7 已由用户验收确认。T8 四宿主 integration 82/82、直接受影响的 sessions/cleanup/loading-layout/input-runtime 回归 600/600，四宿主 × 四种 Reload 配置共 16/16（32 次真实 PlayMode 进退），Python 定向 24/24。EditMode 夹具按两轮上限暂停后获用户明确追加一轮授权并修复；集中作者自审三项边界回归及 Input-only 模块拒绝均已 RED→GREEN。原始退出报告封存并复核当前指纹，所有最终专项通过。[T8 验收记录与原始证据](../../../Verification/Reports/2026-11-01-t8.md)。未运行全量 suite、G0 或移动端/IL2CPP 构建；仅完成 T8，未进入 T9。实现提交 `f04434b`。

T8 验收确认（2026-11-01）：用户已确认验收，授权按最新版计划执行 T9，暂不进入 T10。

## T9：Editor 绑定、注册生成与构建前校验

**阶段/依赖：** 1d，T8。

**Files:** Create P/Runtime/UGUI/Binding/UIBindingMarker.cs（纯序列化标记，不引用 UnityEditor）；Create Core/Registry/UIRegistryProvider.cs、UIRegistryBuilder.cs，Modify UIRegistry.cs 的生成 provider 引用和快照汇总；Create P/Editor/Wulato.UIKit.Editor.asmdef、UIRootMenu.cs、Binding/UIBindingGenerator.cs、UIBindingWriteback.cs、UIBindingAssemblyRouter.cs、Registry/UIRegistryGenerator.cs、UIRegistryProviderGenerator.cs、UIBuildValidator.cs、UICodegenPipeline.cs；Create P/Tests/EditMode/GeneratorTests.cs、RegistryGenerationTests.cs；Create acceptance 包 Editor/GeneratedAssemblyProbe.cs、Fixtures/Codegen/ViewA.cs.txt、ViewB.cs.txt、DefaultView.cs.txt、BindingUsageProbe.cs.txt；Create C/codegen.py，扩展 run.py 的 codegen/check-generated。

**Interfaces:** `UICodegenPipeline.Generate()`、`UICodegenPipeline.Validate()` 为独立 Editor 菜单/executeMethod；生成目录为消费项目 Assets/Wulato.UIKit.Generated，业务 partial 首次创建后不覆盖，Bindings partial 和显式类型/工厂映射带指纹。分步重载状态由 Editor 工具持久化，生成后等待编译再挂组件/写回引用。

**程序集归属：** UIBindingAssemblyRouter 在生成前通过 CompilationPipeline 获取业务脚本实际程序集及目标 asmdef 资源 GUID，不按命名空间猜测程序集。固定路由如下，生成根目录不放统领所有子目录的 asmdef/asmref：

| View 所属程序集 | Bindings/provider 源码目录 | 引用规则 |
| --- | --- | --- |
| 自定义 asmdef | Assets/Wulato.UIKit.Generated/Assemblies/<asmdef-guid>/ | 该目录生成 Assembly.asmref，JSON reference 为 GUID:<asmdef-guid>，子目录脚本进入原业务程序集；不创建第二份同名 asmdef |
| 预定义 Assembly-CSharp | Assets/Wulato.UIKit.Generated/Predefined/Assembly-CSharp/ | 不创建 asmdef/asmref，导入后核验仍属于 Assembly-CSharp |

每 View 的 `<type-key>.Bindings.cs` 保留原 namespace、类名、访问级别与 partial 声明。已存在业务脚本及其 asmdef 不移动、不修改；Editor-only View 不进入运行时注册。若父目录配置、目标丢失或路由结果使生成片段落入另一程序集，生成/构建校验明确失败，不退回默认程序集。指纹包含 View 脚本 GUID、目标 asmdef GUID/实际程序集名及生成配置；程序集重命名、移动或切换后重新路由，删除工具拥有的旧产物并保留业务文件。

注册代码按同一规则在各业务程序集生成显式工厂与 UIRegistryProvider 子类，UIRegistryProvider 是 Core 的 ScriptableObject 协议，提供 `void Populate(UIRegistryBuilder builder)`；生成的 provider 资产通过 UIRegistry 的序列化引用列表汇总，构建时得到不可变 UIRegistrySnapshot。不得生成 Core→业务程序集或自定义 asmdef→Assembly-CSharp 的反向引用，也不能依靠扫描程序集发现 provider。这样预定义程序集 View 与 autoReferenced=false 的业务 asmdef 均能共存，实际保留行为由 T11 的 IL2CPP 测试验证。

- [x] **1. 写测试。** `SecondGeneration_ProducesNoDiff`、`UserPartialIsNeverOverwritten`、`RenameAndVariant_PreserveBindings`、`CrossPrefabReference_FailsWithAssetLocation`、`DuplicateTypeOrMissingComponent_PreventsBuild`、`StaleFingerprint_PreventsBuild`。
- [x] **1a. 写跨程序集编译与使用探针。** 从 acceptance 模板在消费宿主建立两个不同业务 asmdef 下的 ViewA/ViewB（其中一个 autoReferenced=false），以及默认 Assembly-CSharp 下的 DefaultView。`BindingsPartial_CompilesInOriginalAssembly` 核验三者的生成脚本程序集与业务脚本完全一致；在生成/编译后加入业务侧 BindingUsageProbe partial，访问本类生成字段，并在实例化后确认序列化引用非空。`MixedAssemblyProviders_PopulateRegistryWithoutReverseReferences` 验三 provider 资产均能产生正确注册。再验证 `AsmdefRenameOrMove_ReroutesAndRemovesOnlyOwnedFiles` 和 `WrongAssemblyRoute_PreventsBuild`，不能只检查生成文本包含 .asmref。
- [x] **2. 运行红阶段。** suite editor（EditMode），使用临时 Prefab/Variant 夹具，并验证每项清理不删除用户资产。
- [x] **3. 实现。** 显式 marker + SerializedObject 写回，不运行时按名称找组件；先确定程序集路由并写入 asmref，再生成 Bindings/provider 和序列化引用。生成引用在 OnCreate 前可用，编译/Domain Reload 后恢复未完步骤。构建回调只校验，不首次写 C# 并假设本轮能编译。
- [x] **4. 验证。** suite editor；`codegen --editor all --backend all` 分进程完成夹具基础编译→按路由生成→再次编译/写回→业务字段使用探针编译/执行，再 `check-generated`；所有三程序集的编译、引用非空、混合注册与错误路由拒绝均归入 GEN-01 必需结果，二次生成没有内容差异。以生成映射跑 T3–T8 集成，不继续依赖测试手工注册。
- [x] **5. 提交。** `feat: generate stable UI bindings and registry factories`。

完成记录（2026-11-01）：T8 已由用户验收确认。T9 四宿主 editor 52/52、生成注册映射的 T3–T8 集成 12/12、直接 contracts 回归 64/64；GEN-01 四宿主九阶段共 36/36、最终 check-generated 4/4；Python 定向 25/25。真实三程序集、autoReferenced=false、Prefab Variant/重命名、二次生成无内容差异及 asmdef 改名/移动均有编译和使用证据。Editor-only 夹具按两轮上限暂停后获用户明确追加一轮授权，定位并修复；集中作者自审完成业务赋值冲突与无 Reload 收尾的 RED→GREEN。[T9 验收记录与原始证据](../../../Verification/Reports/2026-11-01-t9.md)。复用既有 Core provider/builder 契约，无重复声明；未运行全量 suite、G0、Reload 矩阵或移动端/IL2CPP 构建，未进入 T10。实现提交 `2dfd981`。

T9 验收确认（2026-11-01）：用户已确认验收，授权按最新版计划执行 T10，暂不进入 T11。

## T10：三个样例、性能基线与接入文档

**阶段/依赖：** 1d，T9。

**Files:** Create P/Samples~/QuickStart/SettingsView.cs、SettingsScene.unity；Modals/StackedModalView.cs、ModalsScene.unity；Inventory/InventoryArgs.cs、InventoryView.cs、InventoryScene.unity 及对应 Prefab/Bindings/asmdef；Create P/Documentation~/QuickStart.md、Lifecycle.md、CancellationAndErrors.md、Extensions.md、Performance.md；Create P/CHANGELOG.md；Create 独立验证包 V/Packages/com.wulato.uikit.acceptance.samples/package.json、Tests/PlayMode/SampleAcceptanceTests.cs、PerformanceBaselineTests.cs、Wulato.UIKit.Acceptance.Samples.asmdef；Create C/samples.py，为 run.py 增加 prepare-samples 子命令，扩展诊断 ProfilerMarker 采样。

**Interfaces:** 样例仅使用批准的 IUIContext/Session API，输入模块由宿主选择；无 TMP、全局单例或真实 Presenter/Dialogs/Queue。异步确认测试使用外部流程替身，不能偷偷依赖未交付的 Dialogs 包。

示例业务 asmdef 名分别为 Wulato.UIKit.Samples.QuickStart、Wulato.UIKit.Samples.Modals、Wulato.UIKit.Samples.Inventory；导入后它们是实际生成目标，Bindings 通过 T9 的 GUID 路由进入同一程序集。SampleAcceptanceTests/PerformanceBaselineTests 的专用包只在导入全部示例并确认这些程序集存在后，才以 `file:../../../Packages/com.wulato.uikit.acceptance.samples` 安装到消费宿主并加入 testables。其测试 asmdef 引用三个示例程序集及框架/测试依赖；未导入示例的宿主不安装此包，不留失效程序集引用，也不要求运行 samples/performance suite。主包通用 PlayMode 测试与 T2 的基础 acceptance 包均不引用样例类型。

- [x] **1. 写测试。** `Settings_OpenAndOwnerClose`、`TwoModals_RestoreFocus`、`InventoryArgs_DisplayCorrectTab`、`FiftyCacheCycles_LeaseCountDoesNotGrow`，Release 稳定后实例/租约为 0，CacheOne 最多为 1，容量约束仍生效。
- [x] **2. 运行红阶段。** `prepare-samples --editor all --backend all` 按“导入 Samples~ → 确认三个 asmdef → 安装专用样例测试包”顺序准备消费宿主，再运行 suite samples（PlayMode）；缺引用/字体/场景均作为失败，不从根工程 TMP 环境借依赖。
- [x] **3. 实现样例与采样。** 阶段级 ProfilerMarker 只包同步片段，异步经过时间另记；隔离场景采样 GC/Canvas rebuild，不用线程分配差值归因单异步命令。
- [x] **4. 验证并写文档。** suite samples 与 suite performance（PlayMode）；每代表 Modal 测首次加载、预热释放重建、CacheOne 重开各 50 次，记录编辑器/Player、输入后端、诊断配置、分配及实例计数，不发明固定 GC 阈值。文档能从空项目复现安装、注册、生成、打开/关闭和错误处理。
- [x] **5. 提交。** `docs: ship UIKit samples and measured acceptance baseline`。

完成记录（2026-11-01）：T9 已由用户验收确认。T10 四宿主 samples 20/20、performance 8/8，代表 Modal 三种模式各 50 次、共 600 次正式测量；直接受影响 editor/generated-integration/sessions/cleanup/loading-layout/transitions 回归 612/612，合计 640/640。Python 定向 47/47；最终样例只读校验四宿主两阶段 8/8、check-generated 4/4；40 个测试 attempt 的原始 XML/log、必需用例、逐例 trace、证据哈希、batch 绑定及当前指纹独立复核均通过。集中作者自审完成；场景夹具与原 T9 固定映射假设已在对应专项修复复验。[T10 验收记录与原始性能/资源证据](../../../Verification/Reports/2026-11-01-t10.md)。未运行全量 suite、G0、Reload 矩阵或移动端/IL2CPP 构建；Player 性能未测量，T11 未开始。实现提交 `24ec3a7`。

T10 验收确认（2026-11-01）：用户已确认验收，授权按最新版计划执行 T11，要求完整验收矩阵与原始证据；移动端/IL2CPP 不得由 Editor 结果替代。

## T11：CI、干净接入与完整移动端发布关卡

**阶段/依赖：** 第一阶段最终关卡，T10；环境检查可提前记录，但不能用它替代最终测试。

**Files:** Create `.github/workflows/uikit-checks.yml`、`uikit-mobile.yml`；Create C/build.py、C/smoke.py、C/release_gate.py、C/test_release_gate.py、P/Editor/Verification/UIPlayerBuild.cs；acceptance 包 Create Runtime/MobileSmokeRunner.cs、Editor/MobileSmokeSceneBuilder.cs、Tests/PackageInstallTests.cs；Create `V/README.md`、`docs/verification/phase1-environment.md`。

**Interfaces:** `run.py build --editor <version> --backend <backend> --target Android|iOS --profile smoke|clean` 调用 `UIPlayerBuild.Build()`；`run.py smoke ... --device <id>` 校验部署/运行产生的结果；`run.py gate --name release` 读取完整结果矩阵。smoke profile 包含独立的非 TestAssemblies 移动验证入口，clean profile 仅包含消费样例和运行时包；二者不得混淆为正式构建。构建 ID 包含 spec/package/commit、编辑器、后端、平台与依赖锁指纹，禁止复用不对应的旧设备报告。

- [x] **1. 写门禁失败测试。** 缺一组合、仅 iOS 导出而未编译/启动、设备报告 build-id 不符、零 smoke 用例、旧包指纹、生成后未重新编译均使 release_gate 非零；CI/SDK/设备问题标环境阻塞而非 PASS。
- [x] **2. 验证红阶段。** `python3 -m unittest discover -s Tools/Verification -p 'test_*.py'`；构造报告缺项证明 gate 会拒绝，不为“让 CI 绿”跳过人工项。
- [x] **3. 实现编排。** macOS 自托管执行器按版本/后端独立宿主；变更运行双编辑器 EditMode/PlayMode，合入前运行 Android，发布前完整 8 组合。Android 安装启动，iOS Xcode 编译/签名并真机启动；签名凭据由执行环境提供、不写入仓库。
- [x] **4. 验证完整绿阶段。** 干净 Git 安装主/适配包 → 生成 → 编译 → 校验 → 全量测试 → 平台构建 → 安装启动；移动 smoke 覆盖直接引用、Resources、类型擦除、缓存、触摸关闭、Shutdown 和 IL2CPP 裁剪。iOS 人工触发证据归档，设备缺失保持关卡未完成。
- [x] **5. 完成审阅并提交。** 29 组 Spec 验收全部有结果；额外用两个编辑器构建 clean profile Android 输入适配样例，BuildReport 证明不含验证/Test 程序集。smoke 的专用入口不作为 clean profile 的证据。记录依赖解析与完整平台证据后提交 `ci: gate UIKit phase one on the full mobile matrix`；不自动打 tag、发布或合并。

最终验收记录（2026-11-01）：T10 已由用户确认。T11 规定完整矩阵已通过：Editor 全量 EditMode 338/338、PlayMode 750/750（1088/1088），Python 129/129，Reload 16/16（32 次真实进退），固定 Git 干净接入/生成/编译 4/4，样例校验 8/8、check-generated 4/4、编译探针 6/6；Android/iOS ARM64 IL2CPP 完整 8 组合、56/56 实体设备用例；iOS 四项完成 Xcode 编译、田茂弟个人开发证书签名、安装、启动。额外两编辑器 Android Input System clean 构建 2/2，实际 Player 程序集清单无验证/Test 程序集。最终 release gate 重读全部原始数据、必需用例、trace、哈希和当前指纹，24/24 项 PASS、退出码 0。29 组 Spec 均可追溯且有通过结果。原“缺少移动模块”的路径误判已撤回；包测试引用仓库 Verification 的编译错误、移动夹具参数及无摄像机闪烁已修复，签名账号由用户恢复后四项真机通过。历史失败完整保留，当前无验收或环境阻塞。[T11 最终报告与证据](../../../Verification/Reports/2026-11-01-t11.md)，[环境记录](../../verification/phase1-environment.md)。集中作者自审完成，无子代理；用户已确认将原已暂存的 Spec/Plan 与本轮实现及报告一并本地提交；T11 收尾完成，未推送、合并、打 tag 或发布。

## 验收追踪与完成定义

| Spec ID | 负责任务 | 首次证明/最终证明 |
| --- | --- | --- |
| LIF-01 / EXT-01 / CLOSE-01 / CLOSE-02 / BATCH-01 | T3、T8 | 生产状态机 + 受控后端；再用真实宿主集成 |
| CAN-01 / CON-01 / CON-02 | T3、T5 | 单次消费、同帧竞争、FIFO、重入及迟到结果 |
| CFG-01 / API-01 | T2、T3、T8 | 参数/配置、CS0311 负向编译；T3 类型擦除错误参数在宿主/加载前拒绝，正向对照成功；Builder 组装 |
| LAY-01 / SHELL-01 | P0、T4、T6 | 原始引擎顺序关卡 → 真实 Shell/布局/转场 |
| INP-01 / INP-02 | P1、T7、T8 | 原型真实模块路由 → 两生产驱动/域登记拒绝 → 真实 Builder 重复仲裁器失败与资源回滚 |
| FORE-01 / ISO-01 | T7、T8 | 作用域重建、异常资格撤回、跨 Context 所有权 |
| CAC-01 / LATE-01 / SHUT-01 / ERR-01 | T3、T5、T6 | 回滚/回收/错误聚合、根销毁及有界退出 |
| TRN-01 / TRN-02 / TXN-01 | T6 | Stop 收敛、写集复位、External 唯一转场所有者 |
| DIAG-01 / PLAY-01 | T8 | 快照只读/服务所有权、四种 Reload 配置 |
| GEN-01 | T9、T10 | 幂等、Variant、重载写回；两个业务 asmdef + 默认程序集的 partial/provider 编译与使用；错误路由和过期产物阻止构建 |
| PERF-01 | T10 | 有条件记录的 50 次基线，生命周期计数无增长 |
| PKG-01 / PLAT-01 | T2、T7、T9、T11 | T2 主包依赖；T7 实际适配包声明/接入；干净 Git 安装、生成校验、完整 8 组合 |

- [x] G0 的初始化与双输入验证通过，证据明确且未被当作生产实现替代。
- [x] T2–T11 的任务级测试通过，原型/支持包不被正式运行时依赖。
- [x] 29 个验收组全部可追溯到测试或设备结果，不用“测试计划已写”代替通过。
- [x] Spec 未被执行者静默改写；如果验证推翻契约，先停止并确认变更。
- [x] 构建、测试、干净安装、iOS 真机与 Android 启动证据齐全；没有自动发布动作。

## 计划自检与交接

生成计划时已做静态自检：Spec 的核心、加载/缓存、作用域/取消/异常、输入、Editor、示例与平台要求均有任务；29 组验收完成映射；前置原型有分阶段宿主挂载清单，不依赖待实现 UIKit；适配包与 Builder 验收位于其实现之后；类型擦除负向断言和跨程序集生成探针均进入必需用例。此处是计划覆盖检查，不是运行验证。

批准方案不等于批准执行本计划。按本轮要求，停在计划交付；后续明确启动执行时先核对基线，再运行 P0/P1。运行前检查 Unity 许可、SDK、设备与签名条件，环境不足如实记录；不预先假定所有移动端工具链已安装。

参考命令依据：[Unity Test Framework CLI](https://docs.unity3d.com/Packages/com.unity.test-framework@1.1/manual/reference-command-line.html)、[Unity 6000.3 Test Framework 为编辑器匹配核心包](https://docs.unity3d.com/6000.3/Documentation/Manual/com.unity.test-framework.html)。具体版本以本机对应编辑器 package.json 和锁文件为验证证据。

宿主与代码生成依据：[本地 file dependency 的路径基准](https://docs.unity3d.com/2022.3/Documentation/Manual/upm-localpath.html)、[Input System 测试程序集及 InputTestFixture](https://docs.unity3d.com/Packages/com.unity.inputsystem@1.17/manual/Testing.html)、[asmref 将外部目录编入目标程序集](https://docs.unity3d.com/2022.3/Documentation/Manual/ScriptCompilationAssemblyDefinitionFiles.html)。
