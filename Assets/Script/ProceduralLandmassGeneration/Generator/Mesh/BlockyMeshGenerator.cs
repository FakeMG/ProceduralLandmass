using System.Collections.Generic;
using ProceduralLandmassGeneration.Data;
using UnityEngine;

namespace ProceduralLandmassGeneration.Generator.Mesh {
    public class BlockyMeshGenerator : IMeshGenerator {
    private const int CHUNK_HEIGHT = 70;
    private const int CHUNK_WIDTH = 16;
    private readonly int[,,] _blocks = new int[CHUNK_WIDTH, CHUNK_HEIGHT, CHUNK_WIDTH];
    private const int BASE_LAND_LEVEL = CHUNK_HEIGHT / 2;

    private void PopulateBlocksData(float[,] heightMap) {
        for (int x = 0; x < CHUNK_WIDTH; x++) {
            for (int y = 0; y < CHUNK_HEIGHT; y++) {
                for (int z = 0; z < CHUNK_WIDTH; z++) {
                    if (y <= BASE_LAND_LEVEL + heightMap[x, z]) {
                        _blocks[x, y, z] = 1;
                    } else {
                        _blocks[x, y, z] = 0;
                    }
                }
            }
        }
    }

    private bool IsTransparent(int blockType) {
        return blockType == 0;
    }

    public MeshData GenerateMeshData(float[,] heightMap, MeshSettings meshSettings, int levelOfDetail = 0) {
        //gốc toạ độ của block nằm ở mặt dưới
        Vector3 bottomLeftOfMesh =
            new Vector3(-1 * CHUNK_WIDTH / 2, 0, -1 * CHUNK_WIDTH / 2);

        PopulateBlocksData(heightMap);

        List<Vector3> verts = new List<Vector3>();
        List<int> tris = new List<int>();
        List<Vector2> uvs = new List<Vector2>();

        for (int x = 0; x < CHUNK_WIDTH; x++) {
            for (int y = 0; y < CHUNK_HEIGHT; y++) {
                for (int z = 0; z < CHUNK_WIDTH; z++) {
                    var currentBlockType = _blocks[x, y, z];

                    if (!IsTransparent(currentBlockType)) {
                        int numFaces = 0;

                        //no land above, build top face
                        if (y < CHUNK_HEIGHT - 1 && IsTransparent(_blocks[x, y + 1, z])) {
                            verts.Add(bottomLeftOfMesh + new Vector3(x, y + 1, z));
                            verts.Add(bottomLeftOfMesh + new Vector3(x, y + 1, z + 1));
                            verts.Add(bottomLeftOfMesh + new Vector3(x + 1, y + 1, z + 1));
                            verts.Add(bottomLeftOfMesh + new Vector3(x + 1, y + 1, z));
                            numFaces++;
                        }

                        //bottom
                        if (y > 0 && IsTransparent(_blocks[x, y - 1, z])) {
                            verts.Add(bottomLeftOfMesh + new Vector3(x, y, z));
                            verts.Add(bottomLeftOfMesh + new Vector3(x + 1, y, z));
                            verts.Add(bottomLeftOfMesh + new Vector3(x + 1, y, z + 1));
                            verts.Add(bottomLeftOfMesh + new Vector3(x, y, z + 1));
                            numFaces++;
                        }

                        //front
                        if (z > 0 && IsTransparent(_blocks[x, y, z - 1])) {
                            verts.Add(bottomLeftOfMesh + new Vector3(x, y, z));
                            verts.Add(bottomLeftOfMesh + new Vector3(x, y + 1, z));
                            verts.Add(bottomLeftOfMesh + new Vector3(x + 1, y + 1, z));
                            verts.Add(bottomLeftOfMesh + new Vector3(x + 1, y, z));
                            numFaces++;
                        }

                        //right
                        if (x < CHUNK_WIDTH - 1 && IsTransparent(_blocks[x + 1, y, z])) {
                            verts.Add(bottomLeftOfMesh + new Vector3(x + 1, y, z));
                            verts.Add(bottomLeftOfMesh + new Vector3(x + 1, y + 1, z));
                            verts.Add(bottomLeftOfMesh + new Vector3(x + 1, y + 1, z + 1));
                            verts.Add(bottomLeftOfMesh + new Vector3(x + 1, y, z + 1));
                            numFaces++;
                        }

                        //back
                        if (z < CHUNK_WIDTH - 1 && IsTransparent(_blocks[x, y, z + 1])) {
                            verts.Add(bottomLeftOfMesh + new Vector3(x + 1, y, z + 1));
                            verts.Add(bottomLeftOfMesh + new Vector3(x + 1, y + 1, z + 1));
                            verts.Add(bottomLeftOfMesh + new Vector3(x, y + 1, z + 1));
                            verts.Add(bottomLeftOfMesh + new Vector3(x, y, z + 1));
                            numFaces++;
                        }

                        //left
                        if (x > 0 && IsTransparent(_blocks[x - 1, y, z])) {
                            verts.Add(bottomLeftOfMesh + new Vector3(x, y, z + 1));
                            verts.Add(bottomLeftOfMesh + new Vector3(x, y + 1, z + 1));
                            verts.Add(bottomLeftOfMesh + new Vector3(x, y + 1, z));
                            verts.Add(bottomLeftOfMesh + new Vector3(x, y, z));
                            numFaces++;
                        }

                        int tl = verts.Count - 4 * numFaces;
                        for (int i = 0; i < numFaces; i++) {
                            tris.AddRange(new[] {
                                tl + i * 4, tl + i * 4 + 1, tl + i * 4 + 2, tl + i * 4, tl + i * 4 + 2,
                                tl + i * 4 + 3
                            });
                        }
                    }
                }
            }
        }

        MeshData meshData = new MeshData(verts, tris);
        return meshData;
    }
    }
}