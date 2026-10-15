# Model replacement contract

## Player and enemies

Replace imported character art only inside these wrapper scenes:

- `art/player_visual.tscn`
- `art/mob_visual.tscn`

Enemy variants use the nine native scenes in `art/enemies/`, selected by
`EnemyVisualIdentity`. The wrapper and float animation remain stable when a
role changes. `EnemyCreatureMotion` owns facial and appendage animation inside
the selected model. The original `art/mob.glb` is retained as a reference.

Keep the wrapper root and its `ModelVisualRoot` script. Instance the new imported scene as the child named `Model`. Position, rotate, and scale that child inside the wrapper instead of changing `player.tscn`, `mob.tscn`, or gameplay code.

The stable gameplay path is `Pivot/Character`. Authored float animations target this wrapper, so the internal names of nodes in a replacement GLB may change freely.

For the player, set `TintMaterialName` on the wrapper to the material that customization should recolor. The current value is `body`. The material must import as `StandardMaterial3D`. Mutable tint materials are localized per Player instance automatically.

## What remains deliberately separate

Model replacement does not automatically change:

- player or enemy collision shapes;
- the enemy visibility notifier bounds;
- player trail and glow positions;
- the authored player skin shell and visor alignment;
- enemy archetype accent geometry;
- the refuge camera and mannequin framing.

These values affect readability or gameplay and must be reviewed intentionally after the final model scale is known. The refuge mannequin instantiates `player.tscn`, so it receives the replacement player model automatically.

## Environment models

Most roads, arenas, obstacles, refuge architecture, and combat effects are currently procedural primitive meshes. One production chapter now proves the hybrid route: deterministic code selects and connects authored PackedScene locations, arenas and obstacles, while legacy sections remain as a safe fallback during the staged migration. Model-specific paths must not be added to `ProceduralWorld`.

New roads, connectors, arenas, buildings and reusable obstacles must follow [world-content-contract.md](world-content-contract.md). Final models are placed below the reference scene's replaceable geometry root; sockets and gameplay collisions remain authored independently.

## Required checks after a swap

Run:

1. `model_replacement_contract_probe.tscn`;
2. `player_presentation_probe.tscn`;
3. `nitro_regression_probe.tscn`;
4. `refuge_visual_review.tscn`;
5. `run_world_visual_review.tscn`.

Then visually verify collision height, forward direction, ground contact, trail origin, glow position, skin alignment, enemy notifier bounds, and refuge framing.
