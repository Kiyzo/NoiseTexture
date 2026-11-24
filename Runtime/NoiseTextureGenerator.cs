using System;
using System.Collections.Generic;
using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;
using Object = UnityEngine.Object;
using Random = UnityEngine.Random;

namespace NoiseTexture
{
    public enum NoiseType
    {
        Random,
        Perlin,
        Voronoi
    }

    [Serializable]
    public struct NoiseSettings
    {
        public NoiseType noiseType;
        [Range(0, 1)] public float weight;
        public float scale;
        public int voronoiPoints;
        public Color noiseColor;
        public Vector2 offset;
    }

    [BurstCompile]
    public struct NoiseSettingsBurst
    {
        public NoiseType NoiseType;
        public float Weight;
        public float Scale;
        public Color NoiseColor;
        public float2 Offset;
        public int VoronoiStartIndex;
        public int VoronoiPointCount;
    }

    [BurstCompile]
    public struct NoiseCalculationJob : IJobParallelFor
    {
        [WriteOnly] public NativeArray<Color32> Pixels;
        [ReadOnly] public NativeArray<NoiseSettingsBurst> NoiseSettings;
        [ReadOnly] public NativeArray<float2> VoronoiPoints;
        public float2 Resolution;
        public float Time;

        public void Execute(int index)
        {
            var x = index % (int)Resolution.x;
            var y = index / (int)Resolution.x;

            var finalColor = Color.clear;
            var totalWeight = 0f;

            foreach (var settings in NoiseSettings)
            {
                var noiseValue = CalculateNoise(x, y, settings);
                var colorContribution = settings.NoiseColor * noiseValue * settings.Weight;
                finalColor += colorContribution;
                totalWeight += settings.Weight;
            }

            if (totalWeight > 0) finalColor /= totalWeight;

            Pixels[index] = finalColor;
        }

        private float CalculateNoise(int x, int y, NoiseSettingsBurst settings)
        {
            var scaledX = x / Resolution.x * settings.Scale + settings.Offset.x;
            var scaledY = y / Resolution.y * settings.Scale + settings.Offset.y;

            switch (settings.NoiseType)
            {
                case NoiseType.Random:
                    return Hash(scaledX, scaledY, Time);

                case NoiseType.Perlin:
                    // Time drives both a slow XY drift and the third noise dimension.
                    // A small factor alone looked frozen at interactive frame rates.
                    return noise.cnoise(new float3(scaledX + Time * 0.15f, scaledY + Time * 0.1f,
                        Time * 0.9f)) * 0.5f + 0.5f;

                case NoiseType.Voronoi:
                    // Use the same scaled space as the other layers so scale and offset work.
                    return CalculateVoronoi(scaledX, scaledY, settings);

                default: return 0f;
            }
        }

        private float CalculateVoronoi(float scaledX, float scaledY, NoiseSettingsBurst settings)
        {
            var count = settings.VoronoiPointCount;
            if (count <= 0) return 0f;

            // Points live in a normalized [0,1) tile. The query repeats that tile Scale times
            // across the texture, so Scale changes the number of visible cells (like the other layers).
            var qx = math.frac(scaledX);
            var qy = math.frac(scaledY);
            var spacing = 1f / math.sqrt(count);
            var drift = spacing * 0.35f;

            var minDistance = float.MaxValue;
            var start = settings.VoronoiStartIndex;
            var end = start + count;

            for (var i = start; i < end; i++)
            {
                var point = VoronoiPoints[i];
                var px = point.x + math.sin(Time * 0.7f + i) * drift;
                var py = point.y + math.cos(Time * 0.5f + i * 1.3f) * drift;

                // Wrapped distance keeps tile seams invisible.
                var dx = qx - px;
                var dy = qy - py;
                dx -= math.round(dx);
                dy -= math.round(dy);
                minDistance = math.min(minDistance, dx * dx + dy * dy);
            }

            return math.saturate(1f - math.sqrt(minDistance) / (spacing * 1.5f));
        }

        private float Hash(float a, float b, float c)
        {
            return math.frac(math.sin(math.dot(new float3(a, b, c), new float3(12.9898f, 78.233f, 45.164f))) *
                             43758.5453f);
        }
    }

    /// <summary>
    /// UI-independent noise texture generator. Owns the Texture2D and all native buffers;
    /// feed <see cref="Texture"/> to any rendering path (UI Toolkit, IMGUI, raw texture).
    /// </summary>
    public class NoiseTextureGenerator : IDisposable
    {
        private const int Width = 256;
        private const int Height = 256;
        private const float TransitionDuration = 2f;

        private readonly Texture2D _noiseTexture;

        private readonly List<NoiseSettings> _originalSettings;

        // Point counts are constant after construction: the native point buffer cannot grow.
        private readonly int[] _voronoiPointCounts;

        private NativeArray<NoiseSettingsBurst> _currentSettings;

        private bool _disposed;
        private bool _isTransitioning;

        private float _offset;
        private NativeArray<Color32> _pixelsBuffer;
        private NativeArray<NoiseSettingsBurst> _targetSettings;
        private float _transitionTimer;
        private NativeArray<float2> _voronoiPoints;

        /// <summary>The generated RGBA32 texture. Do not destroy it — the generator owns it.</summary>
        public Texture2D Texture => _noiseTexture;

