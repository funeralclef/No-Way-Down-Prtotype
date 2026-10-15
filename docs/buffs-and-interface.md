# Run buffs and interface

Buff ranks belong to one run. Cleared arenas offer three devices, never a
separate passive-stat draft. Installing or servicing a device also grants one
rank of its matching catalyst. Devices have two ranks; catalysts have three.
At the device cap, servicing it grants its catalyst without increasing rank.
Evolutions have one rank and are acquired only through merging.
`PlayerRunBuild` rebuilds passive catalyst modifiers from ranks; device ranks
strengthen gameplay hooks on `Player`.

`RunProgression` owns drafts and merge transactions. `RunBuffCatalog` owns the
shared recipe list, icons, tier colors and effect text. Presentation reads these
models and does not calculate or mutate combat modifiers.

## Merge rules

Between reward drafts, an installed device and two ranks of its catalyst can
produce an evolution. Two catalyst ranks are consumed. The device remains
installed because it supplies the underlying behavior of the evolution.
An evolution is unique; a duplicate request does not spend anything. Evolution acquisition uses the merge system exclusively.

| Device | Catalyst (two ranks) | Evolution |
| --- | --- | --- |
| Afterburner | Cool Burn | Blue Comet |
| Red Thread | Long Step | Chain Reaction |
| Shock Sole | Steady Pulse | Earth Splitter |
| Glass Heart | Second Wind | Last Light |
| Hunter Lens | Airfoil | Two Horizons |
| Overpass | Quick Spark | Phase Route |
| Kinetic Reserve | Close Call | Perfect Circuit |
| Closed Loop | Finisher Cell | Infinite Line |

After a successful merge, `Main.SynchronizeRunBuild` applies all resulting ranks
to Player, including removed catalysts. Capacity reductions clamp live resources;
repeated synchronization does not grant another pickup bonus.

## Views

- `ui/arena_reward_overlay.tscn` is the reusable authored reward view. It shows
  rank previews, effects, true tag affinity, icons and recipe hints. A scroll
  container preserves access to full cards on narrow windows or larger text.
- `RunBuffHud` shows only two compact icons in the top row and short activation notices.
  A small dot on BUILD indicates that a merge is available; the inspector holds the details.
- `RunBuildPanel` displays installed buffs and recipes, placing ready recipes
  first. The refuge LOADOUT station uses its archive mode to preview all buffs.
- `GameInfoPanel` separates a compact input-aware control page from run-system
  guidance. Keyboard and controller bindings are shown on separate pages.
- `GameMenuPanel` centralizes scrolling, safe insets, text scaling, pause ownership
  and focus restoration. A panel opened from pause leaves the run paused.
- `MenuVisualStyle` shares transparent actions and a glass backdrop between
  pause, settings, INFO and BUILD. The small HUD actions remain icon-only.
- `SettingsTabAccent` draws the gradient in native tab coordinates, clipping to
  the bar and snapping on first layout, reopens and resizing.
- `RunBuffEffects` renders a bounded halo and pulses on Player's
  `RunBuffTriggered` signal. Gameplay decides when an effect actually triggers.
  Visuals honor Player's effects-quality setting and do not alter gameplay.

The refuge palette covers architectural materials, glass, the window, route
lines, portal, soft lights, key light, ambient light and fog. Previewing a style
changes the whole room; applying it uses the existing customization profile.

During gameplay, MENU, INFO, BUILD and CAST use `FocusMode.None`: pointer and
touch activation remain available, but WASD, arrows and controller navigation
cannot move GUI focus onto them. This also prevents Space / A (jump and
`ui_accept`) from opening a menu after casting. Keyboard and controller users
open pause with Escape / Start and navigate its INFO and BUILD actions normally.
Keep modal buttons focusable; do not disable or remap shared UI input actions.

## Verification

Build with `dotnet build "No Way Down.csproj"`. Relevant Godot scenes:

- `tests/run_merge_probe.tscn`: all eight recipes through seeded drafts, exact
  catalyst costs, duplicate guards and reversible rank modifiers.
- `tests/buff_interface_probe.tscn`: live merge button, synchronization to Player,
  pause ownership, controller focus, narrow windows and larger text.
- `tests/gameplay_menu_input_probe.tscn`: actual pointer casts followed by
  movement and jump, HUD launchers, nested pause navigation and focus restoration.
- Existing `run_progression_probe`, `run_upgrade_probe`,
  `arena_reward_integration_probe` and `refuge_interface_probe` cover surrounding
  gameplay and navigation contracts.
- `run_reward_visual_review` and `refuge_visual_review` capture the actual views
  and all room palettes. Buff interface screenshots are captured when rendered.
