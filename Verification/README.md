# UIKit verification

当前任务入口：[Master Optimization Plan](../docs/superpowers/plans/2026-11-03-wulato-uikit-master-optimization.md)。开发默认 Unity `2022.3.62f2`；其他版本为显式兼容目标。下文旧阶段命令及报告为历史资料，不自动构成本轮门禁。

M6 干净安装使用现有 `build --phase phase3 --profile clean --prepare-only`，显式选择默认编辑器；组合为 main/legacy、addressables/legacy、all/legacy、all/input。`--target Android` 在此仅保留安装记录身份，不构建 Player。隔离 Git 快照强制收录授权包目录（不受用户忽略规则影响），逐文件核对提交清单及 SHA-256，并保存 `package-snapshot.json`；样例和 `.meta` 必须从该固定提交读取，不能用快照工作目录的脏文件或未跟踪文件补齐。原始证据索引包含快照清单。安装通过仍要求生成/编译和实际依赖隔离通过；预期退出 1/BLOCKED 的唯一允许原因是 `prepare-only; no mobile Player built`，不等于移动构建通过。2026-11-03 的旧安装证据漏收样例/文档，完整交付结论由 M6 补验替代。

The preflight packages are verification prototypes, independent of production UIKit. The separate acceptance package verifies T2 contracts and actual resolved dependencies. Run commands from the repository root on macOS with the two specified Unity editors installed.

```sh
python3 -m unittest discover -s Tools/Verification -p 'test_*.py'
python3 Tools/Verification/run.py prepare --phase p0 --editor all --backend all
python3 Tools/Verification/run.py tests --editor all --backend all --mode PlayMode --suite init
python3 Tools/Verification/run.py prepare --phase p1 --editor all --backend all
python3 Tools/Verification/run.py tests --editor all --backend all --mode PlayMode --suite init
python3 Tools/Verification/run.py tests --editor all --backend all --mode PlayMode --suite input-preflight
python3 Tools/Verification/run.py gate --name preflight
```

The four hosts use separate Library/Temp directories. Never open a host with another Unity version. The runner checks the app bundle version before opening a host and checks the actual editor version in its log. Tests use batchmode with graphics, without `-quit`.

`input-preflight` maps to NUnit Category `input_preflight`: NUnit rejects hyphens in category names. Backend test assemblies reference the common PlayMode assembly for shared scenarios. `TestAssemblies` supplies TestRunner/NUnit references; explicit duplicate TestRunner references are omitted because Unity rejects them.

`UIKIT_RED_INIT=1` deliberately uses the active-parent control in the inactive-parent assertion. `UIKIT_RED_INPUT=1` makes the gate a pass-through control. Both are expected to fail their safety assertions; use only for regression-control runs. The unlocked input control must pass, proving that a quiet gate test is not caused by absent input. Callback-triggered lock/transfer and initial nonowner-click cases are mandatory as well.

The legacy backend injects a BaseInput override and exposes held state through ILegacyReleaseState, because BaseInput only exposes button-down edges. The default release provider polls public Unity Input APIs. The Input System backend uses an isolated InputTestFixture and synthetic Mouse/Keyboard/Gamepad/Touchscreen devices. All input runs through the actual module Process method. ExecuteEvents is used only for explicit cancellation notifications on framework-owned nodes, never to inject test input.

On revocation, the node snapshot sends pointerUp/endDrag without click/drop, selection is cleared, and the module resets via public DeactivateModule / enabled lifecycle. The legacy module's DeactivateModule clears protected pointer data. For Input System, disabling/re-enabling invokes its own pointer-reset lifecycle. InputGateNode snapshots callback data because Input System reuses PointerEventData between mouse buttons. InputEventSpy only records delivered events. Within a Process call, revocation clears current eligibility and cached pointer raycasts/deltas so later mouse buttons cannot use a pre-lock target. Legacy invalidates all protected pointer data; Input System shares its observed mouse event data between button dispatches. Native releases first drain their snapshots once, then any remaining presses are canceled and the module resets when Process exits. Tests cover left/right/middle release identities, lock/transfer and fresh side-button recovery. No input receiver or spy is disabled to hide delivery. The live references are discarded at that boundary. Initial ownership is applied explicitly before processing any input. Processing resumes only after relevant UI controls are neutral, followed by a fresh input edge. Input System release checks enumerate the UI actions’ resolved controls; unrelated held gameplay keys do not quarantine the UI.

