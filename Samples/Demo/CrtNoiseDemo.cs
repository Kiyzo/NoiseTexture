#if NOISETEXTURE_UIELEMENTS
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;
using Cursor = UnityEngine.Cursor;

namespace NoiseTexture.Sample
{
    /// <summary>UI Toolkit demo: works with Unity 6 PanelRenderer or classic UIDocument.</summary>
    public sealed class CrtNoiseDemo : MonoBehaviour
    {
        private NoiseTextureUpdater _noise;
        private VisualElement _root;
        private VisualElement _noiseElement;

        private Button _toggleButton;
        private Button _burstButton;
        private Button _randomButton;
        private Button _perlinButton;
        private Button _voronoiButton;
        private NoiseSlider _scaleSlider;
        private NoiseSlider _sizeSlider;
        private Label _scaleValue;
        private Label _sizeValue;
        private Label _typeValue;

        private PanelRenderer _panel;
        private PanelRenderer.VersionedUIReloadCallback _reload;
        private int _version = -1;
        private bool _alternate;
        private NoiseType _type = NoiseType.Random;
        private float _scale = 1f;
        private float _size = 1f;

        private bool _cursorWasVisible;
        private CursorLockMode _cursorLock;

        private void OnEnable()
        {
            // Demo controls are useless without a pointer, so unlock the cursor.
            _cursorWasVisible = Cursor.visible;
            _cursorLock = Cursor.lockState;
            Cursor.visible = true;
            Cursor.lockState = CursorLockMode.None;

            _panel = GetComponent<PanelRenderer>();
            if (_panel != null)
            {
                // PanelRenderer rebuilds its tree on asset reload; the version guard skips duplicates.
                _reload = OnPanelReload;
                _panel.RegisterUIReloadCallback(_reload);
                return;
            }

            TryBindDocument();
        }

        private void LateUpdate()
        {
            // UIDocument builds its root after OnEnable, so keep trying until it exists.
            if (_root == null) TryBindDocument();
            _noise?.Update(Time.unscaledDeltaTime);
            UnlockCursor(); // A host project may relock the pointer in Update.
        }

        private void OnPanelReload(PanelRenderer renderer, VisualElement root, int version)
        {
            if (version == _version && root == _root) return;
            _version = version;
            Bind(root);
        }

        private void TryBindDocument()
        {
            var document = GetComponent<UIDocument>();
            if (document == null) return;
            var root = document.rootVisualElement;
            if (root != null) Bind(root);
        }

        private void Bind(VisualElement root)
        {
            if (root == null) return;
            DetachControls();
            _root = root;

            // Build the whole UI in C#: UXML parsing differs between Unity versions and can
            // silently drop elements, leaving a black screen with only a title label.
            BuildUi(root);
            RebuildNoise();
            WireControls();
            UpdateReadouts();
        }

