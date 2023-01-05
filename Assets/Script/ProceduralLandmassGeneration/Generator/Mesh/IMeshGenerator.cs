namespace ProceduralLandmassGeneration.Generator.Mesh {
    public interface IMeshGenerator {
        public IMeshData GenerateMeshData(float[,] heightMap);
    }
}