Each batch writes an independent batch.json inventory before creating/launching its attempt directories. Schema-3 run.json and summaries bind their batch ID, inventory SHA-256 and declared host/suite. Each attempt then writes XML, logs, per-case traces, host snapshots, command.json and summary.json under ignored Artifacts/uikit. evidence-index.json seals the raw files with SHA-256 and its own digest is recorded in the summary. G0 discovers attempts from the batch inventory even if run.json or an entire attempt directory disappears. Missing/unknown registration schemas fail closed, and orphaned records cannot silently disappear. Per-host start times are used when valid, with inventory time for missing registrations. A newer intact retry can supersede an older inventoried failure; deleting its registration cannot restore an older PASS. It checks the index, re-parses raw XML/logs, compares summary cases and snapshot/configuration, and requires every fixture-qualified case and its trace. Current fingerprints cover all host Assets/ProjectSettings/Packages, verifier package contents and runner/matrix sources. Canonical local package sources and lock entries are checked; a same-named alternate source is rejected. Changes during a run invalidate its result. Missing or changed raw evidence, skipped/inconclusive/failed cases or parent suites, compiler errors, timeout and nonzero process exit all fail closed.

Limits: these are Editor preflight tests, not mobile/IL2CPP release acceptance or production UIKit tests. Input System Submit/Cancel uses keyboard keys; navigation uses the gamepad stick. Physical gamepad Submit/Cancel buttons are not claimed as verified. T2 has separate contract acceptance below; T3 logical session execution is verified below; T4 actual hosting/loading/layout is verified below; T5 cache/bounded cleanup is verified below; T6 stoppable transitions and the transaction boundary are verified below; T7 production input arbitration and both drivers are verified below; T8 composition is covered by the integration and independent play-reset checks below; T9 generation and T10 imported samples/performance are covered below; T11 orchestration is described below; complete mobile/IL2CPP acceptance remains pending until its release gate passes.

Verification prepare/tests batches use a nonblocking process-wide filesystem lock for the artifact root. Overlapping CLI batches fail explicitly instead of interleaving host execution; G0 uses a shared guard and returns FAIL while a batch is active. G0 schema 3 requires the serialized-batch inventory/evidence binding. Historical schema-2 records remain readable but cannot satisfy the current evidence verifier. Unknown inventories require restoring valid provenance, not silently skipping them.

## 当前开发入口

开发默认 2022.3.62f2 / legacy；额外编辑器显式选择。Tools/Verification/editors.json 的 default 指定开发默认，targets 指定 all 的目标集合，profiles 保存准确宿主名及 uGUI/Test Framework/Input System/Addressables/SBP 约束。新增 profile 还须准备匹配版本的宿主、对应 matrix 必需用例和验证夹具；缺配置或版本不符失败，不从版本号推断依赖。

```sh
python3 Tools/Verification/ci_suites.py --base HEAD --plan
python3 Tools/Verification/ci_suites.py --base HEAD --prepare
python3 Tools/Verification/ci_suites.py --base HEAD
python3 Tools/Verification/run.py tests --mode PlayMode --suite context-host
python3 Tools/Verification/run.py tests --backend all --mode EditMode --suite preview-authoring
python3 Tools/Verification/run.py tests --backend all --mode PlayMode --suite preview-runtime
python3 Tools/Verification/run.py play-reset --backend all --scope context-host
python3 Tools/Verification/run.py play-reset --backend all --scope preview
```

变更选择包含基线至 HEAD、暂存/未暂存及未跟踪相关文件；建议包含原因、精确宿主与准备步骤。普通模块不默认扩大宿主，元数据和公共边界的跨后端影响通过 --backend all 显式覆盖。准备与测试耗时记录在 Artifacts/uikit/ci-selected；原 runner 的命令、XML、trace、指纹、锁和必需用例规则继续有效。

context-host/preview 的独立 Editor Reload 验证使用现有 play-reset 执行器，Legacy 四组合、input 默认及双禁用，各连续两次；这些证据不能代替完整 integration Reload 或移动发布矩阵。preview 需要先 codegen/prepare-samples，恢复记录及临时场景来自真实生产预览路径。

主包内含四业务模块，安装 main 不安装 Addressables/Input System；addressables 仅增适配和 SDK，all 按显式输入后端组合。旧单业务包安装参数返回迁移错误。SDK 与编辑器依赖以 profile 为准。

以下为历史阶段说明及证据索引；旧包数量、版本和固定四宿主命令不作为当前任务要求。

## T2 contracts