        private void BuildUi(VisualElement root)
        {
            root.Clear();
            SizeRoot(root);

            var shell = new VisualElement();
            Fill(shell);
            shell.style.backgroundColor = new StyleColor(new Color(0.024f, 0.031f, 0.027f, 1f));
            root.Add(shell);

            var title = new Label("NOISE TEXTURE — UI TOOLKIT");
            Place(title, 16f, 12f);
            title.style.color = new StyleColor(new Color(0.2f, 1f, 0.6f));
            title.style.fontSize = 14;
            title.style.unityFontStyleAndWeight = FontStyle.Bold;
            shell.Add(title);

            var hint = new Label("Sliders change scale and size; buttons switch noise type and presets.");
            Place(hint, 16f, 36f);
            hint.style.color = new StyleColor(new Color(0.7f, 0.75f, 0.7f));
            hint.style.fontSize = 12;
            shell.Add(hint);

            _noiseElement = new VisualElement { pickingMode = PickingMode.Ignore };
            Fill(_noiseElement);
            _noiseElement.style.opacity = 0.65f;
            shell.Add(_noiseElement);

            var controls = new VisualElement();
            Place(controls, 16f, 70f);
            controls.style.width = new Length(340, LengthUnit.Pixel);
            controls.style.paddingLeft = 10f;
            controls.style.paddingRight = 10f;
            controls.style.paddingTop = 10f;
            controls.style.paddingBottom = 10f;
            controls.style.backgroundColor = new StyleColor(new Color(0f, 0f, 0f, 0.7f));
            var border = new StyleColor(new Color(0.2f, 1f, 0.6f, 0.35f));
            controls.style.borderLeftColor = border;
            controls.style.borderRightColor = border;
            controls.style.borderTopColor = border;
            controls.style.borderBottomColor = border;
            controls.style.borderLeftWidth = 1f;
            controls.style.borderRightWidth = 1f;
            controls.style.borderTopWidth = 1f;
            controls.style.borderBottomWidth = 1f;
            shell.Add(controls);

            var header = new Label("NOISE CONTROLS");
            header.style.color = new StyleColor(new Color(0.2f, 1f, 0.6f));
            header.style.fontSize = 12;
            header.style.unityFontStyleAndWeight = FontStyle.Bold;
            header.style.marginBottom = 4f;
            controls.Add(header);

            _scaleValue = AddLabel(controls);
            _scaleSlider = AddSlider(controls, 0.2f, 3f, _scale);
            _sizeValue = AddLabel(controls);
            _sizeSlider = AddSlider(controls, 0.4f, 2.5f, _size);
            _typeValue = AddLabel(controls);
            _typeValue.style.marginBottom = 2f;

            var types = AddRow(controls);
            _randomButton = AddButton(types, "Random", () => SelectType(NoiseType.Random));
            _perlinButton = AddButton(types, "Perlin", () => SelectType(NoiseType.Perlin));
            _voronoiButton = AddButton(types, "Voronoi", () => SelectType(NoiseType.Voronoi));

            var presets = AddRow(controls);
            presets.style.marginTop = 4f;
            _toggleButton = AddButton(presets, "Toggle preset", OnToggleClicked);
            _burstButton = AddButton(presets, "Glitch burst", OnBurstClicked);
            _burstButton.style.color = new StyleColor(new Color(1f, 0.47f, 0.35f));
        }

        private void SizeRoot(VisualElement root)
        {
            // A freshly created panel can report a zero-height root even in ScaleWithScreenSize mode.
            // Give it an explicit size so full-screen children and top-anchored controls render.
            var size = new Vector2Int(Screen.width, Screen.height);
            var panelSettings = _panel != null
                ? _panel.panelSettings
                : GetComponent<UIDocument>()?.panelSettings;
            if (panelSettings != null && panelSettings.scaleMode == PanelScaleMode.ScaleWithScreenSize)
            {
                var reference = panelSettings.referenceResolution;
                if (reference.x > 0 && reference.y > 0) size = reference;
            }
            root.style.width = size.x;
            root.style.height = size.y;
        }

        private static void Fill(VisualElement element)
        {
            element.style.position = Position.Absolute;
            element.style.left = new Length(0, LengthUnit.Percent);
            element.style.top = new Length(0, LengthUnit.Percent);
            element.style.right = new Length(0, LengthUnit.Percent);
            element.style.bottom = new Length(0, LengthUnit.Percent);
        }

        private static void Place(VisualElement element, float left, float top)
        {
            element.style.position = Position.Absolute;
            element.style.left = left;
            element.style.top = top;
        }

        private static Label AddLabel(VisualElement parent)
        {
            var label = new Label();
            label.style.color = new StyleColor(new Color(0.82f, 0.86f, 0.82f));
            label.style.fontSize = 11;
            parent.Add(label);
            return label;
        }

