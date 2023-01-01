using UnityEngine;

namespace ProceduralLandmassGeneration.Data {
    [CreateAssetMenu()]
    public class MeshSettings : UpdatableData {
        public const int NUM_SUPPORTED_LODS = 5;
        
        private const int NUM_SUPPORTED_CHUNK_SIZES = 9;
        private const int NUM_SUPPORTED_FLAT_SHADED_CHUNK_SIZES = 3;

        private static readonly int[]
            SupportedChunkSizes = {16, 48, 72, 96, 120, 144, 168, 192, 216, 240 }; //space between vertices

        [Range(0, NUM_SUPPORTED_CHUNK_SIZES - 1)]
        public int chunkSizeIndex;

        [Range(0, NUM_SUPPORTED_FLAT_SHADED_CHUNK_SIZES - 1)]
        public int flatShadedChunkSizeIndex;

        public float meshScale = 1f;
        public bool useFlatShading;

        // num verts per line of mesh rendered at LOD = 0.
        // SupportedChunkSizes is space between vertices.
        // +3 to get the number of vertices, included 2 vertices that are used to calculate normals
        // +2 is for ??, it makes the mesh bigger, 
        public int NumVertsPerLine {
            get { return SupportedChunkSizes[(useFlatShading) ? flatShadedChunkSizeIndex : chunkSizeIndex] + 2 + 3; }
        }

        public float MeshWorldSize {
            get {
                // minus 1 to get the space between vertices, minus 2 because 2 extra verts are used for calculating normals 
                return (NumVertsPerLine - 3) * meshScale;
            }
        }
    }
}