```sh
python3 Tools/Verification/run.py prepare --phase runtime --editor all --backend all
python3 Tools/Verification/run.py tests --editor all --backend all --mode EditMode --suite contracts
python3 Tools/Verification/run.py compile-probe --editor all
```

Runtime preparation preserves the P1 backend packages and testables, mounts only the main package, acceptance package and pinned UniTask, and does not install the future UIKit Input System adapter. It refuses incompatible existing UniTask declarations/locks and missing or incorrect explicit uGUI before changing the host manifest. Correct those inputs explicitly before retrying. It checks main-package metadata and runtime assembly references, then captures Unity PackageInfo versions/sources/resolved paths in dependency-snapshot.json. Four host lock files are committed.

The contracts suite checks argument branches/typed null, identity, immutable results/diagnostics/registry descriptors, registration constraints and validated settings. TimeSpan cannot represent NaN or infinity; construction rejects them, while CopyValidated rejects nonpositive tick values. Registry descriptor copies preserve the referenced prefab but isolate serialized configuration mutations.

compile-probe uses a newly created temporary Editor assembly in the legacy host for each editor: valid typed call must compile, the no-argument typed View call must fail with CS0311 as its only compiler error, and a corrected source must compile again. It refuses to overwrite an existing probe directory, removes only its own files in finally, and checks the original source fingerprint afterward. Evidence lives under Artifacts/uikit/contract-probes. These are contract/compilation checks; they do not claim Session execution, mobile compatibility or a finished installable runtime.

## T3 scoped FIFO sessions

The `sessions` category now covers the internal UIContextEngine with real UIView hooks and controlled protocol backends. Four hosts require 22 EditMode and 29 PlayMode session cases each in matrix.json. Type-erased mismatches must fail asynchronously before host/load allocation; positive controls must open once. SingleAwaitSource verifies one GetResult for original operations, including cancellation/delivery races and late worker faults. ManualScheduler explicitly queues worker-to-main handoffs.

```sh
python3 Tools/Verification/run.py tests --editor all --backend all --mode EditMode --suite sessions
python3 Tools/Verification/run.py tests --editor all --backend all --mode PlayMode --suite sessions
```

Full `--suite all` runs also retain T2 contracts and P0/P1 regressions. See [T3 acceptance](Reports/2026-10-31-t3.md) for current raw evidence and independent-review corrections. User-close deduplication covers queued/in-progress requests; business confirmation flows own their pending flag and may retry after a veto or exception. Core is internal until T8 builder assembly. This is logical session acceptance with fake hosts, not T4 Canvas/layout/default loading, T5 cache/independent cleanup host/deadlines/bounded Shutdown, or production input/transition acceptance. No T4 work is included.

## T4 actual loading, Shell and layout

`loading-layout` maps to NUnit Category `loading_layout`. Four hosts require 26 PlayMode cases each, using committed serialized Prefabs from the main package's Tests/Fixtures/Resources/T4 directory and newly created empty Canvases. The Editor-only T4FixtureBuilder.Create can regenerate these assets; it is not a runtime dependency. The native fixture-generation logs and Prefab hashes are retained under Artifacts/uikit/t4-fixtures.

```sh
python3 Tools/Verification/run.py tests --editor all --backend all --mode PlayMode --suite loading-layout
```

Tests exercise default Prefab/Resources leases, inactive initialization, actual Shell geometry, optional authored SafeArea in Overlay/Camera modes, parent-resize relocation, real GraphicRaycaster positive/negative controls, LayoutGroup/ContentSizeFitter and exactly one Update layout preparation. Dynamic configuration guards reject unsupported Canvas/CanvasGroup bypasses at frame/render, layout and gate boundaries, including async preparation. The cache relocation test uses Host directly; no CacheOne/LRU, timeout or independent cleanup runtime is implemented. See [T4 acceptance](Reports/2026-11-01-t4.md) for final full regressions, raw evidence and the two independent-review corrections. Work stops before T5.


## T5 cache, late cleanup and bounded Shutdown

The `cleanup` suite requires 15 EditMode and 15 PlayMode cases per host. It covers hard-capacity healthy CacheOne/LRU reuse, fresh session scopes, late single-consumption cleanup, root-independent notifications/isolation, one absolute Shutdown budget, cancellation races and timeScale=0. Manual clocks verify deadline sharing; PlayMode also uses actual Shells, root destruction and the Realtime scheduler.

