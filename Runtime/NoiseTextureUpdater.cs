#if NOISETEXTURE_UIELEMENTS
using System;
using System.Collections.Generic;
using UnityEngine.UIElements;

namespace NoiseTexture
{
    /// <summary>
    /// UI Toolkit adapter over <see cref="NoiseTextureGenerator"/>. Compiled only when the
    /// <c>com.unity.modules.uielements</c> module is available (symbol <c>NOISETEXTURE_UIELEMENTS</c>).
    /// Without UI Toolkit use <see cref="NoiseTextureGenerator.Texture"/> directly.
    /// </summary>
    public class NoiseTextureUpdater : IDisposable
    {
        private readonly NoiseTextureGenerator _generator;
        private readonly VisualElement _rootElement;
        private bool _disposed;

        public NoiseTextureUpdater(VisualElement element, List<NoiseSettings> noiseSettings)
        {
            _rootElement = element;
            _generator = new NoiseTextureGenerator(noiseSettings);
        }

        public void SmoothChangeSettings(List<NoiseSettings> newSettings) =>
            _generator.SmoothChangeSettings(newSettings);

        public void Update(float deltaTime)
        {
            if (_disposed) throw new ObjectDisposedException(nameof(NoiseTextureUpdater));

            _generator.Update(deltaTime);
            _rootElement.style.backgroundImage = new StyleBackground(_generator.Texture);
        }

        public void Dispose()
        {
            if (_disposed) return;
            _generator.Dispose();
            _disposed = true;
        }
    }
}
#endif
