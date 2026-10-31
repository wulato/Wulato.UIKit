# Changelog

## 0.1.0 — development checkpoints

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

T4 remains a development checkpoint. CacheOne/LRU, independent late-cleanup hosting and bounded Shutdown (T5), production transitions/input, Builder and mobile acceptance remain later tasks.
