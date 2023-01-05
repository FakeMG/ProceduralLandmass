using ProceduralLandmassGeneration.Generator.Noise;
using UnityEngine;

namespace ProceduralLandmassGeneration.Data {
    [CreateAssetMenu()]
    public class HeightMapSettings : UpdatableData {
        public NoiseSettings noiseSettings;

        public float heightMultiplier;
        public AnimationCurve heightCurve;
        public bool useFalloff;

        public float minHeight {
            get { return heightMultiplier * heightCurve.Evaluate(0); }
        }

        public float maxHeight {
            get { return heightMultiplier * heightCurve.Evaluate(1); }
        }

#if UNITY_EDITOR
        protected override void OnValidate() {
            noiseSettings.ValidateValues();

            base.OnValidate();
        }
    }
#endif
}