```sh
python3 Tools/Verification/run.py tests --editor all --backend all --mode EditMode --suite cleanup
python3 Tools/Verification/run.py tests --editor all --backend all --mode PlayMode --suite cleanup
```

Final full regressions: 610 Unity cases including 120 T5 cases, Python 76 cases and G0 PASS. See [T5 acceptance](Reports/2026-11-01-t5.md) for raw evidence, author self-review, fixes and scope. Pending cleanup remains owned until actual completion; stopped diagnostics are immutable snapshots. Work stops before T6; no mobile/IL2CPP release acceptance is claimed.


## T6 stoppable transitions and transaction ownership

The `transitions` suite requires 30 PlayMode cases on each of the four hosts. It checks synchronous Stop convergence, no writes after Stop across later frames, completed-result preservation, reentrant restart, unscaled playback, immediate defaults, fresh open/close baselines, business layout preservation, unhealthy-instance isolation, and explicit External ownership. Controlled enter/exit tests exercise initial, intermediate and late frame boundaries; exit cancellation is driven by its independent timeout token, not the already-canceled Session token.

```sh
python3 Tools/Verification/run.py tests --editor all --backend all --mode PlayMode --suite transitions
```

The transaction capability is registered explicitly by the internal composition seam (`UIRuntimeDependencies.enableTransactions`); T8 will expose composition through Builder. `IUITransactionAccess.ExecuteAsync` owns the existing FIFO for its callback. Its steps prepare without foreground/input commit, commit once, roll back uncommitted preparations, and close after commit. Commit is the irreversible boundary: exceptions before it restore the previous visible/input owner; extensions perform history changes within their synchronous commit sequence. `AnimateAsync` lets an External owner use a custom adapter without default Enter/Exit. External animation is observed once and synchronously stopped/reset on cancellation. Steps are serialized, cannot escape their transaction, and must be awaited. Ordinary commands submitted during a transaction wait behind it; a transaction callback must use the direct steps instead of awaiting public commands on the same Context.

Shutdown interrupts a transaction from outside the queue. An active core step settles before the queue advances; an uncooperative extension callback is observed separately and loses all step access. Unfinished preparations are rolled back. No Navigation history, paired navigation adapter, production input driver, mobile or IL2CPP release acceptance is included. See [T6 acceptance](Reports/2026-11-01-t6.md) for precise evidence and direct regressions.


## T7 production input ownership and both drivers

`input-runtime` exercises the production UIInputDomain/UIInputArbiter and explicitly supplied drivers on four hosts: 42 legacy cases and 44 Input System cases per editor. It preserves the complete P1 epoch scenarios and adds modal stacking/barriers, emergency transfer, stale grants, domain registration rejection, root cleanup, foreground scope/error paths, native ScrollRect drag routing, same-frame CacheOne reuse and arbiter-disposal cancellation. Input System coverage includes synthetic Mouse/Touchscreen/Keyboard/Gamepad devices and actual gamepad Submit/Cancel bindings. Receivers are observers; input flows through native module Process, with ExecuteEvents used only to cancel old pointer state.

```sh
python3 Tools/Verification/run.py prepare --phase runtime --editor all --backend all
python3 Tools/Verification/run.py tests --editor all --backend input --mode EditMode --suite input-package
python3 Tools/Verification/run.py tests --editor all --backend all --mode PlayMode --suite input-runtime
```

Runtime preparation now adds the actual `com.wulato.uikit.input-system` package only to input hosts. Its metadata pins the main package to 0.2.0 and Input System to 1.17.0. The two `input-package` cases per input host read the actual package manifest/asmdef and resolved versions. Adapter sources participate in canonical-path and fingerprint checks. No automatic optional-package discovery is introduced into Core.

Final T7 module acceptance is 176/176; direct P1 input, sessions PlayMode and transitions regressions are 334/334, with 18/18 targeted Python tool tests. See [T7 acceptance and raw evidence](Reports/2026-11-01-t7.md), including the four-host negative controls and exact source restoration. Builder/default composition and real Build rollback remain T8; no full acceptance matrix or mobile/IL2CPP release acceptance is claimed.


## T8 explicit composition, services and diagnostics

`integration` exercises the real UIContextBuilder/facade, registry/settings snapshots, service lookup and reverse disposal, rollback, root exclusivity and input-domain ownership. It uses the installed backend on each host. Input-only hosts reject a Legacy driver; multiple Contexts explicitly share a host-owned arbiter. A failed default Build leaves the original owner running; real View open/close and foreground transfer provide positive controls.

