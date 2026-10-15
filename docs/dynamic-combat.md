# Dynamic encounters and device skills

## Design references

These are design interpretations, not copied game content:

- [Returnal, official PlayStation overview](https://www.playstation.com/en-us/editorial/everything-you-need-to-know-about-returnal/): movement-focused combat, readable attack fields and equipment that changes actions. Here the runner answers ground patterns with jumps, steering and short blinks.
- [Hades, Supergiant FAQ](https://www.supergiantgames.com/blog/hades-faq/): run-to-run combinations and fast action inform the device/catalyst/evolution structure. Each device adds a visible rule, not just a larger stat.
- [Dead Cells, official Malaise update](https://deadcells.com/patchnotes/21): mutations attach attacks, parries and effects to actions such as rolling. Here existing dodges, stomps and reactor release trigger counterwaves, echo platforms and wind wakes.

All models, geometry and effects remain original project assets.

## Guardian policy

`BossBrain` is a scene-free seeded policy. Inputs are current distance, runner
velocity, observed stillness, phase, recent attack and the arena footprint.
It never reads future input. A weighted choice prevents consecutive repeats;
anchor scoring chooses a new side with body clearance and a reachable distance
from the player. Identical observations and seed replay the same decisions.

`BossEncounter` owns intro, reposition, attack, opening, recovery, phase change
and victory. Repositioning is continuous at bounded speed. During patterns,
the guardian strafes or approaches; during an exposed crown it stops so the
runner can finish a committed jump. Charges lock their endpoint before firing.
The two phases still require eight total crown hits and one hit per opening.

`BossAttacks` schedules strikes and owns their cleanup. `BossHazard` draws and
tests the same ring, lane or disc. Marked slams sample the player's position
when the warning appears, then stop tracking. Interrupting a warning does not
erase an already active attack. Pausing freezes all encounter clocks.

| Guardian | Pattern vocabulary | Second-phase / environment interaction |
| --- | --- | --- |
| Bellwarden | Moving resonance rings, chime fans, an approaching echo chase | Delayed echo ring and locked ground slams; bloom pads help time a second jump |
| Thornmaw | Locked horn rush, sequential thorn marks, bramble fans | Two rushes or lingering marks; bait a rush endpoint into a resonator for an extended opening |
| Prismweaver | Orbiting seals, mirror exchange, prism lattice | Two seals in order; mirror seals change sides after the first break; lattice adds a marked slam |

Hunt Spark marks a guardian and extends one opening rather than bypassing its
shield. Repeated marks do not stack. The camera includes the active seal in
its focus during a puzzle.

## Arena geometry

`ArenaLayout` is the immutable convex footprint shared by floor geometry,
camera clamping, active arena containment, AI destinations and enemy spawns.
The ascent includes a round introduction, an authored courtyard, a bevelled
long hall and a hexagonal final court. Later legacy arenas rotate the recipes.
`ArenaGeometry` builds mesh and convex floor collision from the same vertices;
bridges preserve the travel sockets. `ArenaTactics` adds optional bloom pads
and resonators, leaving the main play surface open. Existing authored world
contracts are retained. `TryGetArenaCircle` remains only a compatibility
centre/radius envelope for pressure and wave policy.

## Devices as actions

`RunSkillCatalog` describes four active casts. `RunSkillRuntime` owns equipped
selection, shared cooldown, anchors and passive rules. `PlayerSkillActions`
is a small partial Player adapter: only Player moves its body and spends its
nitro. `PlayerRunBuild` remains deterministic rank/modifier state. The existing
merge transaction and reward economy are unchanged.

| Device | New behavior | Evolution |
| --- | --- | --- |
| Overpass | Air Step: steering blink with no enemy target | Phase Route leaves an Echo Step at departure |
| Shock Sole | Fault Dive: airborne plunge, impact pulse interrupts nearby warnings | Earth Splitter widens its warning-clearing field |
| Hunter Lens | Hunt Spark: homing projectile strikes a creep or marks a guardian | Two Horizons launches at two separate targets |
| Red Thread | Thread Recall: plant an anchor, cast again to return; Rift auto-plants | Chain Reaction leaves an Echo Step when recalling |
| Glass Heart | Visible one-hit glass guard; three stomps rebuild it | Last Light breaks into a warning-clearing pulse |
| Closed Loop | Every fifth combo stomp makes a friendly single-use Echo Step | Infinite Line uses a four-stomp rhythm with two platforms allowed |
| Kinetic Reserve | Perfect dodge releases a counterwave and stores stomp energy | Perfect Circuit also makes an Echo Step |
| Afterburner | Reactor release leaves a wind lane that can be crossed for momentum | Blue Comet wakes also interrupt nearby warnings |

Only one active cast is equipped. E / controller X / touch button casts it;
BUILD offers explicit Equip buttons. Keyboard binding is configurable in
settings and the HUD shows the actual key. Equipping another device retains
the shared cooldown. Failed movement casts consume nothing. Recall's first
press plants a free five-second preview; the return spends nitro and starts
the cooldown. Blink/recall destinations require floor, clear space and a clear
world path, and cannot cross sealed arena boundaries.

`RunSkillTarget` uses explicit Mob targeting capabilities: friendly supports
can be jump/Rift targets without dealing damage or awarding kills. `BossMob`
uses the same capability boundary for an exposed crown. Projectiles and echo
steps have finite lifetimes; their counts are bounded. `ui/run_skill_hud.tscn`
is an authored compact cast view. Touch placement is derived from the actual
joystick geometry. Interactive controls register touch exclusions so the broad
lower-screen gesture zones cannot steal a cast. Friendly supports do not
consume enemy spawn slots.

## Verification

- `arena_tactics_probe`: all convex shapes, boundary projection, policy replay,
  destination clearance, movement, attack variety and production arena binding.
- `run_skills_probe`: physical floor/walls, blink costs and cooldown, friendly
  target semantics, one-hit guard, recall, homing impact, dive and warning interruption.
- `boss_encounter_probe` and `run_finale_integration_probe`: both phases of all
  guardians, a real Rift-to-crown landing, victory gates and continuation.
- `settings_layout_probe`, `run_settings_input_probe`, `buff_interface_probe`,
  `run_merge_probe`, `nitro_regression_probe` and traversal probes retain their
  existing regression coverage.
- `boss_visual_review`: all guardians and phases inside the production final arena.

`device_interface_probe` covers 390×844, 640×360 and 1152×648 touch layouts,
pointer casts, the physical E key and independent joystick/reactor gesture zones.

Arena encounters seal only after the runner steps onto the actual footprint.
Entry bridges remain ordinary traversal, avoiding a snap from the bridge to
the polygon boundary. Victory releases containment before the next route.
