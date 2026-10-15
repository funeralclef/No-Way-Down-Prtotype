# Creature models

Original native Godot geometry; no external asset license is required.
The family keeps the original one-eye floating creature concept. Each role has
its own scene and anatomy, combining soft proportions with small unsettling details.

| Role | Form | Gentle / unsettling contrast |
| --- | --- | --- |
| Springboard | Round bud, belly, leaf ears, bounce crown | Huge round eye / tiny fangs |
| Spear | Narrow horned pup with arrow tail | Soft haunches / slit pupil, brow and nasal horn |
| Courier | Small moth with broad soft wings | Oversized eye / wing claws and one crooked tooth |
| Shepherd | Wide lantern guardian with short arms | Sleepy face / glowing horn tips and underbite |
| Trickster | Tall imp with crooked horns and barbed tail | Cheeks / asymmetric horns and sharp grin |
| Shell | Grumpy turtle with a segmented carapace | Stubby paws / claws, fangs and a heavy brow |
| Relay | Floating ghost seed with wisps and an uplink crown | Round eye / tapering ghost limbs |
| Diver | Vampire ray with fins, keel and forked tail | Soft head / predatory eye and small fangs |
| Twin | Two joined cyclops puffs | Shared belly / contrasting expressions and paired teeth |

`EnemyVisualIdentity` selects a scene. `ModelVisualRoot` applies an instance-local
semantic palette: `mob_body`, `mob_eye`, `pupil`, `armor`, `tooth`, `glint`, `accent`.
`EnemyCreatureMotion` binds the optional Eyes/Blink, SecondEyes/Blink,
LeftAppendage, RightAppendage and TailGroup nodes. Keep gameplay collision and
navigation outside these models.

Scenes can be edited in Godot. The source geometry recipe is
`tools/build_enemy_models.py`; running it rewrites all nine scene files, so
carry any manual scene edits back into the recipe before regenerating.