        private static NoiseSlider AddSlider(VisualElement parent, float low, float high, float value)
        {
            var slider = new NoiseSlider(low, high, value);
            parent.Add(slider.Root);
            return slider;
        }

        private static VisualElement AddRow(VisualElement parent)
        {
            var row = new VisualElement();
            row.style.flexDirection = FlexDirection.Row;
            parent.Add(row);
            return row;
        }

        private static Button AddButton(VisualElement parent, string text, System.Action onClick)
        {
            var button = new Button(onClick) { text = text };
            button.style.flexGrow = 1f;
            button.style.height = 28f;
            button.style.marginRight = 3f;
            button.style.fontSize = 11;
            parent.Add(button);
            return button;
        }

        private void WireControls()
        {
            if (_scaleSlider != null) _scaleSlider.OnValueChanged += OnScaleChanged;
            if (_sizeSlider != null) _sizeSlider.OnValueChanged += OnSizeChanged;
        }

        private void DetachControls()
        {
            if (_scaleSlider != null) _scaleSlider.OnValueChanged -= OnScaleChanged;
            if (_sizeSlider != null) _sizeSlider.OnValueChanged -= OnSizeChanged;
            _noise?.Dispose();
            _noise = null;
        }

        private void OnScaleChanged(float value)
        {
            // Layer count is constant; only scale is interpolated by the generator.
            _scale = value;
            UpdateReadouts();
            _noise?.SmoothChangeSettings(CurrentPreset());
        }

        private void OnSizeChanged(float value)
        {
            _size = value;
            UpdateReadouts();
            ApplySize();
        }

        private void SelectType(NoiseType type)
        {
            // NoiseType is not interpolated, so a new updater is required for a switch.
            _type = type;
            UpdateReadouts();
            RebuildNoise();
        }

        private void RebuildNoise()
        {
            if (_noiseElement == null) return;
            _noise = new NoiseTextureUpdater(_noiseElement, CurrentPreset());
            ApplySize();
        }

        private void ApplySize() =>
            _noiseElement.style.scale = new Scale(new Vector2(_size, _size));

        private void UpdateReadouts()
        {
            if (_scaleValue != null) _scaleValue.text = $"Noise scale  x{_scale:0.0}";
            if (_sizeValue != null) _sizeValue.text = $"Overlay size  x{_size:0.0}";
            if (_typeValue != null) _typeValue.text = $"Type: {_type}";

            Highlight(_randomButton, _type == NoiseType.Random);
            Highlight(_perlinButton, _type == NoiseType.Perlin);
            Highlight(_voronoiButton, _type == NoiseType.Voronoi);
        }

        private static void Highlight(Button button, bool active)
        {
            if (button == null) return;
            button.style.backgroundColor = new StyleColor(active
                ? new Color(0.08f, 0.28f, 0.16f)
                : new Color(0.04f, 0.06f, 0.05f));
            button.style.color = new StyleColor(active
                ? new Color(0.2f, 1f, 0.6f)
                : new Color(0.7f, 0.75f, 0.7f));
        }

        private void OnToggleClicked() => TogglePreset();

        /// <summary>Smooth 2-second transition between soft and harsh presets.</summary>
        public void TogglePreset()
        {
            _alternate = !_alternate;
            _noise?.SmoothChangeSettings(CurrentPreset());
        }

        private void OnBurstClicked() => _noise?.SmoothChangeSettings(BurstPreset());

        private void OnDisable()
        {
            if (_panel != null && _reload != null) _panel.UnregisterUIReloadCallback(_reload);
            DetachControls();

            _root = null;
            _noiseElement = null;
            _toggleButton = null;
            _burstButton = null;
            _randomButton = null;
            _perlinButton = null;
            _voronoiButton = null;
            _scaleSlider = null;
            _sizeSlider = null;
            _scaleValue = null;
            _sizeValue = null;
            _typeValue = null;
            _reload = null;
            _panel = null;
            _version = -1;

            Cursor.visible = _cursorWasVisible;
            Cursor.lockState = _cursorLock;
        }

