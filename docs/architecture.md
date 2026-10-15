# No Way Down architecture

## Ownership rules

- Scenes own node lifetime, hierarchy, static layout, text defaults, and reusable resources.
- C# components own gameplay state, event binding, algorithms, and runtime-only effects.
- `Main` coordinates the run. It does not construct persistent services or authored UI.
- Run topology, selection and placement remain code-generated because they depend on the seed and route. Reusable geometry is progressively moving to authored PackedScene chunks.
- Temporary enemies and generated world sections may be instantiated at runtime and must be released by their owning director.

## Run scene

`main.tscn` is divided into explicit composition roots:

- `RunSystems`: procedural world, encounter director, atmosphere, pressure front, combat feedback, trajectory preview, HUD director, and ground vitals.
- `UserInterface/RunOverlays`: pause menu, arena reward overlay, and run progress HUD.
- `UserInterface`: authored HUD widgets, projection lines, display settings, and the instanced `RunEndOverlay` scene.

`Main._Ready()` resolves these nodes and injects runtime dependencies. It must not replace a scene-owned node with a newly allocated equivalent.

Static component scenes currently include:

- `ui/full_settings.tscn` for the shared settings surface;
- `ui/run_progress_hud.tscn` and `ui/run_end_overlay.tscn` for run UI;
- `world/ground_vitals_indicator.tscn` and `world/player_jump_trajectory.tscn` for authored 3D presentation.

## Player boundary

`Player` is the public gameplay facade and the sole owner of `Velocity`, `MoveAndSlide()`, health effects, signals, input and scene queries. `PlayerPresentation`, attached to `player.tscn/Pivot`, owns pose, animation speed, skin nodes, damage blinking, trail, and glow. Presentation code must never mutate gameplay state.

Input-independent player state is split into scene-free components: `PlayerAbilityState` owns Rift/Nitro timers and transitions, `PlayerComboState` owns combo counters and one-shot rewards, `EnemyContactTracker` owns contact confirmation and reaction cooldowns, and `PlayerMovementMotor` contains pure planar/gravity calculations. These components cannot move the character, emit gameplay signals or query the scene tree; `Player` applies their results in a single physics order.

`PlayerRunBuild` owns run-local upgrade ranks and deterministic derived modifiers. Persistent accessibility and customization settings remain outside it. Reapplying the same upgrade rank must be idempotent.

Player and enemy art is isolated behind `ModelVisualRoot` wrapper scenes. Gameplay scenes depend on the stable `Pivot/Character` wrapper. The native or imported payload stays below a child named `Model`; `EnemyVisualIdentity` selects the regular creature catalog. Model-local transforms may change without moving gameplay collisions.

## Initialization order

Scene children enter the tree before `Main._Ready()`. Components that require run-specific dependencies expose an idempotent `Initialize(...)` method. Their `_Ready()` methods may prepare local, dependency-free resources only.

`ProceduralWorld.Initialize(...)` is the boundary that starts generation. Before generation it snapshots the assigned `WorldGenerationSettings` resource, injects the player and run start position, and prepares the selected pipeline. An active run therefore cannot change when the shared authored resource is edited later.

## Encounter boundary

`EncounterPlanner` is a scene-free deterministic policy object. It owns the wave and lane decks, selects travel patterns and arena styles, and produces immutable `ArenaWavePlan` values from an explicit seed. Changing RNG-sensitive rules requires incrementing its planning version and updating the pinned fingerprint probe.

`EncounterDirector`, authored under `main.tscn/RunSystems`, owns the encounter timers, physical enemy spawn, arena pulse lifecycle, and wave completion. Its tunable values come from the typed `EncounterSettings` resource, and its run dependencies are supplied once through an idempotent `Initialize(...)` call.

`Main` remains the coordinator for run state, arena transitions, rewards, music, camera, and pressure. It reacts to typed director events but does not choose wave policy or instantiate mobs.

The fourth arena delegates to `BossEncounter`, with three definitions in `BossCatalog`. `BossBrain` chooses position and patterns from current observations; `BossAttacks` executes bounded schedules. `ArenaLayout` is the shared footprint for floor, camera, AI, enemy placement and containment. `BossMob` adapts weak points to existing stomp and Rift mechanics; `BossHazard` owns attack geometry and damage. Main still owns arena locks, music, camera framing and the final reward decision. `ui/boss_hud.tscn` authors the compact guardian rail, bound by `BossHud`. See [boss-encounters.md](boss-encounters.md).

## Authored world-content boundary

Every reusable location scene has a `WorldChunk` root with stable `Entry`, `Exit`, `PlayableBounds`, `ObstacleSlots`, `EnemySpawnAreas`, camera metadata and pressure metadata paths. Models and simplified authored collisions live below the replaceable geometry root; the assembler must not inspect model-internal node names or derive gameplay collision from visual bounds.

`WorldChunkDefinition` and `WorldObstacleDefinition` are immutable catalog metadata. They hold PackedScenes, weights, compatibility tags, socket profiles, theme, width, chapter and difficulty gates, but never active-run state. `WorldContentCatalog` groups these definitions and validates unique IDs. The complete authoring rules are documented in [world-content-contract.md](world-content-contract.md).

The production scene uses `HybridV1`. `HybridWorldPlanner` selects the compatible travel → connector → arena → exit recipe without creating nodes. `HybridWorldAssembler` instantiates its scenes, aligns public socket transforms and fills compatible obstacle slots. `WorldSectionRuntime` contains per-run mutable state, while `WorldSectionStream` owns lookup and retain-behind release for authored and legacy sections alike.

The migration boundary is intentionally narrow: chapter one is legacy, chapter two is the authored reference slice, and later sections continue through the legacy builder. A failed catalog plan falls back before the slice starts rather than partially replacing arbitrary geometry. The full policy and extension points are documented in [hybrid-world-generation.md](hybrid-world-generation.md).

## UI boundary

Authored controls live in the scene. UI scripts bind controls by stable paths, connect signals, apply state-dependent styles, and animate transitions. They must not recreate their complete control tree in `_Ready()` or `Initialize(...)`.

`RunSkillRuntime` owns one equipped device cast and passive device rules. Movement and spending are requested through `PlayerSkillActions`; modifier math stays in `PlayerRunBuild`. The authored `ui/run_skill_hud.tscn` owns the cast affordance. Friendly supports and guardian weak points use explicit Mob target capabilities rather than pretending every jump target deals damage. Details and references: [dynamic-combat.md](dynamic-combat.md).

## Resource policy

Immutable materials and shaders may be shared. `ProceduralWorld` caches repeated surface, structure, obstacle, and water materials. A cached resource must never be mutated for one instance; per-instance animation belongs on instance properties or a duplicated material.

Mutable scene resources must use `resource_local_to_scene = true` or be duplicated once during binding. This applies to player skin materials and the trail process material. Probes must instantiate at least two owners and verify that changing one resource does not affect the other.

Stable top-level world dimensions and streaming distances live in `world/default_world_generation_settings.tres`. Hybrid selection and obstacle placement use an explicit local seed and versioned fingerprints; legacy section recipes retain their existing random path until their category is migrated.

## Verification boundary

Every extraction needs a clean C# build and a focused MCP probe. Changes to a composition root also require its production transition or visual review. Scene probes should assert authored paths, instance-local mutable resources, idempotent repeated calls, and the absence of replaced runtime node trees.

## Future extraction order

The six planned optimization passes, their dependencies and completion criteria are tracked in [future-improvements.md](future-improvements.md). Each extraction requires a clean build and an MCP playtest before the next boundary is changed.
