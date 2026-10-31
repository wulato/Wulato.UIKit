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

Limits: these are Editor preflight tests, not mobile/IL2CPP release acceptance or production UIKit tests. Input System Submit/Cancel uses keyboard keys; navigation uses the gamepad stick. Physical gamepad Submit/Cancel buttons are not claimed as verified. T2 has separate contract acceptance below; T3 and later tasks remain unimplemented.

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