```sh
python3 Tools/Verification/run.py tests --editor all --backend all --mode EditMode --suite integration
python3 Tools/Verification/run.py tests --editor all --backend all --mode PlayMode --suite integration
python3 Tools/Verification/run.py play-reset --editor all --backend all
```

Service factories run in registration order and can only query earlier services while Build is in progress. Commands are unavailable until construction completes. The collection is frozen afterward; owned factory results are disposed in reverse order on Shutdown, Root destruction or rollback. Injected shared dependencies remain caller-owned. Diagnostics remain readable after stopping, omit detailed entries at Off, and expose copied values rather than runtime objects.

`play-reset` starts independent matching-version Editors, selects each of the four Domain/Scene Reload configurations and enters/exits actual PlayMode twice. Contexts, a service subscription and an outstanding lock remain alive until Unity exits. A probe observes automatic teardown after quit callbacks; it does not call Shutdown or clear counters in that path. Both automatic-exit records and the final two-session report are mandatory. Missing reports, nonzero counters, wrong configuration/version, compiler errors, timeout or changed input fingerprints fail the check. Original Editor reload settings are restored. Ordinary PlayMode test-runner cases are additional coverage, not a substitute for this entry point.

T8 and its direct regressions do not require `suite all`, G0 refresh or Player builds. They do not establish mobile/IL2CPP or whole-phase release acceptance. See [T8 evidence and scope](Reports/2026-11-01-t8.md).

## T9 binding generation and explicit registry providers

Author a concrete, top-level partial `UIView` / `UIView<TArgs>` script (the `Assets/Create/Wulato/UIKit/View partial` menu creates it once). Add a root `UIBindingMarker` to an active RectTransform Prefab, assign the business MonoScript, and explicitly list binding field names and Component references. Object names are unrestricted. Select one registered Prefab per View type: when registering a Variant, exclude its authoring base with `includeInRegistry`. Known Editor-only scripts are excluded from runtime discovery; broken references still fail validation.

Use `Tools/Wulato/UIKit/Generate`. Generated bindings stay in the original business assembly through a GUID asmref, or in Assembly-CSharp without an asmdef/asmref. The generated root is `Assets/Wulato.UIKit.Generated`; the generated `UIRegistry.asset` references provider assets explicitly. Supply that registry to `UIContextBuilder`. Business files and asmdefs are preserved. Bindings are private serialized fields available to the business partial and before `OnCreate`; no runtime lookup by object name is used.

Generation persists pending writeback in the host Library. After compilation/reload, the Editor resumes component attachment and SerializedObject reference writeback. `Complete writeback` is also available as a menu/executeMethod recovery step. An unchanged generation completes immediately because it cannot trigger another script reload. Build preprocessing only validates; missing, stale, misrouted or incomplete outputs block the build.

```sh
python3 Tools/Verification/run.py codegen --editor all --backend all
python3 Tools/Verification/run.py tests --editor all --backend all --mode EditMode --suite editor
python3 Tools/Verification/run.py tests --editor all --backend all --mode PlayMode --suite generated-integration
python3 Tools/Verification/run.py check-generated --editor all --backend all
```

`codegen` uses independent processes for base compilation, routed generation, compilation/writeback and business field usage. It exercises two business asmdefs (one autoReferenced=false) plus Assembly-CSharp, an actual renamed Prefab Variant, mixed provider population, stale fingerprints and wrong-route rejection; the editor suite additionally checks duplicate/missing inputs. It repeats generation and compares actual asset/source bytes, then renames/moves a business asmdef and verifies obsolete owned sources are removed while business/user files survive. Reports retain commands, logs, actual versions, input fingerprints and file hashes under Artifacts/uikit. Existing fixture source drift fails instead of silently overwriting business files. `check-generated` requires read-only validation with an unchanged input fingerprint.

`generated-integration` uses the generated registry and actual Prefab leases for typed/untyped sessions, Single rejection, modal foreground restoration, CacheOne reuse, observer/service lifetime, canceled-load late cleanup, cancellation during Enter, synchronous Stop and Shutdown lease/lock cleanup. These are T3–T8 integration checks of generated mappings; the existing module suites remain unchanged. No T10 samples/performance, full release matrix, G0 refresh or mobile/IL2CPP build is implied. See [T9 acceptance](Reports/2026-11-01-t9.md) for actual results and scope.


## T10 imported samples and measured Editor baseline

