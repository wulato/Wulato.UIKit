# UIKit verification

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

Limits: these are Editor preflight tests, not mobile/IL2CPP release acceptance or production UIKit tests. Input System Submit/Cancel uses keyboard keys; navigation uses the gamepad stick. Physical gamepad Submit/Cancel buttons are not claimed as verified. T2 has separate contract acceptance below; T3 logical session execution is verified below; T4 actual hosting/loading/layout is verified below; T5 cache/bounded cleanup is verified below; T6 stoppable transitions and the transaction boundary are verified below; T7 production input arbitration and both drivers are verified below; T8 composition is covered by the integration and independent play-reset checks below; later tasks remain unimplemented.

Verification prepare/tests batches use a nonblocking process-wide filesystem lock for the artifact root. Overlapping CLI batches fail explicitly instead of interleaving host execution; G0 uses a shared guard and returns FAIL while a batch is active. G0 schema 3 requires the serialized-batch inventory/evidence binding. Historical schema-2 records remain readable but cannot satisfy the current evidence verifier. Unknown inventories require restoring valid provenance, not silently skipping them.

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

Runtime preparation now adds the actual `com.wulato.uikit.input-system` package only to input hosts. Its metadata pins the main package to 0.1.0 and Input System to 1.17.0. The two `input-package` cases per input host read the actual package manifest/asmdef and resolved versions. Adapter sources participate in canonical-path and fingerprint checks. No automatic optional-package discovery is introduced into Core.

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
