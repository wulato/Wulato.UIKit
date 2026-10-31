# UIKit P0/P1 verification

These packages are verification prototypes. They do not depend on, or create, the production UIKit packages. Run commands from the repository root on macOS with the two specified Unity editors installed.

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

On revocation, the node snapshot sends pointerUp/endDrag without click/drop, selection is cleared, and the module resets via public DeactivateModule / enabled lifecycle. The legacy module's DeactivateModule clears protected pointer data. For Input System, disabling/re-enabling invokes its own pointer-reset lifecycle. InputGateNode snapshots callback data because Input System reuses PointerEventData between mouse buttons. InputEventSpy only records delivered events. Within a Process call, revocation also clears the public in-flight event pointerClick/press/drag eligibility immediately; module reset is deferred until Process exits. The live references are discarded at that boundary. Initial ownership is applied explicitly before processing any input. Processing resumes only after relevant UI controls are neutral, followed by a fresh input edge. Input System release checks enumerate the UI actions’ resolved controls; unrelated held gameplay keys do not quarantine the UI.

Each attempt registers run.json before launch, then writes XML, logs, per-case traces, host snapshots, command.json and summary.json under ignored Artifacts/uikit. evidence-index.json seals the raw files with SHA-256 and its own digest is recorded in the summary. G0 chooses the latest registered attempt by start time/run ID, never falling back from an incomplete attempt to an older green summary. It checks the index, re-parses raw XML/logs, compares summary cases and snapshot/configuration, and requires every fixture-qualified case and its trace. Current fingerprints cover all host Assets/ProjectSettings/Packages, verifier package contents and runner/matrix sources. Canonical local package sources and lock entries are checked; a same-named alternate source is rejected. Changes during a run invalidate its result. Missing or changed raw evidence, skipped/inconclusive/failed cases or parent suites, compiler errors, timeout and nonzero process exit all fail closed.

Limits: these are Editor preflight tests, not mobile/IL2CPP release acceptance or production UIKit tests. Input System Submit/Cancel uses keyboard keys; navigation uses the gamepad stick. Physical gamepad Submit/Cancel buttons are not claimed as verified. T2 and later tasks require separate authorization.
