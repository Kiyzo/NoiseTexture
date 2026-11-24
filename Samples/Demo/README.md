# Demos — install and run

Open one of these from **Noise Texture**; the scene is generated on first open:

- **Demo - UI Toolkit** — `UIDocument` + `CrtNoiseDemo`.
- **Demo - PanelRenderer (Unity 6)** — the same UI and script on Unity 6 `PanelRenderer`
  (uses `RegisterUIReloadCallback`).
- **Demo - IMGUI (no UI Toolkit)** — `CrtNoiseIMGUIDemo` draws the generator texture with
  `GUI.DrawTexture`; works without the UI module.

All scenes unlock the cursor and provide **Noise scale** and **Overlay size** sliders,
**Random / Perlin / Voronoi** type buttons, plus **Toggle preset** and **Glitch burst**. The IMGUI
scene also accepts `Space`. **Regenerate Demo Scenes** rebuilds the scenes if they were edited.

`CrtNoiseDemo` handles both `PanelRenderer` and `UIDocument`. `NoiseDemoTheme.tss` is assigned to
`DemoPanelSettings` automatically, and the panel uses `ScaleWithScreenSize`. The UI and its sliders
are built in C#, so a fresh project shows the noise layer and controls. `CrtNoiseDemo.cs` is wrapped
in `#if NOISETEXTURE_UIELEMENTS`; without UI Toolkit, only the IMGUI scene exists.