The main package declares three Samples~ entries: QuickStart, Modals and Inventory. Their actual runtime assemblies are `Wulato.UIKit.Samples.QuickStart`, `.Modals` and `.Inventory`; generated Bindings/providers route by the original asmdef GUID. The sample package uses uGUI Text and an ordinary ScrollRect, with no TMP or optional input assembly references. The imported QuickStart Editor utility provides the two-step generation/usage-partial/scene wiring workflow documented in `Packages/com.wulato.uikit/Documentation~/QuickStart.md`.

```sh
python3 Tools/Verification/run.py prepare-samples --editor all --backend all
python3 Tools/Verification/run.py tests --editor all --backend all --mode PlayMode --suite samples
python3 Tools/Verification/run.py tests --editor all --backend all --mode PlayMode --suite performance
# Only compile and validate existing sample assets when generation inputs are already current:
python3 Tools/Verification/run.py prepare-samples --editor all --backend all --check-only
```

Preparation imports samples, compiles/confirms all three business assemblies, then mounts `com.wulato.uikit.acceptance.samples` and its testables. It generates/writes back with the existing T9 pipeline, installs the sample business usage partials and checks actual fonts, scenes and assembly routes. Consumer scripts with unexpected changes fail explicitly; existing Prefab/Scene content and asset GUIDs are preserved. The acceptance host, rather than the sample runtime, serializes the matching input driver into each test scene before scene startup. Unimported hosts do not mount the optional sample test package; suite-all expansion omits samples/performance suites when that package is absent.

`samples` has five required cases per host: Settings owner/button close, two Modal selections/restoration, actual typed Inventory text/cache refresh, fifty stable CacheOne cycles, and three serialized scene bootstraps with an external async confirmation substitute including pending-click suppression. `performance` has two required cases per host: three modes with fifty measured cycles each and actual five-stage ProfilerMarker samples. Resource checks use a counting wrapper around the real PrefabUIViewLoader and scene instance counts after delayed Destroy, not just active Session indices. Capacity zero and explicit ClearCache must leave no leases/instances; pending/isolated cleanup is zero.

The three timing modes are first load in a new Context, warmed Release reconstruction, and CacheOne reuse of the same Inventory Modal. Prefab assets are already loaded in the Editor registry: this does not measure cold disk loading. Each raw record includes editor/platform/Editor mode/backend/Basic diagnostics/fixed parameters, per-cycle wall times, whole-window GC frame allocation and available Canvas recorder samples. The allocation scope includes Editor/test harness work and differing frame counts; no fixed GC budget or per-command allocation attribution is claimed. Synchronous profiler scopes end before async waits; ForceUpdateCanvases has its own post-wait scope.

See [T10 acceptance and raw evidence](Reports/2026-11-01-t10.md) for exact module/regression counts and baseline tables. T10 does not run suite all, G0, the Reload matrix or any mobile/IL2CPP Player build; T11 remains outside this task.

## T11 release acceptance

The release gate requires the four current full Editor runs (EditMode + PlayMode), G0, all 16 reload configurations, four pinned Git consumer installations, eight ARM64/IL2CPP smoke combinations, and two additional clean Android/Input System builds. Environment blockers remain nonzero results. Current machine prerequisites and blockers are recorded in [phase1-environment.md](../docs/verification/phase1-environment.md).

```sh
python3 -m unittest discover -s Tools/Verification -p 'test_*.py'
python3 Tools/Verification/run.py prepare-samples --editor all --backend all --check-only
python3 Tools/Verification/run.py check-generated --editor all --backend all
python3 Tools/Verification/run.py compile-probe --editor all
python3 Tools/Verification/run.py tests --editor all --backend all --mode EditMode --suite all
python3 Tools/Verification/run.py tests --editor all --backend all --mode PlayMode --suite all
python3 Tools/Verification/run.py play-reset --editor all --backend all
python3 Tools/Verification/run.py gate --name preflight
python3 Tools/Verification/run.py build --editor all --backend all --target Android --profile smoke --prepare-only
python3 Tools/Verification/run.py build --editor all --backend all --target all --profile smoke
python3 Tools/Verification/run.py build --editor all --backend input --target Android --profile clean
python3 Tools/Verification/run.py smoke --build <exact-attempt-directory> --device <physical-device-id> --timeout 900
python3 Tools/Verification/run.py gate --name release
```

