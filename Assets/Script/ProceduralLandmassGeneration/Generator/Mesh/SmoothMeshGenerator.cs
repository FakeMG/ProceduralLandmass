using ProceduralLandmassGeneration.Data;
using UnityEngine;

namespace ProceduralLandmassGeneration.Generator.Mesh {
    public static class MeshGenerator {
        public static MeshData GenerateMeshData(float[,] heightMap, MeshSettings meshSettings, int levelOfDetail) {
            int skipIncrement = (levelOfDetail == 0) ? 1 : levelOfDetail * 2;
            int numVertsPerLine = meshSettings.NumVertsPerLine;
            Vector2 topLeftOfMesh = new Vector2(-1, 1) * meshSettings.MeshWorldSize / 2;

            MeshData meshData = new MeshData(numVertsPerLine, skipIncrement, meshSettings.useFlatShading);
            
            int[,] vertexIndicesMap = new int[numVertsPerLine, numVertsPerLine];
            int meshVertexIndex = 0;
            int outOfMeshVertexIndex = -1;

            SetupVertexIndicesMap();

            CalculateMeshData();

            meshData.ProcessMesh();

            // return meshData because unity API can only work in main thread
            return meshData;

            void SetupVertexIndicesMap() {
                for (int y = 0; y < numVertsPerLine; y++) {
                    for (int x = 0; x < numVertsPerLine; x++) {
                        bool isOutOfMeshVertex =
                            y == 0 || y == numVertsPerLine - 1 || x == 0 || x == numVertsPerLine - 1;
                        
                        //(x-2); (y-2) because there are 2 extra verts that are excluded from final mesh
                        bool isSkippedVertex = x > 2 && x < numVertsPerLine - 3 && y > 2 && y < numVertsPerLine - 3 &&
                                               ((x - 2) % skipIncrement != 0 || (y - 2) % skipIncrement != 0);

                        if (isOutOfMeshVertex) {
                            vertexIndicesMap[x, y] = outOfMeshVertexIndex;
                            outOfMeshVertexIndex--;
                        } else if (!isSkippedVertex) {
                            vertexIndicesMap[x, y] = meshVertexIndex;
                            meshVertexIndex++;
                        }
                    }
                }
            }

            void CalculateMeshData() {
                bool isMainVertex;
                bool isEdgeConnectionVertex;

                for (int y = 0; y < numVertsPerLine; y++) {
                    for (int x = 0; x < numVertsPerLine; x++) {
                        // x,y > 2 because the edge of chunk is render at full resolution for every chunks
                        //(x-2); (y-2) because there are 2 extra verts that are excluded from final mesh
                        bool isSkippedVertex = x > 2 && x < numVertsPerLine - 3 && y > 2 && y < numVertsPerLine - 3 &&
                                               ((x - 2) % skipIncrement != 0 || (y - 2) % skipIncrement != 0);

                        if (!isSkippedVertex) {
                            bool isOutOfMeshVertex =
                                y == 0 || y == numVertsPerLine - 1 || x == 0 || x == numVertsPerLine - 1;
                            bool isMeshEdgeVertex =
                                (y == 1 || y == numVertsPerLine - 2 || x == 1 || x == numVertsPerLine - 2) &&
                                !isOutOfMeshVertex;
                            isMainVertex = (x - 2) % skipIncrement == 0 && (y - 2) % skipIncrement == 0 &&
                                           !isOutOfMeshVertex && !isMeshEdgeVertex;
                            isEdgeConnectionVertex =
                                (y == 2 || y == numVertsPerLine - 3 || x == 2 || x == numVertsPerLine - 3) &&
                                !isOutOfMeshVertex && !isMeshEdgeVertex && !isMainVertex;

                            CalculateVerticesPos(x, y);

                            CalculateTrianglesIndex(x, y);
                        }
                    }
                }

                //Vertices position is local
                void CalculateVerticesPos(int x, int y) {
                    int vertexIndex = vertexIndicesMap[x, y];
                    Vector2 percent = new Vector2(x - 1, y - 1) / (numVertsPerLine - 3);
                    Vector2 vertexPosition2D =
                        topLeftOfMesh + new Vector2(percent.x, -percent.y) * meshSettings.MeshWorldSize;
                    float height = heightMap[x, y];

                    // calculate height for edgeConnectionVertex by distance percent from a to b
                    if (isEdgeConnectionVertex) {
                        bool isVertical = x == 2 || x == numVertsPerLine - 3;
                        int dstToMainVertexA = ((isVertical) ? y - 2 : x - 2) % skipIncrement; // up/left
                        int dstToMainVertexB = skipIncrement - dstToMainVertexA; // bot/right
                        float dstPercentFromAToB = dstToMainVertexA / (float)skipIncrement;

                        float heightMainVertexA = heightMap[(isVertical) ? x : x - dstToMainVertexA,
                            (isVertical) ? y - dstToMainVertexA : y];
                        float heightMainVertexB = heightMap[(isVertical) ? x : x + dstToMainVertexB,
                            (isVertical) ? y + dstToMainVertexB : y];

                        height = heightMainVertexA * (1 - dstPercentFromAToB) +
                                 heightMainVertexB * dstPercentFromAToB;
                    }

                    meshData.AddVertex(new Vector3(vertexPosition2D.x, height, vertexPosition2D.y), percent,
                        vertexIndex);
                }

                void CalculateTrianglesIndex(int x, int y) {
                    bool canCreateTriangle = x < numVertsPerLine - 1 && y < numVertsPerLine - 1 &&
                                             (!isEdgeConnectionVertex || (x != 2 && y != 2));
                    if (canCreateTriangle) {
                        int currentIncrement =
                            (isMainVertex && x != numVertsPerLine - 3 && y != numVertsPerLine - 3)
                                ? skipIncrement
                                : 1;

                        int a = vertexIndicesMap[x, y];
                        int b = vertexIndicesMap[x + currentIncrement, y];
                        int c = vertexIndicesMap[x, y + currentIncrement];
                        int d = vertexIndicesMap[x + currentIncrement, y + currentIncrement];
                        meshData.AddTriangle(a, d, c);
                        meshData.AddTriangle(d, a, b);
                    }
                }
            }
        }
    }
}