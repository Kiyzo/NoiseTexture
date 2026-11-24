using System.Collections.Generic;
using NoiseTexture;
using UnityEngine;

namespace NoiseTexture.Sample
{
    /// <summary>Fallback demo: the UI-independent generator feeds an IMGUI overlay.</summary>
    public sealed class CrtNoiseIMGUIDemo : MonoBehaviour
    {
        private NoiseTextureGenerator _noise;
        private bool _alternate;
        private NoiseType _type = NoiseType.Random;
        private float _scale = 1f;
        private float _size = 1f;
        private bool _cursorWasVisible;
        private CursorLockMode _cursorLock;

        private void OnEnable()
        {
            // IMGUI controls need a visible pointer.
            _cursorWasVisible = Cursor.visible;
            _cursorLock = Cursor.lockState;
            Cursor.visible = true;
            Cursor.lockState = CursorLockMode.None;
            RebuildNoise();
        }

        private void Update()
        {
            _noise?.Update(Time.unscaledDeltaTime);
            // A host project may relock the pointer in Update; keep it usable for the controls.
            if (!Cursor.visible || Cursor.lockState != CursorLockMode.None)
            {
                Cursor.visible = true;
                Cursor.lockState = CursorLockMode.None;
            }
        }

        private void OnGUI()
        {
            if (_noise == null) return;

            // Tint and centre-scaled rect keep the noise readable behind the controls.
            var previousColor = GUI.color;
            var width = Screen.width * _size;
            var height = Screen.height * _size;
            var rect = new Rect((Screen.width - width) * 0.5f, (Screen.height - height) * 0.5f, width, height);
            GUI.color = new Color(1f, 1f, 1f, 0.55f);
            GUI.DrawTexture(rect, _noise.Texture, ScaleMode.StretchToFill);
            GUI.color = previousColor;

            GUI.Label(new Rect(24f, 20f, 560f, 24f), "Noise Texture — IMGUI fallback (no UI Toolkit)");
            GUI.Label(new Rect(24f, 44f, 560f, 24f), "Sliders: noise scale / overlay size. Space toggles presets.");

            var x = 24f;
            var y = Screen.height - 196f;
            GUI.Label(new Rect(x, y, 300f, 20f), $"Noise scale  x{_scale:0.0}");
            var previousScale = _scale;
            _scale = GUI.HorizontalSlider(new Rect(x, y + 22f, 270f, 20f), _scale, 0.2f, 3f);
            if (!Mathf.Approximately(previousScale, _scale)) _noise.SmoothChangeSettings(CurrentPreset());

            GUI.Label(new Rect(x, y + 48f, 300f, 20f), $"Overlay size  x{_size:0.0}");
            _size = GUI.HorizontalSlider(new Rect(x, y + 70f, 270f, 20f), _size, 0.4f, 2.5f);

            GUI.Label(new Rect(x, y + 96f, 300f, 20f), "Noise type: " + _type);
            var previousBackground = GUI.backgroundColor;
            DrawTypeButton(x, y + 118f, NoiseType.Random);
            DrawTypeButton(x + 94f, y + 118f, NoiseType.Perlin);
            DrawTypeButton(x + 188f, y + 118f, NoiseType.Voronoi);
            GUI.backgroundColor = previousBackground;

            if (GUI.Button(new Rect(x, y + 148f, 138f, 28f), "Toggle preset")) TogglePreset();
            if (GUI.Button(new Rect(x + 146f, y + 148f, 138f, 28f), "Glitch burst"))
                _noise.SmoothChangeSettings(BurstPreset());

            // Space is a shortcut for the same toggle as the button.
            if (Event.current.type == EventType.KeyDown && Event.current.keyCode == KeyCode.Space) TogglePreset();
        }

        private void DrawTypeButton(float x, float y, NoiseType type)
        {
            GUI.backgroundColor = _type == type
                ? new Color(0.2f, 0.8f, 0.45f)
                : new Color(0.35f, 0.35f, 0.35f);
            if (GUI.Button(new Rect(x, y, 90f, 26f), type.ToString())) SelectType(type);
        }

        private void SelectType(NoiseType type)
        {
            // NoiseType is not interpolated, so a new generator is required for a switch.
            if (_type == type) return;
            _type = type;
            RebuildNoise();
        }

        private void RebuildNoise()
        {
            _noise?.Dispose();
            _noise = new NoiseTextureGenerator(CurrentPreset());
        }

        private void TogglePreset()
        {
            _alternate = !_alternate;
            _noise?.SmoothChangeSettings(CurrentPreset());
        }

        private void OnDisable()
        {
            _noise?.Dispose();
            _noise = null;
            Cursor.visible = _cursorWasVisible;
            Cursor.lockState = _cursorLock;
        }

        private List<NoiseSettings> CurrentPreset() => _alternate ? HarshPreset() : SoftPreset();

        private List<NoiseSettings> SoftPreset() => new()
        {
            new NoiseSettings
            {
                noiseType = _type, weight = 0.55f, scale = 9f * _scale,
                voronoiPoints = 20, noiseColor = new Color(1f, 0.96f, 0.53f)
            },
            new NoiseSettings
            {
                noiseType = _type, weight = 0.3f, scale = 11f * _scale,
                voronoiPoints = 20, noiseColor = new Color(0.5f, 0.19f, 0.9f)
            }
        };

        private List<NoiseSettings> HarshPreset() => new()
        {
            new NoiseSettings
            {
                noiseType = _type, weight = 0.8f, scale = 3f * _scale,
                voronoiPoints = 20, noiseColor = new Color(1f, 0.92f, 0.45f, 0.65f)
            },
            new NoiseSettings
            {
                noiseType = _type, weight = 0.2f, scale = 5f * _scale,
                voronoiPoints = 20, noiseColor = new Color(0.6f, 0.1f, 0.12f, 0.4f)
            }
        };

        private List<NoiseSettings> BurstPreset() => new()
        {
            new NoiseSettings
            {
                noiseType = _type, weight = 0.9f, scale = 3f * _scale,
                voronoiPoints = 20, noiseColor = new Color(1f, 0.92f, 0.45f, 0.75f)
            },
            new NoiseSettings
            {
                noiseType = _type, weight = 0.3f, scale = 12f * _scale,
                voronoiPoints = 20, noiseColor = new Color(0.8f, 0.1f, 0.1f, 0.35f)
            }
        };
    }
}