`--prepare-only` performs the pinned Git installation, sample authoring/generation, a separate compilation/writeback, scene wiring, and final compilation/validation without building a Player. Its `install_status` may be PASS while its build status remains BLOCKED and its exit code is nonzero. It never substitutes for a platform build. Repeat `smoke` for every one of the eight successful smoke builds. Android uses `UIKIT_ADB` (optional override) and the selected device; iOS uses `UIKIT_IOS_TEAM`, `xcodebuild` and `xcrun devicectl`. Signing secrets must stay in the executor's keychain/provisioning configuration. The Player waits for an actual Close interaction and writes `uikit-smoke.json` in its persistent data directory; neither `onClick.Invoke` nor `ExecuteEvents` injects that input.

Build IDs bind the source/spec fingerprint, source commit, editor/backend/platform/profile, fixed Git snapshot revision, dependency lock SHA-256 and attempt. The generated scene receives the ID only after dependency resolution. Every invocation uses its own clean consumer; no old project Library is copied. Mobile inventories live outside individual attempts, so deleting a newer attempt does not resurrect an older PASS. The release gate verifies indexed raw files, request identity, raw BuildReport data and raw Player report rather than accepting cached statuses. A successful iOS export alone does not satisfy compilation/install/launch. The clean profile mounts runtime packages and imported samples only, without TestAssemblies, verification packages or the smoke entry; the build entry rejects missing assembly evidence or any test/verification assembly in the actual Player assembly inventory.

The self-hosted workflows use dedicated `uikit-phase1` / `uikit-phase1-mobile` macOS labels. Configure both exact editor versions, matching mobile modules, SDKs, device variables and signing before enabling them. `release_ci.py` calls the existing runner sequentially; missing devices and manual touch steps do not become a green CI result. No workflow publishes, tags or merges a release. Platform execution remains unverified on a machine missing its toolchain; committed orchestration is not device acceptance.

本轮执行结果（包含阻塞矩阵与恢复要求）：[T11 验收记录](Reports/2026-11-01-t11.md)。

## 第二阶段：可选业务流程扩展

主包与 Input System 适配为 0.2.0；Navigation、Presentation、Dialogs、Popup Queue 各为独立 0.1.0 包，显式依赖核心 0.2.0。公共契约见 [Spec 第二阶段](../docs/superpowers/specs/2026-10-31-wulato-uikit-design.md)，任务及实际证据见 [第二阶段执行计划](../docs/superpowers/plans/2026-11-01-wulato-uikit-phase2-implementation.md)。

专项 `navigation`、`presentation`、`dialogs`、`popup-queue` 以 EditMode 验证调度、身份及所有权；`transitions` 和 `flow-integration` 在 PlayMode 验证实际 Shell、输入与退出。组合样例的 PlayMode 专项为 `flow-samples`，需要先 `prepare-samples` 导入全部四个扩展的 BusinessFlow。样例 Author 只补缺失资源，并根据消费宿主选择输入驱动；不覆盖其他业务场景内容。

```sh
python3 Tools/Verification/run.py tests --editor all --backend all --mode EditMode --suite navigation
python3 Tools/Verification/run.py tests --editor all --backend all --mode PlayMode --suite flow-integration
python3 Tools/Verification/run.py tests --editor all --backend all --mode PlayMode --suite flow-samples
```

PR 工作流根据变更路径选择直接相关 suite；只有显式 `full` 输入或移动发布门禁运行完整 Editor/Reload 矩阵。文档修改不触发 Unity。新增公共 API 使用正负编译探针，带参 View 的无参 Push/Open 必须编译失败。

发布阶段使用 `build --phase phase2` 与 `gate --name release --phase phase2`；默认 phase1 仍保留原七例。第二阶段移动报告必须在原七例上增加 NavigationPushBack、NavigationRollback、DialogTokenCancel、PopupPauseResume、PausedQueueShutdown、PresenterCacheReopen，合计十三例。phase 绑定请求、build-id、原始报告及汇总；旧阶段报告不能满足第二阶段要求。

`build --prepare-only` 只验证固定 Git 消费、生成、写回和编译，成功安装的 `install_status=PASS` 与预期非零退出/BLOCKED 不等于 Player 通过。仅允许原始证据完整、当前指纹一致且唯一阻塞为 no mobile Player built 的安装结果；其他错误不得忽略。

独立扩展依赖隔离使用已有 build 入口的安装限定范围：

```sh
python3 Tools/Verification/run.py build --phase phase2 --editor all --backend legacy --target Android --profile clean --package-set navigation --prepare-only
```

