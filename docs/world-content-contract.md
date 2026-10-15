# Authored world content contract

This contract is the boundary between replaceable environment art and deterministic run generation. The reference catalog now supplies the bounded production vertical slice; unmigrated chapters continue through the legacy builder.

## Coordinate convention

- Chunk roots use identity scale and local forward `-Z`.
- `Entry` is the positive-Z socket and `Exit` is the negative-Z socket.
- Geometry, collisions and model-local transforms stay below `Geometry`.
- A model may be replaced without moving the root, sockets or gameplay collision.
- Visual bounds must not be used to generate collision automatically.

## Required WorldChunk paths

- `Entry` and `Exit`: `Marker3D` connection sockets.
- `PlayableBounds`: bounds origin; size is stored in `WorldChunk.PlayableSize`.
- `ObstacleSlots`: one or more local placement markers.
- `EnemySpawnAreas`: one or more local spawn-area markers.
- `Metadata/Camera` and `Metadata/Pressure`: stable metadata anchors.
- `Geometry`: replaceable visuals and authored collision bodies.

Every child of `ObstacleSlots` is a `Marker3D`. A slot may declare `max_footprint`, `required_obstacle_tags`, and `spawn_chance` metadata. The assembler filters catalog entries by those values, chunk kind, theme, chapter and difficulty before a weighted seeded choice. A slot without required tags may remain empty when the catalog has no compatible obstacle.

`WorldChunkDefinition` stores the PackedScene, selection weight, kind, theme, width class, socket profiles, chapter/difficulty gates and allowed neighbour kinds. It contains no seed, active encounter or per-run state.

## Obstacles and structures

Reusable obstacles use a `WorldObstacle` root and a `WorldObstacleDefinition`. The definition declares footprint, gameplay tags, allowed chunk kinds, difficulty and weight. Buildings that are inseparable from a location stay below that chunk's `Geometry`; reusable structures may use `WorldObstacleTag.Structure` and the same footprint contract.

Mutable materials must be `resource_local_to_scene`. Collision is authored explicitly below a `CollisionObject3D`; it is never inferred from a mesh AABB.

## Adding content

1. Duplicate the closest reference scene under `world/chunks` or `world/obstacles`.
2. Preserve the contract root and required stable paths.
3. Replace only the content below `Geometry` or `Visual`, then author simplified gameplay collision.
4. Create one matching definition under `world/content` and add it to the catalog.
5. Run `world_content_contract_probe.tscn` and `world_content_visual_review.tscn`.

The reference vertical slice is `reference_travel` → `reference_connector` → `reference_arena` → `reference_exit_connector`. The two connector definitions are directional: the entrance converts `Standard → Wide`, while the exit converts `Wide → Standard`; authored chunks must not be reversed implicitly.

Reference assets are prototypes for contract validation and the migration path, not final environment art.
