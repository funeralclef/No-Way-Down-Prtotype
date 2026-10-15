# Future improvements

## Target architecture

The world should use a hybrid architecture:

- Code owns seeds, section order, difficulty, weights, variants, assembly and streaming.
- Authored `PackedScene` assets own roads, arenas, buildings, obstacles, collisions and visual sockets.
- Typed `Resource` assets describe content metadata, compatibility, frequency and constraints.
- `ProceduralWorld` orchestrates components without knowing internal model paths.

Migrate incrementally. Remove a legacy procedural builder only after its category has a scene-based replacement that passes deterministic and visual checks.

## Pass 1: authored refuge interface

- [x] Create `ui/refuge_interface.tscn` with a static hierarchy, layout and styles.
- [x] Keep station state, input, 3D-to-UI projection, transitions and data updates in `Refuge`.
- [x] Remove confirmed unused labels, backdrops, scanlines and old UI factories.
- [x] Preserve paths and reflection contracts used by existing visual and integration probes until their planned replacement.
- [x] Add `refuge_interface_probe.tscn` for structure, navigation, settings and collapse/restore behavior.

Complete when `Refuge` no longer builds a permanent Control tree in C#, and `refuge_visual_review` and `refuge_transition_probe` pass without visual regressions.

Status: completed on August 10, 2026. Contract, visual and transition probes pass; `Refuge` no longer creates permanent `Control` nodes.

## Pass 2: EncounterDirector and wave policy

- [x] Extract encounter selection, wave decks, spawn lanes, arena pulses and wave completion from `Main`.
- [x] Supply dependencies through an idempotent `Initialize(...)` or typed configuration Resource.
- [x] Keep `Main` as the coordinator of run state, transitions and major system events.
- [x] Separate pure wave planning from spawning so planning can be tested without a scene.
- [x] Record a seed and fingerprint for arena and wave sequences.

Complete when wave rules can be tested independently and `arena_reward_integration`, `run_finale_integration` and production runs preserve their behavior.

Status: completed on August 10, 2026. `EncounterPlanner` records a version and deterministic plan fingerprint. `EncounterDirector` owns timers and spawn lifecycles. Focused, reward, finale and production checks pass.

## Pass 3: player abilities and contact components

- [x] Separate movement, Rift/Nitro, stomp/combo and enemy-contact resolution into small state components.
- [x] Keep `CharacterBody3D.MoveAndSlide()` and ownership of `Velocity` in `Player`: one node owns physics.
- [x] Use value state and pure calculations where a component does not need a Node lifecycle.
- [x] Preserve the public `Player` facade for the HUD, settings, customization and existing scenes.
- [x] Add probes for ability ordering, repeated contacts and duplicate side effects.

Complete when core abilities can be tested independently and Nitro, traversal and combat probes pass without changing game feel.

Status: completed on August 10, 2026. `PlayerAbilityState`, `PlayerComboState`, `EnemyContactTracker` and `PlayerMovementMotor` do not depend on Node lifecycles. `Player` remains the sole owner of `Velocity` and `MoveAndSlide()`. Component, combat-contact, Nitro/Rift and traversal probes pass.

## Pass 4: hybrid world content contract

- [x] Introduce typed definitions for location chunks, arenas and obstacles.
- [x] Define a shared authored-section root, such as `WorldChunk`, without depending on internal mesh names.
- [x] Specify required sockets: `Entry`, `Exit`, playable bounds, obstacle slots, enemy spawn areas and camera/pressure metadata.
- [x] Document compatibility tags: section type, theme, width, entry/exit, minimum chapter, difficulty and permitted neighbors.
- [x] Store the `PackedScene`, selection weight and constraints in definitions, keeping run state separate.
- [x] Create one reference scene each for travel chunks, connectors, arenas and obstacles.
- [x] Add a contract probe for sockets, bounds, collisions, prohibited gameplay scripts and local mutable resources.

Complete when new content can be added with one scene and Resource entry without changing `ProceduralWorld.cs`.

Status: completed on August 10, 2026. The reference catalog, authored reference scenes, contract probe and visual seam review pass. Production integration follows in the next pass.

## Pass 5: hybrid assembly and incremental ProceduralWorld migration

- [x] Separate section selection, compatibility checks, placement, decoration and streaming lifetimes.
- [x] Preserve RNG ordering or introduce an explicit generator version and deterministic fingerprint.
- [x] Migrate one vertical slice first: travel -> connector -> arena -> exit.
- [x] Join authored chunks through sockets. Generate transitions and variations procedurally where needed.
- [x] Populate obstacle slots from scenes using seeds, difficulty, footprints and gameplay tags.
- [x] Preserve generation-ahead/retain-behind streaming and world-owned section cleanup.
- [x] Check seams, intersections, route width, spawn reachability and reproducibility for equal seeds.

Complete when a production run can traverse a mixed authored/procedural chapter and both pipelines can be compared with one integration probe.

Status: completed on August 10, 2026. `HybridWorldPlanner` selects a versioned recipe with an explicit seed. `HybridWorldAssembler` joins scenes through sockets and fills typed slots. `WorldSectionStream` owns lookup and retain-behind cleanup. Production keeps the first legacy chapter, uses an authored slice in the second, then returns to the legacy continuation. The integration probe compares both pipelines and checks fingerprints, seams, ranges, width, spawn markers, obstacles and streaming lifetimes.

## Pass 6: complete model library and final optimization

- [ ] Replace roads, arenas, buildings and obstacles with final or representative authored scenes.
- [ ] Tune LOD, shadow policy, material sharing, collision complexity and visibility ranges against measured budgets.
- [ ] Check forward direction, scale, ground contact, effect sockets and obstacle readability.
- [ ] Tune Player/Mob wrappers, animations, skin attachments, trails/glow and refuge framing for final models.
- [ ] Add pooling only for frequently created objects identified by profiling.
- [ ] Remove remaining primitive builders and old assets after category parity probes pass.
- [ ] Record CPU frame time, GPU frame time, draw calls, object count and memory baselines for a representative run.

Complete when replaceable geometry is supplied by scenes and catalogs, runtime code no longer builds permanent models, and measured performance meets the approved baseline.

## Order and dependencies

- Passes 1-3 are independent of final models and can precede the asset library.
- Complete pass 4 before producing locations at scale: it defines the art and level-design contract.
- Start pass 5 with a small vertical slice.
- Begin pass 6 once representative models and performance measurements are available.

## Avoid premature changes

- Do not move RNG-sensitive parameters without legacy/new fingerprints.
- Do not derive gameplay colliders automatically from visual model AABBs.
- Do not add internal GLB node paths to `ProceduralWorld`.
- Keep primitive fallbacks until verified scene replacements are available.
- Build a library of small compatible chunks instead of one huge level scene.
- Add pooling, multithreading or complex caches only after profiling identifies a bottleneck.
