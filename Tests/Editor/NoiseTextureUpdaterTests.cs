using System;
using System.Collections.Generic;
using NoiseTexture;
using NUnit.Framework;
using Unity.Collections;
using Unity.Mathematics;
using UnityEngine;

namespace NoiseTexture.Tests
{
    /// <summary>
    /// Package contracts without the Unity loop: the job runs directly via Execute,
    /// so these tests stay stable in EditMode and CI.
    /// </summary>
    public sealed class NoiseTextureUpdaterTests
    {
        [Test]
        public void NoiseJob_FillsEveryPixel_AndUsesLayerColor()
        {
            const int width = 64;
            const int height = 64;

            using var pixels = new NativeArray<Color32>(width * height, Allocator.TempJob);
            using var settings = CreateSingleRandomLayer();
            using var points = new NativeArray<float2>(0, Allocator.TempJob);

            var job = new NoiseCalculationJob
            {
                Pixels = pixels,
                NoiseSettings = settings,
                VoronoiPoints = points,
                Resolution = new float2(width, height),
                Time = 0.37f
            };

            job.Execute(3 + width * 11);

            Assert.AreNotEqual(new Color32(0, 0, 0, 0), pixels[3 + width * 11],
                "A Random layer with weight 1 and white colour must produce a visible pixel");
            Assert.AreEqual(width * height, pixels.Length);
        }

        private static NativeArray<NoiseSettingsBurst> CreateSingleRandomLayer()
        {
            var settings = new NativeArray<NoiseSettingsBurst>(1, Allocator.TempJob);
            settings[0] = new NoiseSettingsBurst
            {
                NoiseType = NoiseType.Random,
                Weight = 1f,
                Scale = 1f,
                NoiseColor = Color.white,
                Offset = float2.zero,
                VoronoiStartIndex = 0,
                VoronoiPointCount = 0
            };
            return settings;
        }

        [Test]
        public void Generator_ProducesTexture_AndThrowsAfterDispose()
        {
            var noise = new NoiseTextureGenerator(new List<NoiseSettings>
            {
                new() { noiseType = NoiseType.Random, weight = 1f, scale = 4f, noiseColor = Color.white }
            });

            Assert.IsNotNull(noise.Texture);
            Assert.AreEqual(256, noise.Texture.width);
            Assert.AreEqual(256, noise.Texture.height);

            noise.Update(0.016f);
            noise.Dispose();
            Assert.Throws<ObjectDisposedException>(() => noise.Update(0.016f));
        }

        [Test]
        public void PerlinAndVoronoi_AreAnimated_AndVoronoiIsVisible()
        {
            const int width = 64;
            const int height = 64;
            const int index = 0; // Coordinates (0,0), next to the first Voronoi point.

            using var voronoiSettings = CreateLayer(NoiseType.Voronoi, 12);
            using var scaledSettings = CreateLayer(NoiseType.Voronoi, 12, 3f);
            using var points = CreateVoronoiPoints(12);

            using var voronoiA = new NativeArray<Color32>(width * height, Allocator.TempJob);
            using var voronoiB = new NativeArray<Color32>(width * height, Allocator.TempJob);
            RunJob(voronoiA, voronoiSettings, points, width, height, 0f, index);
            RunJob(voronoiB, voronoiSettings, points, width, height, 6f, index);

            Assert.Greater((int)voronoiA[index].a, 16, "Voronoi cells must be visible, not almost black");
            Assert.AreNotEqual(voronoiA[index], voronoiB[index], "Voronoi must change over time");

            const int scaleIndex = 3326; // Strongest scale-dependent pixel for this fixture.
            RunJob(voronoiB, scaledSettings, points, width, height, 0f, scaleIndex);
            RunJob(voronoiA, voronoiSettings, points, width, height, 0f, scaleIndex);
            Assert.AreNotEqual(voronoiA[scaleIndex], voronoiB[scaleIndex],
                "Voronoi must react to Scale like the other layers");

            using var perlinSettings = CreateLayer(NoiseType.Perlin, 0);

            using var perlinA = new NativeArray<Color32>(width * height, Allocator.TempJob);
            using var perlinB = new NativeArray<Color32>(width * height, Allocator.TempJob);
            RunJob(perlinA, perlinSettings, points, width, height, 0f, index);
            RunJob(perlinB, perlinSettings, points, width, height, 1.5f, index);
            Assert.AreNotEqual(perlinA[index], perlinB[index], "Perlin must change over time");
        }

        private static NativeArray<NoiseSettingsBurst> CreateLayer(NoiseType type, int pointCount, float scale = 1f)
        {
            var settings = new NativeArray<NoiseSettingsBurst>(1, Allocator.TempJob);
            settings[0] = new NoiseSettingsBurst
            {
                NoiseType = type,
                Weight = 1f,
                Scale = scale,
                NoiseColor = Color.white,
                Offset = float2.zero,
                VoronoiStartIndex = 0,
                VoronoiPointCount = pointCount
            };
            return settings;
        }

        private static NativeArray<float2> CreateVoronoiPoints(int count)
        {
            var points = new NativeArray<float2>(count, Allocator.TempJob);
            for (var i = 0; i < count; i++)
                points[i] = new float2(i * 0.37f % 1f, i * 0.53f % 1f);
            return points;
        }

        private static void RunJob(NativeArray<Color32> pixels, NativeArray<NoiseSettingsBurst> settings,
            NativeArray<float2> points, int width, int height, float time, int index)
        {
            var job = new NoiseCalculationJob
            {
                Pixels = pixels,
                NoiseSettings = settings,
                VoronoiPoints = points,
                Resolution = new float2(width, height),
                Time = time
            };
            job.Execute(index);
        }

        [Test]
        public void NoiseSettings_ExposeEveryLayerKnob()
        {
            var layers = new List<NoiseSettings>
            {
                new()
                {
                    noiseType = NoiseType.Voronoi,
                    weight = 0.4f,
                    scale = 9f,
                    voronoiPoints = 20,
                    noiseColor = new Color(0.5f, 0f, 0f, 1f),
                    offset = new Vector2(3f, 7f)
                }
            };

            Assert.AreEqual(NoiseType.Voronoi, layers[0].noiseType);
            Assert.AreEqual(20, layers[0].voronoiPoints);
            Assert.AreEqual(new Vector2(3f, 7f), layers[0].offset);
        }
    }
}
