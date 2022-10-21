using ProceduralLandmassGeneration.Generator.Data;
using ProceduralLandmassGeneration.Generator.Generator.NoiseGenerator;
using UnityEngine;

namespace ProceduralLandmassGeneration.Generator {
    public static class HeightMapGenerator {
        public static HeightMap GenerateHeightMap(int width, int height, HeightMapSettings heightMapSettings,
            Vector2 sampleCenter) {
            float[,] values = Noise.GenerateNoiseMap(width, height, heightMapSettings.noiseSettings, sampleCenter);

            // animation curve will get weird glitch if being used in multithreading
            AnimationCurve heightCurveThreadSafe = new AnimationCurve(heightMapSettings.heightCurve.keys);

            float minValue = float.MaxValue;
            float maxValue = float.MinValue;

            for (int i = 0; i < width; i++) {
                for (int j = 0; j < height; j++) {
                    values[i, j] *= heightCurveThreadSafe.Evaluate(values[i, j]) * heightMapSettings.heightMultiplier;

                    if (values[i, j] > maxValue) {
                        maxValue = values[i, j];
                    }

                    if (values[i, j] < minValue) {
                        minValue = values[i, j];
                    }
                }
            }

            return new HeightMap(values, minValue, maxValue);
        }
    }

    public struct HeightMap {
        public readonly float[,] Values;
        public readonly float MinValue;
        public readonly float MaxValue;

        public HeightMap(float[,] values, float minValue, float maxValue) {
            Values = values;
            MaxValue = maxValue;
            MinValue = minValue;
        }
    }
}