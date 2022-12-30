using ProceduralLandmassGeneration.Data;

namespace ProceduralLandmassGeneration.Generator.Mesh {
    public interface IMeshGenerator {
        public MeshData GenerateMeshData(float[,] heightMap, MeshSettings meshSettings, int levelOfDetail);
    }
}