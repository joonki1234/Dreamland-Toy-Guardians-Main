# Boss_GiftBox face setup

No placeholder artwork is included. Until both required assets are assigned, the runtime BossFace Quad exists but its Renderer is disabled. The previous sphere eyes and eye Point Light are removed.

## Assets to supply

- One RGBA face atlas, 4 columns by 4 rows. Suggested size: 1024 x 1024 (256 x 256 per cell).
- Transparent background; white/grayscale eyes, mouth and teeth so runtime tint can change red to gentle cyan. Keep all expressions aligned with identical face proportions. Leave transparent padding inside each cell to avoid filtering bleed.
- Import as Texture2D (Default is sufficient), sRGB on, Alpha Is Transparency on, Wrap Clamp, Filter Bilinear. Use Android ASTC with alpha; check readability and mip bleeding on Quest before choosing final compression/mip settings.
- One material using Universal Render Pipeline/Unlit: Surface Transparent, Alpha blending, Base Color white, Render Face Front, Alpha Clipping off. No lighting, emission feature, Bloom or Point Light is required. Brightness comes from the unlit base color; it does not illuminate nearby objects.

Cells are read left to right, starting at the TOP row:

| Row | Column 1 | Column 2 | Column 3 | Column 4 |
| --- | --- | --- | --- | --- |
| 1 | IdlePhase1: sharp eyes, evil grin | Summon: wide eyes, broad toothy grin | ApproachPhase2: angry squint, clenched teeth | IdlePhase2: sharper eyes/grin |
| 2 | ApproachPhase3: asymmetric eyes, manic open grin | IdlePhase3: sustained manic grin | SlamWindup: narrow eyes, clenched teeth | SlamImpact: wide eyes, open mouth |
| 3 | Spin: one narrow eye, crooked grin | Hit: short wince | DeathShock: startled eyes, broken smile | DeathWeak: drooping eyes, slack mouth |
| 4 | CleanseTransition: rounded eyes, small mouth | CleanseGentle: soft curved eyes, gentle smile | Unused | Unused |

## Inspector

Open Boss_GiftBox.prefab and use its FinalBossFaceController component:

1. Assign Face Atlas and Face Material. The component clones the material once at runtime; it does not modify the asset.
2. Keep Atlas Grid at 4 x 4 for the layout above.
3. Face Size is a fraction of the existing root BoxCollider's local X/Z size. Face Offset is a fraction of its local X/Y/Z size. Surface Offset moves the face outward from local -Y. Defaults account for GiftBox's -90-degree X rotation and large import scale.
4. In Play mode inspect FinalBoss/BossFace. It has one MeshRenderer, no active collider and no light. It follows the box, not the camera. Tune placement from front and side views with the final artwork; runtime Inspector tuning must be copied back to the prefab.

## Behavior and validation

- Successful minion spawns in the same frame produce one 0.6-second summon response.
- Actual approach begin/end controls the approach expression; no separate 2.15-second timer predicts movement.
- Slam windup/impact and spin use existing routine boundaries. Impact lasts 0.12 seconds.
- Priority: death/cleanse > approach/attack (including summon) > 0.12-second hit > HP idle. Hits do not interrupt an active attack expression; their timer still expires. No delayed hit is replayed after it expires.
- Death shock lasts 0.18 seconds, then weak face. At the existing defeat shrink loop, cleanse becomes gentle within the first 20% (at most 0.3 seconds), before removal.
- Verify 2/3 and 1/3 crossings, simultaneous hit/attack, rapid fatal damage, interrupted approach, six-minion bursts, disabled/destroyed boss, and cleanse before shrink makes the face unreadable.
- Existing combat ranges/timing/networking are unchanged. In the current configuration the approach minimum distance (7.5) exceeds the direct attack range (5.5), so normal play may not reach slam/spin. Do not change combat settings merely to make expressions fire in production.
- Check Quest 3 stereo visibility, front/back orientation, ribbon overlap, z-fighting, atlas padding, color readability and GPU cost on device. This change does not add network synchronization to existing local boss spawning/events.
