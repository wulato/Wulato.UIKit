# Changelog

## Unreleased — 0.5 tasks 1.1–4.1

- Consolidate the seven built-in Runtime assemblies into `Wulato.UIKit`; keep View/Session behavior, namespaces and script GUIDs. Preserve the former Core asmdef GUID on the new root definition.
- Make first-party transaction protocols and input bookkeeping components internal; keep navigation/transition and input-driver extension contracts public.
- Migrate consumers, samples, codegen/compiler probes and verification boundaries. Addressables/Input System adapters now ship in SDK-gated Runtime/Editor/Tests assemblies inside the main package; LocalInventory is a main-package sample. Remove retired adapter package references, retain optional Unity SDKs. Default resource routing now follows Registry sources with per-context backend snapshots; optional Addressables auto-registers and missing backends fail before Context allocation. Explicit WithLoader remains available.
- Make the facade the sole production IUIContext; expose host.UI and ContextId, validate Context-owned CloseAsync. Compose navigation/dialogs/popup queue before user factories; keep Presenter optional. Register dialog requests with their View through AddDialog and regenerate providers.
- Route Page Open through the same Navigator FIFO as Push/Replace/PopTo. Publish new-root/replacement history atomically; retain precommit rollback and postcommit bounded cleanup. Other layers remain ordinary opens.
- Unify Back/Cancel in the existing Context FIFO: non-page input owner first, CanClose veto without fallthrough, page restoration and root no-op. Bind input requests to their original owner; preserve fresh-edge gating, bounded closure, cancellation and Shutdown.
- Add DialogAsync and typed PopupAsync on IUIContext, backed by the existing default services and unified Registry. Give popup items independent cancellation with rollback/Closed and deduplication barriers; marshal cancellation callbacks to the main thread. Popup scheduling rejects Page registrations.
- Add SDK-independent Host preset factories and an opt-in DOTween 1.3.045 adapter/sample with Fade/Scale/Slide and paired sequences. Remove UITransitionRunner; retain the custom transition contracts and SDK-free Immediate default.
- Add an optional R3 1.3.1 Counter sample using native uGUI helpers and Session.Track; keep business model ownership external and guard queued notifications after main-thread dispatch. No core R3 dependency or binding API.
- Add an optional FancyScrollView 1.9.0 fixed-size catalog sample with native pooling/Scroller, per-cell cancellation and session/ticket guards. Document direct control integration boundaries.
- Extend Workbench with Page/Modal/Dialog and optional Presenter templates, guarded source selection, explicit input selection and atomic scene/Host initialization with existing presets. Preserve existing policies, user partials and assembly routes.
- See Documentation~/Migration-0.5.md. Package versions are unchanged at this development checkpoint; this is not a 0.5 release.

## 0.4.0

Integrated Navigation, Presentation, Dialogs and PopupQueue without renaming assemblies or types. Added optional UIContextHost composition, Workbench host installation and isolated PlayMode preview with explicit adapters and scene recovery. Optional SDK adapters remain separate; see Documentation~/Migration-0.4.md.

## 0.3.0

- Add generic External asset descriptors and explicit marker/codegen support without optional backend dependencies.

## 0.2.0

- Add opt-in business flow composition and Context stop capability; preserve existing View/Session and transaction entry points.
- Declare the 0.2.0 core baseline for optional extensions.


## 0.1.0 — development checkpoints

### T10 — samples, profiling and integration documentation (2026-11-01)

- Ship QuickStart settings, stacked Modals and typed Inventory samples with uGUI Text, ordinary ScrollRect, explicit Context composition and routed generated bindings.
- Import sample assemblies before mounting the optional sample acceptance package; preserve consumer scenes and asset GUIDs.
- Record 50 first-context loads, warm Release rebuilds and CacheOne reopens for a representative Modal, including frame allocations, available Canvas profiler data and independent live resource counts.
- Add synchronous load/layout/hook/transition/cache profiler scopes and installation, lifecycle, cancellation/error, extension and performance guides.
- T9 generator, T8 composition and T7 input are implemented checkpoints. Whole-phase mobile/IL2CPP release acceptance remains T11.

### T5 — caching and bounded cleanup (2026-11-01)

- Add healthy CacheOne instance reuse with a hard-capacity LRU, explicit clearing and independent Session identities.
- Bound load/open/layout/close/transition waits; Shutdown starts one absolute deadline outside the command queue and never resets it per Session.
- Transfer abandoned operation records and isolated leases to an independent cleanup owner, preserving single consumption, origin and cleanup errors.
- Add a persistent runtime notification/quarantine host, root-loss evacuation and a real-time UniTask scheduler; root loss, caller cancellation and timeouts retain distinct outcomes.
- Freeze final stopped diagnostics and retain shared Closed/Shutdown completion results; reject queued opens on stop.
- Release Context-owned runtime roots after final notifications/late owners drain while preserving project Canvas and external services; detach ended scope/notification closures from Context ownership.

### T4 — actual hosting and loading (2026-11-01)

- Add direct Prefab and explicit Resources loading with idempotent clone leases and partial-load cleanup.
- Add UIRoot Overlay/Camera layers and optional authored SafeArea mounting, inactive staging/cache parents, and Shell/AnimationRoot wrappers that preserve authored View geometry.
- Detach loader-owned Views before lease disposal; destroy empty wrappers afterward.
- Add a one-Update uGUI layout barrier, hidden-state raycast gating, and dynamic configuration guards; wrappers ignore parent LayoutGroups.
- Verify actual initialization order, layout rebuilding, special pivots, Camera/SafeArea geometry, relocation after parent resizing, and dynamic gate failures on both specified Unity editors and both verification backends.

### T3 — logical sessions

- Add single-consumer operation records, stable Closed/Shutdown results, FIFO commands, and scoped session ownership.
- Reject type-erased argument mismatches before allocation; preserve lifecycle rollback, cancellation, reverse observer cleanup, and synchronous continuation isolation.

### T2 — contract baseline

- Define public Context, Session, View, loader, scheduler, transition and input protocols.
- Add validated settings and immutable completion, registry and diagnostic data.
- Establish Core, Loading and UGUI assembly boundaries and contract tests.

The package remains a development checkpoint until the T11 clean-install and complete mobile/IL2CPP release gate is satisfied.