        public NoiseTextureGenerator(List<NoiseSettings> noiseSettings)
        {
            _noiseTexture = new Texture2D(Width, Height, TextureFormat.RGBA32, false);
            _originalSettings = new List<NoiseSettings>(noiseSettings);
            _voronoiPointCounts = new int[_originalSettings.Count];
            for (var i = 0; i < _voronoiPointCounts.Length; i++)
                if (_originalSettings[i].noiseType == NoiseType.Voronoi)
                    _voronoiPointCounts[i] = Mathf.Max(0, _originalSettings[i].voronoiPoints);

            InitializeNativeArrays();
            UpdateSettingsImmediate(noiseSettings);
            UpdateNoiseTexture();
        }

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        ~NoiseTextureGenerator()
        {
            Dispose(false);
        }

        protected virtual void Dispose(bool disposing)
        {
            if (_disposed) return;

            if (_currentSettings.IsCreated) _currentSettings.Dispose();
            if (_targetSettings.IsCreated) _targetSettings.Dispose();
            if (_voronoiPoints.IsCreated) _voronoiPoints.Dispose();
            if (_pixelsBuffer.IsCreated) _pixelsBuffer.Dispose();

            if (disposing && _noiseTexture != null)
            {
#if UNITY_EDITOR
                if (!Application.isPlaying) Object.DestroyImmediate(_noiseTexture);
                else Object.Destroy(_noiseTexture);
#else
                Object.Destroy(_noiseTexture);
#endif
            }

            _disposed = true;
        }

        public void SmoothChangeSettings(List<NoiseSettings> newSettings)
        {
            UpdateNativeSettings(newSettings, _targetSettings, _voronoiPointCounts);
            _transitionTimer = 0f;
            _isTransitioning = true;
        }

        public void Update(float deltaTime)
        {
            if (_disposed) throw new ObjectDisposedException(nameof(NoiseTextureGenerator));

            if (_isTransitioning)
            {
                _transitionTimer += deltaTime;
                var t = math.smoothstep(0f, 1f, _transitionTimer / TransitionDuration);
                InterpolateSettings(t);

                if (_transitionTimer >= TransitionDuration)
                    _isTransitioning = false;
            }

            UpdateNoiseTexture();
        }

        private void InitializeNativeArrays()
        {
            _currentSettings = new NativeArray<NoiseSettingsBurst>(_originalSettings.Count, Allocator.Persistent);
            _targetSettings = new NativeArray<NoiseSettingsBurst>(_originalSettings.Count, Allocator.Persistent);
            _pixelsBuffer = new NativeArray<Color32>(Width * Height, Allocator.Persistent);
            _voronoiPoints = new NativeArray<float2>(CalculateVoronoiPointsCount(), Allocator.Persistent);
        }

        private int CalculateVoronoiPointsCount()
        {
            var total = 0;
            foreach (var count in _voronoiPointCounts) total += count;
            return total;
        }

        private void UpdateSettingsImmediate(List<NoiseSettings> settings)
        {
            UpdateNativeSettings(settings, _currentSettings, _voronoiPointCounts);
            GenerateVoronoiPoints();
        }

        private static void UpdateNativeSettings(List<NoiseSettings> source,
            NativeArray<NoiseSettingsBurst> target, int[] pointCounts)
        {
            var voronoiIndex = 0;
            for (var i = 0; i < source.Count && i < target.Length; i++)
            {
                var setting = source[i];
                var pointCount = pointCounts[i];
                target[i] = new NoiseSettingsBurst
                {
                    NoiseType = setting.noiseType,
                    Weight = setting.weight,
                    Scale = setting.scale,
                    NoiseColor = setting.noiseColor,
                    Offset = setting.offset,
                    VoronoiStartIndex = voronoiIndex,
                    VoronoiPointCount = setting.noiseType == NoiseType.Voronoi ? pointCount : 0
                };
                voronoiIndex += pointCount;
            }
        }

        private void GenerateVoronoiPoints()
        {
            var index = 0;
            for (var layer = 0; layer < _voronoiPointCounts.Length; layer++)
            {
                // Normalized positions; Scale and Offset are applied in the job every frame.
                for (var i = 0; i < _voronoiPointCounts[layer]; i++)
                    _voronoiPoints[index++] = new float2(Random.value, Random.value);
            }
        }

        private void InterpolateSettings(float t)
        {
            if (_targetSettings.Length == 0) return;

            for (var i = 0; i < _currentSettings.Length; i++)
            {
                var current = _currentSettings[i];
                var target = _targetSettings[Mathf.Min(i, _targetSettings.Length - 1)];

                current.Weight = math.lerp(current.Weight, target.Weight, t);
                current.Scale = math.lerp(current.Scale, target.Scale, t);
                current.Offset = math.lerp(current.Offset, target.Offset, t);
                current.NoiseColor = Color.Lerp(current.NoiseColor, target.NoiseColor, t);

                _currentSettings[i] = current;
            }
        }

        private void UpdateNoiseTexture()
        {
            var job = new NoiseCalculationJob
            {
                Pixels = _pixelsBuffer,
                NoiseSettings = _currentSettings,
                VoronoiPoints = _voronoiPoints,
                Resolution = new float2(Width, Height),
                Time = Time.unscaledTime
            };

            var handle = job.Schedule(Width * Height, 64);
            handle.Complete();

            _noiseTexture.LoadRawTextureData(_pixelsBuffer);
            _noiseTexture.Apply();
        }
    }
}
