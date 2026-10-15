# Presentation and run direction

The runner is a round cyclops related to the enemy silhouette. A pearl crown,
small swept fins, luminous eye ring and warm lower band distinguish the player.
The original Godot-native model lives at `art/runner_model.tscn`, inside the
stable `art/player_visual.tscn` adapter. Cosmetic colors tint the body; a subtle
Fresnel shader adds a pearl edge. `RunnerVisualMotion` animates fins and feet.

The interface uses open space, thin dividers and small orange focus accents.
INFO and BUILD are icon-only controls next to the run menu, with a tiny fusion
availability dot. Detailed equipment stays in the build inspector. The manual
and build inspector share safe margins, scrolling, focus restoration and pause
ownership. All interface copy is English. Button icon sizes use Godot theme
constants. Settings use the same optical glass backdrop as pause and field panels, with
fully transparent card surfaces. `SettingsTabAccent` paints a warm gradient in
the native TabBar coordinate space. Initial layout, reopens and resizing snap
to the selected native tab; tab changes interpolate locally, clipped by the bar.
`MenuVisualStyle` owns the transparent action treatment for all pause actions,
including INFO and BUILD. The small HUD icons keep their compact presentation.

UI references, used for principles rather than copied assets:
- [Returnal UI/UX, Housemarque](https://blog.playstation.com/2021/05/11/unpacking-returnals-ux-design-gameplay-first-ui-retro-futuristic-tech-and-accessibility/): gameplay first, industrial surfaces, information hierarchy and input-aware guidance.
- [Hades updates, Supergiant](https://www.supergiantgames.com/blog/hades-updates/): distinguish build-defining choices and combinations from small numeric modifiers.

## One equipment loop

Clear arena → install a device and receive its catalyst → choose a route.
Repeated installations strengthen the device and supply another catalyst.
Two catalyst ranks + the installed device → a unique evolution in BUILD.
There is no separate stat-boost selection and no direct evolution reward draft.
Catalyst passives support the run until they are spent on an evolution.

## Biome chapters

A biome persists across travel sections and its arena. The next chapter advances
to the next biome, replacing the former per-section random color changes. The
first four chapters lead to the existing finale; continuing reaches the remaining
biomes and cycles the set in endless play.

| Biome | Environment | Weather |
| --- | --- | --- |
| Dustbound Ruins | Broken columns, inscriptions, rubble | Dust and crosswind |
| Verdant Vault | Trunks, low-poly canopy, roots, luminous growth | Rain and spores |
| Frostfall Ridge | Rock shelves and ice spires | Drifting snow |
| Rainline District | Towers, window bands, facade ribs, neon signs | Rain and cool haze |
| Cinder Works | Exhaust stacks, hot vents, pipes, basalt | Rising ash |
| Skyglass Isles | Suspended islands and glass arches | Mist and wind |

`RunBiomeCatalog` supplies profiles. `RunBiomeScenery` creates deterministic,
collision-free decoration outside playable bounds using a local RNG, so visual
variation cannot perturb encounters. `WorldSectionRuntime.BiomeId` drives
`RunVisualAtmosphere` and its bounded `RunBiomeWeather` field. Weather honors
effects quality. Scenery streams with its parent section and has a visibility
range rather than accumulating indefinitely.

All new model and scenery assets are original project-native geometry. No
third-party art packs or commercial-game assets were downloaded or copied.

Verification scenes include `settings_layout_probe`, `biome_presentation_probe`,
`buff_interface_probe`, `run_progression_probe`, `run_merge_probe`,
`player_presentation_probe`, and the existing arena and model contracts. Rendered
probes save review images under `/tmp`; they are not production scene content.

## Gentle traversal

Water relays alternate with two 60-metre courses built by
`GentleTraversalCourse`: a continuous 8-metre-wide S walk, and broad landing
islands with gaps under 2 metres plus a continuous 5.8-metre bypass. A solid
shallow bed 1.1 metres below the road catches mistakes. Reeds and edge inlays
have no collision. These courses replace the dense wall junction in the live
legacy rotation; the old construction remains available in the content library.
`traversal_water.gdshader` supplies low-amplitude waves and soft warped caustics
for the pond and new courses. `gentle_traversal_probe` validates the paths with
physics rays and saves renders of both variants.

## Pacing and helpful constructions

`WorldGenerationSettings.MinimumBiomeTravelDistance` is a physical distance,
600 metres in the production resource. Both procedural arena scheduling and
the authored hybrid slice obey it. The hybrid prelude measures authored socket
distances and fills the missing approach with normal travel before entering
the contiguous authored recipe. An exit connector counts toward the following
biome. `biome_pacing_probe` covers eight arenas, four seeds and both pipelines.

`RunSupportStructures` adds optional side routes between arena sections:
- **Slipstream Gallery:** open, collision-free arches frame a broad tailwind
  lane. Real rail zones sustain speed while leaving vertical motion and strong
  lateral steering available.
- **Bloom Launch:** broad kinetic flowers translate a landing into forward
  momentum. The center road stays clear; jumping onto them is optional.
- **Ribbon Bridge:** continuous gentle ramps lead to a low upper deck. Players
  may stay on the ground or take the speed lane and exit charge.

The island promenade also offers a kinetic bloom on its first island. Its
continuous bypass and shallow recovery bed remain available. Moving floor
inlays show the beneficial direction without adding text or danger warnings.

## Enemy recognition

`EnemyVisualIdentity` selects nine separate native scenes under `art/enemies`.
They retain the original species language (floating rounded body, a large eye,
bright role colors), while giving each role its own anatomy, face and outline.
Soft cheeks, bellies and large eyes provide the cute half; narrow pupils,
asymmetric horns, small fangs and predatory fins provide the unsettling half.
The twin is a joined pair of cyclops creatures with contrasting expressions.
`EnemyCreatureMotion` adds blinks, asymmetric twin blinks and quiet appendage
motion without touching the gameplay wrapper. The original GLB remains in the
project as a reference asset. Physics, spawn roles and hitboxes are unchanged.
`ModelVisualRoot.ApplySurfacePalette` colors each semantic material separately;
`ReplaceModel` swaps only the child named Model and resets material bindings.

## Refuge interface state

The authored toolbar now includes INFO beside the eye and settings, sharing
style, row height and touch target size. INFO opens refuge-specific navigation,
customization and hide/show instructions; the run guide remains separate.

One scalar in Refuge owns HUD opacity, visibility and input eligibility. Rapid
reversals continue from the current opacity, with no competing per-control
callbacks. H / controller Y hides all overlays and world labels; the restore
eye stays in place. Escape / controller B also restores the hidden interface.
Hidden controls cannot change selections. Toolbar touches never start a scene
swipe or mannequin drag. `refuge_interface_probe` covers rapid toggles, layout
refresh, hidden navigation, world labels, INFO, and a portrait touch layout.

Additional principles and references:
- [Valve: Illustrative Rendering in Team Fortress 2](https://cdn.cloudflare.steamstatic.com/apps/valve/2007/NPAR07_IllustrativeRenderingInTeamFortress2_Slides.pdf): distinguish roles through silhouette, shape and shading.
- [Epic: Movement Modulator devices](https://dev.epicgames.com/documentation/fortnite/using-movement-modulator-devices-in-fortnite-creative?lang=en-US): environmental devices can grant speed and impulses, making movement itself a reward.

These references inform the design; the geometry, shaders and interface code
are original project assets. No third-party game art was copied.

## Final guardians

The fourth hold now culminates in one of three two-phase guardians. Each uses
existing jump, dodge, Rift and stomp skills, with a different rule for opening
the crown: jump rings, bait charges, or break crystal seals. See
[boss-encounters.md](boss-encounters.md) for encounter ownership and validation.
Regular enemy models are 45% larger to read clearly against the player.

## Dynamic combat iteration

Guardians now reposition and select patterns from current runner observations.
Devices add an equipped cast or an action-triggered world effect. Convex arena
footprints are shared by geometry, camera and AI, with optional bloom pads and
resonators. See [dynamic-combat.md](dynamic-combat.md) for rules and references.