`--package-set` 可选 main/navigation/presentation/dialogs/popup-queue，限定范围仅用于 phase2 clean prepare-only；all 是默认完整组合。独立安装在两个指定编辑器的 legacy 消费环境验证，组合安装覆盖四宿主；Input System 兼容由四宿主专项和最终移动矩阵验证。各范围有独立登记目录，不能用单包安装记录代替完整组合。

P8 明确要求四宿主完整 EditMode/PlayMode、16 个 Reload 组合、四宿主固定 Git 组合安装、八个 ARM64 IL2CPP 实体设备组合，以及两个 Android/Input System clean 构建。iOS 必须 Xcode 编译/签名/安装/启动；TouchClose 需要用户真实触摸。模块专项通过不代表这些发布门禁已经完成。

## 第三阶段：Addressables 本地内容适配

独立包 `com.wulato.uikit.addressables` 0.1.0，核心及输入适配 0.3.0、四业务扩展 0.1.1。2022.3.62f2 固定 Addressables 1.21.21 / SBP 1.21.25，6000.3.22f1 固定 2.9.1 / 2.6.1；旧统一版本前置失败证据保留。

先 runtime prepare，再 codegen/prepare-samples；源输入稳定后执行 `prepare --phase addressables --editor all --backend all` 构建 StandaloneOSX 本地内容并封存证据。首次新增描述字段需要重新生成。内容证据校验实际依赖、Existing Build 类型、真实 Bundle、运行时源码/宿主资产指纹和内容哈希。修改内容输入后先重新准备；不能使用 AssetDatabase/Fast Mode 冒充。

新增 EditMode `addressables`，PlayMode `addressables-runtime`、`addressables-integration`；`addressables-preflight` 保留入口验证。包缺席时不纳入对应专项。真实组合覆盖缓存/LRU、导航、Presenter、Dialog、Queue、迟到清理、资源来源路由和 LocalInventory 场景。模块专项通过不代表移动发布。

固定 Git 接入只允许：`build --phase phase3 --editor all --backend legacy --target Android --profile clean --package-set main --prepare-only`，另运行 addressables 同组合及 all 的四宿主组合，共八项。target 仅沿用安装身份，不构建 Player。prepare-only 预期非零/BLOCKED 仅在 install_status=PASS、生成编译完成、当前指纹及原始证据正确且唯一阻塞为未构建 Player 时接受。校验实际安装版本、包来源、主包隔离及运行时程序集；不新增 phase3 release gate。

安装范围的运行时隔离读取 `GetAssemblies(Player)` 的实际引用图，从消费 Assets 源码与所选 UIKit 程序集计算完整闭包，同时检查可达预编译 DLL。候选列表中的未引用 `autoReferenced=false` SDK 测试程序集不等于 Player 依赖；保留候选、根、全部边、可达闭包和未引用项作为原始证据，任何可达 TestRunner/NUnit/验收程序集仍失败。此检查只证明编译依赖，不代替 Player 构建/裁剪验收。

预编译 DLL 的编译器候选（例如 SDK 自动引用的 NUnit）还须与实际 Editor 编译的 Runtime DLL 导入元数据求交；保留原始 DLL、副本哈希及导入清单。缺元数据或实际导入测试框架均失败。该证据明确限于 Editor 安装编译，不代替目标 Player 的条件编译、构建或裁剪验证。

## P0 Editor authoring workbench

`authoring` is an EditMode suite covering scene preflight/Undo/idempotence, Prefab ownership and binding validation, typed View source validation, persistent request cancellation/reload and sample directory/Usage conflicts. Existing `editor` and `generated-integration` suites retain generation fingerprint, routing and runtime regression coverage.

```sh
python3 Tools/Verification/run.py tests --editor all --backend all --mode EditMode --suite authoring
python3 Tools/Verification/run.py prepare-samples --editor all --backend all --quickstart-only
```

`--quickstart-only` creates fresh consumers under `Artifacts/uikit`, imports only QuickStart into the Package Manager versioned directory, and uses the workbench's operation service across real compiler/editor restarts. Two complete configuration passes must leave identical input fingerprints. It explicitly selects the host driver, checks absence of Modals/Inventory assemblies, runs `quickstart-only` PlayMode through the existing registered test runner, and revalidates raw XML/traces/inventory/hashes/current snapshots. Preparation logs, command records, source fingerprints and summaries are retained. This is an Editor installation/authoring acceptance path, not a Player build or release gate. Do not combine this mode with `--red` or `--check-only`; standard sample preparation and its canonical paths remain compatible.
