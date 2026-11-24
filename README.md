# Noise Texture Updater

A small self-contained procedural noise generator for Unity: a 256×256 RGBA32 texture built in a
Burst `IJobParallelFor`, with animated Random / Perlin / Voronoi layers and smooth preset transitions.

**Primary tested version: Unity 6000.6.7f1** (the project version where it is developed and
verified). `2022.3` in `package.json` is the declared minimum.

---

## Why

A noise generator is not always at hand, so I made this small package. It has three noise types —
**Random**, **Perlin** and **Voronoi** — all animated over time, with smooth transitions between
presets. If it is useful to someone, I am glad. Change it, fork it, reuse it: MIT license, do with
it what you want.

---

## Install and run

1. Copy the `NoiseTexture` folder into `Assets/Plugins/` (plain assets).
2. Let Unity import and compile.
3. Pick a demo from **Noise Texture**:
   - **Open Demo - UI Toolkit** — classic `UIDocument`.
   - **Open Demo - PanelRenderer (Unity 6)** — Unity 6 `PanelRenderer`, same UI and script.
   - **Open Demo - IMGUI (no UI Toolkit)** — fallback for projects without the UI module.
4. Press **Play**. All scenes unlock the cursor and expose live controls:
   - **Noise scale** slider — layer scale (smooth 2-second transition);
   - **Overlay size** slider — zooms the noise layer (UI Toolkit `style.scale`, IMGUI draw rect);
   - **Random / Perlin / Voronoi** buttons — rebuild the generator with the chosen type;
   - **Toggle preset** and **Glitch burst** buttons. `Space` also toggles in the IMGUI scene.

Scenes live in `Samples/Demo/Scenes/` and are generated on first open. **Regenerate Demo Scenes**
in the same menu rebuilds them.

---

## Key files — the code that actually generates noise

- `Runtime/NoiseTextureGenerator.cs` — `NoiseType`, `NoiseSettings`, `NoiseSettingsBurst` and
  `NoiseCalculationJob` (the Burst job computing Random/Perlin/Voronoi per pixel), plus
  `NoiseTextureGenerator`, which owns the texture, native buffers and preset transitions.
- `Runtime/NoiseTextureUpdater.cs` — thin UI Toolkit adapter that puts the generated texture into
  `VisualElement.style.backgroundImage`. Compiled only with `NOISETEXTURE_UIELEMENTS`.

Everything else (README, license, changelog, tests, scenes) is packaging and examples.

---

## UI Toolkit availability and the `#if` switch

The generator core has **no UI Toolkit dependency**. The adapter and its demo compile only when
`com.unity.modules.uielements` is present:

- asmdefs declare `versionDefines`: `com.unity.modules.uielements >= 1.0.0` →
  `NOISETEXTURE_UIELEMENTS`;
- `NoiseTextureUpdater.cs` and `CrtNoiseDemo.cs` are wrapped in `#if NOISETEXTURE_UIELEMENTS`;
- without the symbol, use `NoiseTextureGenerator.Texture` with any other UI path. The IMGUI demo
  always works;
- the symbol can also be set manually in Player Settings → Scripting Define Symbols.

| Symbol present | Available API | Demo scenes |
|---|---|---|
| `NOISETEXTURE_UIELEMENTS` | `NoiseTextureGenerator` + `NoiseTextureUpdater` | UI Toolkit, PanelRenderer, IMGUI |
| absent | `NoiseTextureGenerator` only | IMGUI |

---

## PanelRenderer (Unity 6) with UIDocument support

`CrtNoiseDemo` is a compatibility bridge: it uses `UnityEngine.UIElements.PanelRenderer` when present
(Unity 6 runtime UXML rendering with `RegisterUIReloadCallback`), otherwise `UIDocument`. Both paths
bind the same noise element and controls. A versioned callback skips duplicate rebuilds. The cursor
is unlocked in `OnEnable` and every frame, then restored on disable. The demo UI and its sliders are
built in C#, so no host-project theme or UXML parser is required.

---

## API

| Member | Purpose |
|---|---|
| `NoiseTextureGenerator(List<NoiseSettings>)` | Owns the texture and native buffers; draws the first frame immediately. |
| `NoiseTextureGenerator.Texture` | Generated `Texture2D` (do not destroy it yourself). |
| `NoiseTextureGenerator.Update(float)` / `SmoothChangeSettings(...)` / `Dispose()` | Recompute / transition / release. |
| `NoiseTextureUpdater(VisualElement, List<NoiseSettings>)` | UI Toolkit adapter (symbol-gated). |
| `NoiseTextureUpdater.Update(float)` | Recomputes and assigns `style.backgroundImage`. |
| `NoiseSettings` | `noiseType`, `weight` (0…1), `scale`, `voronoiPoints`, `noiseColor`, `offset`. |
| `NoiseType` | `Random`, `Perlin`, `Voronoi`. |
| `NoiseSettingsBurst`, `NoiseCalculationJob` | Public Burst structs for custom jobs and tests. |

---

## Invariants and gotchas

- **Layer count and Voronoi point count are constant after construction.** Transitions only change
  weight, scale, offset and colour; a transition never overreads the native point buffer.
- **Layer type is not interpolated.** To change `NoiseType`, create a new instance.
- **Voronoi points are normalized `[0,1)`** and tiled `Scale` times across the texture; higher
  `Scale` means more, smaller cells. `Offset` shifts the tile; time drifts the points.
- **Time comes from `Time.unscaledTime`**, so noise animates while the game is paused. The
  `deltaTime` argument only drives the 2-second transition timer.
- `NoiseTextureUpdater` overwrites `backgroundImage` each `Update`; one updater per element.
- **Main thread only** for `NativeArray`, `Texture2D` and UI Toolkit. One Burst job, batch size 64,
  no managed allocations per frame.
- `Dispose()` is idempotent; `Update()` after disposal throws `ObjectDisposedException`.
- Suggested rate: 30–60 Hz for visible static, 8–10 Hz for a subtle layer. On mobile prefer 1–2
  layers: Voronoi checks every point for every pixel.

---

## Requirements

- Unity 2022.3+ (developed and tested on Unity 6000.6.7f1).
- `com.unity.burst` 1.8+, `com.unity.collections` 2.x, `com.unity.mathematics` 1.2+.
- UI Toolkit optional; IMGUI is built into Unity.
