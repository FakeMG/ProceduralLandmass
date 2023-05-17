using System.Collections.Generic;
using ProceduralLandmassGeneration.Data;
using ProceduralLandmassGeneration.Generator.Noise;
using UnityEngine;

namespace ProceduralLandmassGeneration.Generator {
    public class PerlinWorm {
        public HeightMapSettings HeightMapSettings;
        public int Steps = 3;

        public float Speed = 0.4f;
        public float LateralSpeed = (2.0f / 8192.0f);
        public int Radius = 3;
        public int Length = 150;

        private Vector3 _headNoisePos;
        private Vector3 _headScreenPos;

        public PerlinWorm(Vector3 headNoisePos, Vector3 headScreenPos, HeightMapSettings heightMapSettings) {
            HeightMapSettings = heightMapSettings;
            _headNoisePos = headNoisePos;
            _headScreenPos = headScreenPos;
        }
        

        public List<Vector3> GenerateWorm() {
            List<Vector3> wormBlocks = new List<Vector3>();

            for (int i = 0; i < Length; i++) {
                float noiseValue = NoiseGenerator.GetNoise(HeightMapSettings.noiseSettings,
                    new Vector2(_headNoisePos.x, _headNoisePos.y));
                float noiseValue2 = NoiseGenerator.GetNoise(HeightMapSettings.noiseSettings,
                    new Vector2(-_headNoisePos.x / 2, _headNoisePos.z));

                Vector3 oldHeadScreenPos = _headScreenPos;

                _headScreenPos.x = Mathf.RoundToInt(_headScreenPos.x - (Mathf.Cos(noiseValue * 2f * Mathf.PI) * Steps));
                _headScreenPos.y = Mathf.RoundToInt(_headScreenPos.y - (Mathf.Sin(noiseValue * 2f * Mathf.PI) * Steps));
                _headScreenPos.z =
                    Mathf.RoundToInt(_headScreenPos.z - (Mathf.Sin(noiseValue2 * 2f * Mathf.PI) * Steps));

                _headNoisePos.x -= Speed * 2.0f;
                _headNoisePos.y += LateralSpeed;
                _headNoisePos.z -= LateralSpeed;

                if (_headScreenPos != oldHeadScreenPos) {
                    for (int x = -Radius; x <= Radius; x++) {
                        for (int y = -Radius; y <= Radius; y++) {
                            for (int z = -Radius; z <= Radius; z++) {
                                Vector3 position = _headScreenPos + new Vector3(x, y, z);
                                float distance = Vector3.Distance(position, _headScreenPos);

                                if (noiseValue2 < 1) {
                                    noiseValue2 += 1;
                                } else if (noiseValue2 < 0) {
                                    noiseValue2 += 2;
                                }

                                if (distance < Radius * noiseValue2) {
                                    wormBlocks.Add(position);
                                }
                            }
                        }
                    }
                }
            }

            return wormBlocks;
        }
    }
}