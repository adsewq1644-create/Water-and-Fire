# Water Spirit Sprite Shader

## Scope

- Unity 6000.0.49f1, URP 17.0.4, URP 2D Renderer.
- Shader: `WaterAndFire/WaterSpirit`.
- Material: `Assets/Shaders/WaterSpirit/M_WaterSpirit.mat`.
- Runtime values are supplied with `MaterialPropertyBlock`; the shared material is never mutated by the controller.

## Flow model

The shader samples two independent channels from the large noise at different directions and rates. That vector offsets the UV used for the detail noise. A second detail sample then perturbs the final detail lookup. The resulting shared flow field drives deep/shallow color, caustic highlights, internal color distortion, edge intensity, and emission.

The sprite alpha is always sampled at the original SpriteRenderer UV. Only color and procedural pattern UVs are distorted. `WaterSpiritShaderController` supplies the current sprite atlas rect so animation frames do not make the pattern jump to an unrelated atlas region. Neighbor alpha reads are clamped inside that rect to avoid bleeding from adjacent sprites.

## Rendering choices

- Uses the URP 2D Lit path and receives 2D Light blend styles.
- Includes a Universal Forward unlit fallback for previews and non-2D renderer fallbacks.
- Uses transparent alpha blending, `Cull Off`, and `ZWrite Off`.
- Keeps SpriteRenderer color, Flip X/Y, sorting layer, order, and animated sprite changes intact.
- Does not sample Scene Color or Camera Opaque Texture. Reliable background refraction in the URP 2D Renderer would require a camera copy or Renderer Feature, so the effect uses safe internal UV refraction instead.
- Does not modify the existing Selective Glow Bloom Renderer Feature. HDR highlight and edge colors can feed the existing bloom when their output is bright enough.

## Texture import

`WaterNoise_Large.png` and `WaterNoise_Detail.png` are generated as seamless 64 x 64 textures with Repeat wrap, Point filtering, no mip maps, linear sampling, and no compression.

## Recommended starting values

| Goal | Property | Start |
| --- | --- | ---: |
| Stronger large motion | Large Distortion Strength | 0.17 |
| Larger forms | Large Flow Scale | 0.92 |
| Finer ripples | Detail Flow Scale | 1.35 |
| More detail bending | Detail Distortion Strength | 0.045 |
| Slower overall flow | Large / Detail Flow Speed | 0.12 / 0.19 |
| Stronger color split | Pattern Contrast | 1.30 |
| Harder pixel style | Pixelation Strength | 0.58 |
| Coarser pixels | Pixel Size | 24-36 |
| Stronger movement response | Movement Flow / Distortion Multiplier | 1.35 / 1.28 |
| Stronger landing ring | Landing Pulse Strength | 1.00 |

`TriggerLandingPulse()` is public for an explicit gameplay event, while the default controller also detects landing through the read-only `PlayerCharacter.IsGrounded` state. If that component is absent, it falls back to upward Rigidbody2D contact normals.

`PlayerCharacter.EnsureElementVisualEffect()` routes the water element to `WaterSpiritShaderController` so the retired runtime `WaterSpriteWobble` material cannot overwrite this material during `Awake`.