        private void UnlockCursor()
        {
            if (!Cursor.visible || Cursor.lockState != CursorLockMode.None)
            {
                Cursor.visible = true;
                Cursor.lockState = CursorLockMode.None;
            }
        }

        /// <summary>Self-styled slider: the demo cannot rely on a host theme for Slider internals.</summary>
        private sealed class NoiseSlider
        {
            public readonly VisualElement Root;
            public System.Action<float> OnValueChanged;

            private readonly float _min;
            private readonly float _max;
            private readonly VisualElement _fill;
            private readonly VisualElement _knob;
            private bool _dragging;

            public float Value { get; private set; }

            public NoiseSlider(float min, float max, float initial)
            {
                _min = min;
                _max = max;
                Value = initial;

                Root = new VisualElement();
                Root.style.height = 22f;
                Root.style.marginBottom = 4f;

                var track = new VisualElement();
                track.style.position = Position.Absolute;
                track.style.left = new Length(0, LengthUnit.Percent);
                track.style.top = 9f;
                track.style.width = new Length(100, LengthUnit.Percent);
                track.style.height = 4f;
                track.style.backgroundColor = new StyleColor(new Color(0.06f, 0.1f, 0.08f, 1f));
                track.style.borderLeftColor = new StyleColor(new Color(0.24f, 0.4f, 0.3f, 1f));
                track.style.borderRightColor = track.style.borderLeftColor.value;
                track.style.borderTopColor = track.style.borderLeftColor.value;
                track.style.borderBottomColor = track.style.borderLeftColor.value;
                track.style.borderLeftWidth = 1f;
                track.style.borderRightWidth = 1f;
                track.style.borderTopWidth = 1f;
                track.style.borderBottomWidth = 1f;
                Root.Add(track);

                _fill = new VisualElement();
                _fill.style.height = new Length(100, LengthUnit.Percent);
                _fill.style.backgroundColor = new StyleColor(new Color(0.2f, 1f, 0.6f, 0.65f));
                track.Add(_fill);

                _knob = new VisualElement();
                _knob.style.position = Position.Absolute;
                _knob.style.top = 4f;
                _knob.style.width = 14f;
                _knob.style.height = 14f;
                _knob.style.marginLeft = -7f;
                _knob.style.backgroundColor = new StyleColor(new Color(0.2f, 1f, 0.6f, 1f));
                Root.Add(_knob);

                Root.RegisterCallback<PointerDownEvent>(evt =>
                {
                    _dragging = true;
                    Root.CapturePointer(evt.pointerId);
                    SetFromLocalX(evt.localPosition.x);
                });
                Root.RegisterCallback<PointerMoveEvent>(evt =>
                {
                    if (_dragging) SetFromLocalX(evt.localPosition.x);
                });
                Root.RegisterCallback<PointerUpEvent>(evt =>
                {
                    _dragging = false;
                    Root.ReleasePointer(evt.pointerId);
                });
                Root.RegisterCallback<PointerCancelEvent>(evt =>
                {
                    _dragging = false;
                    Root.ReleasePointer(evt.pointerId);
                });

                Paint();
            }

            private void SetFromLocalX(float localX)
            {
                var width = Root.resolvedStyle.width;
                if (width <= 0f) return;
                var t = Mathf.Clamp01(localX / width);
                var next = Mathf.Lerp(_min, _max, t);
                if (Mathf.Approximately(next, Value)) return;
                Value = next;
                Paint();
                OnValueChanged?.Invoke(Value);
            }

            private void Paint()
            {
                var t = Mathf.InverseLerp(_min, _max, Value);
                _fill.style.width = new Length(t * 100f, LengthUnit.Percent);
                _knob.style.left = new Length(t * 100f, LengthUnit.Percent);
            }
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
#endif // NOISETEXTURE_UIELEMENTS
