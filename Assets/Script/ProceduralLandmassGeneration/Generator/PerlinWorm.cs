using ProceduralLandmassGeneration.Data;
using ProceduralLandmassGeneration.Generator.Noise;
using UnityEngine;

namespace ProceduralLandmassGeneration.Generator {
    public class PerlinWorm : MonoBehaviour {
        public int size = 20;
        public HeightMapSettings heightMapSettings;
        public GameObject cube;

        private Vector3 _headNoisePos;
        private Vector3 _headScreenPos;
        public float speed = (3.0f / 2048.0f);
        public float lateralSpeed = (2.0f / 8192.0f);

        private void Update() {
            float noiseValue = NoiseGenerator.GetNoise(heightMapSettings.noiseSettings, new Vector2(_headNoisePos.x,_headNoisePos.y));
            float noiseValue2 = NoiseGenerator.GetNoise(heightMapSettings.noiseSettings, new Vector2(-_headNoisePos.x/2,_headNoisePos.z));
            _headScreenPos.x -= (Mathf.Cos(noiseValue * 1f * Mathf.PI) * speed);
            _headScreenPos.y -= (Mathf.Sin(noiseValue * 1.5f * Mathf.PI) * speed);
            _headScreenPos.z -= (Mathf.Sin(noiseValue2 * 1f * Mathf.PI) * speed);
        
            _headNoisePos.x -= speed * 2.0f;
            _headNoisePos.y += lateralSpeed;
            _headNoisePos.z -= lateralSpeed;
        
        
            Instantiate(cube, _headScreenPos, Quaternion.identity);
        }
    }
}