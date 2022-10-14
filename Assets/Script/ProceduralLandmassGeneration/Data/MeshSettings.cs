using UnityEngine;

namespace ProceduralLandmassGeneration.Generator.Data {
    [CreateAssetMenu()]
    public class MeshSettings : UpdatableData {
        public const int numSupportedLODs = 5;
        public const int numSupportedChunkSizes = 9;
        public const int numSupportedFlatshadedChunkSizes = 3;

        public static readonly int[]
            supportedChunkSizes = { 48, 72, 96, 120, 144, 168, 192, 216, 240 }; //space between vertices

        [Range(0, numSupportedChunkSizes - 1)] public int chunkSizeIndex;

        [Range(0, numSupportedFlatshadedChunkSizes - 1)]
        public int flatshadedChunkSizeIndex;

        public float meshScale = 2f;
        public bool useFlatShading;

        // num verts per line of mesh rendered at LOD = 0. Includes the 2 extra verts that are excluded from final mesh, but used for calculating normals
        public int numVertsPerLine {
            get { return supportedChunkSizes[(useFlatShading) ? flatshadedChunkSizeIndex : chunkSizeIndex] + 5; }
        }

        public float meshWorldSize {
            get {
                // minus 1 to get the space between vertices, minus 2 because 2 extra verts are used for calculating normals 
                return (numVertsPerLine - 3) * meshScale;
            }
        }
    }
}