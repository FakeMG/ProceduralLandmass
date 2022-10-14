using UnityEngine;

namespace ProceduralLandmassGeneration.Generator.Generator.NoiseGenerator {
    public static class Noise {
        public enum NormalizeMode {
            Local,
            Global
        };

        public static float[,] GenerateNoiseMap(int mapWidth, int mapHeight, NoiseSettings noiseSettings,
            Vector2 sampleCenter) {
            float[,] noiseMap = new float[mapWidth, mapHeight];

            // offset
            System.Random prng = new System.Random(noiseSettings.seed);
            Vector2[] octaveOffsets = new Vector2[noiseSettings.octaves];

            float maxPossibleHeight = 0;
            float amplitude = 1;
            float frequency;

            for (int i = 0; i < noiseSettings.octaves; i++) {
                float offsetX = prng.Next(-100000, 100000) + noiseSettings.offset.x + sampleCenter.x;
                float offsetY = prng.Next(-100000, 100000) - noiseSettings.offset.y - sampleCenter.y;
                octaveOffsets[i] = new Vector2(offsetX, offsetY);

                // estimate maxPossibleHeight
                // perlinValue = 1
                maxPossibleHeight += amplitude;
                amplitude *= noiseSettings.persistance;
            }

            float maxLocalNoiseHeight = float.MinValue;
            float minLocalNoiseHeight = float.MaxValue;

            float halfWidth = mapWidth / 2f;
            float halfHeight = mapHeight / 2f;


            for (int y = 0; y < mapHeight; y++) {
                for (int x = 0; x < mapWidth; x++) {
                    amplitude = 1;
                    frequency = 1;
                    float noiseHeight = 0;

                    for (int i = 0; i < noiseSettings.octaves; i++) {
                        float sampleX = (x - halfWidth + octaveOffsets[i].x) / noiseSettings.scale * frequency;
                        float sampleY = (y - halfHeight + octaveOffsets[i].y) / noiseSettings.scale * frequency;

                        // *2-1 to have interesting noise
                        float perlinValue = Mathf.PerlinNoise(sampleX, sampleY) * 2 - 1;
                        noiseHeight += perlinValue * amplitude;

                        amplitude *= noiseSettings.persistance;
                        frequency *= noiseSettings.lacunarity;
                    }

                    // find min max height
                    if (noiseHeight > maxLocalNoiseHeight) {
                        maxLocalNoiseHeight = noiseHeight;
                    }

                    if (noiseHeight < minLocalNoiseHeight) {
                        minLocalNoiseHeight = noiseHeight;
                    }

                    noiseMap[x, y] = noiseHeight;

                    if (noiseSettings.normalizeMode == NormalizeMode.Global) {
                        float normalizedHeight = (noiseMap[x, y] + 1) / (maxPossibleHeight / 0.9f);
                        noiseMap[x, y] = Mathf.Clamp(normalizedHeight, 0, int.MaxValue);
                    }
                }
            }

            // normalize noiseMap
            if (noiseSettings.normalizeMode == NormalizeMode.Local) {
                for (int y = 0; y < mapHeight; y++) {
                    for (int x = 0; x < mapWidth; x++) {
                        noiseMap[x, y] = Mathf.InverseLerp(minLocalNoiseHeight, maxLocalNoiseHeight, noiseMap[x, y]);
                    }
                }
            }

            return noiseMap;
        }
    }

    [System.Serializable]
    public class NoiseSettings {
        public Noise.NormalizeMode normalizeMode;
        public float scale = 50;
        public Vector2 offset;

        public int seed;
        [Space] public int octaves = 6;
        [Range(0, 1)] public float persistance = 0.6f;
        public float lacunarity = 2f;

        public void ValidateValues() {
            scale = Mathf.Max(scale, 0.01f);
            octaves = Mathf.Max(octaves, 1);
            lacunarity = Mathf.Max(lacunarity, 1);
            persistance = Mathf.Clamp01(persistance);
        }
    }
}