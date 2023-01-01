using System.Collections.Generic;
using ProceduralLandmassGeneration.Data;
using UnityEngine;

namespace ProceduralLandmassGeneration.Generator.Mesh {
    public class BlockyMeshGenerator : IMeshGenerator {
        private int CHUNK_HEIGHT;
        private int CHUNK_WIDTH;
        private int[,,] _blocks;
        private int BASE_LAND_LEVEL;

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
            CHUNK_HEIGHT = 64;
            CHUNK_WIDTH = (int)meshSettings.MeshWorldSize + 2;
            _blocks = new int[CHUNK_WIDTH, CHUNK_HEIGHT, CHUNK_WIDTH];
            BASE_LAND_LEVEL = CHUNK_HEIGHT / 2;

            // cần phải build mesh từ +z -> -z để lấy đối xứng với noiseMap
            Vector3 topLeftOfMesh =
                new Vector3(-1 * (CHUNK_WIDTH - 2) / 2, 0, 1 * (CHUNK_WIDTH - 2) / 2);

            PopulateBlocksData(heightMap);

            List<Vector3> verts = new List<Vector3>();
            List<int> tris = new List<int>();
            List<Vector2> uvs = new List<Vector2>();

            for (int x = 1; x < CHUNK_WIDTH - 1; x++) {
                for (int y = 0; y < CHUNK_HEIGHT; y++) {
                    for (int z = 1; z < CHUNK_WIDTH - 1; z++) {
                        var currentBlockType = _blocks[x, y, z];

                        if (!IsTransparent(currentBlockType)) {
                            int numFaces = 0;

                            //Because there are 2 extra blocks line in chunk width that are used to calculate block face
                            int currentBlockLocalX = x - 1;
                            int currentBlockLocalZ = -(z - 1);

                            //world positive y direction
                            if (y < CHUNK_HEIGHT - 1 && IsTransparent(_blocks[x, y + 1, z])) {
                                verts.Add(topLeftOfMesh +
                                          new Vector3(currentBlockLocalX, y + 1, currentBlockLocalZ));
                                verts.Add(topLeftOfMesh +
                                          new Vector3(currentBlockLocalX + 1, y + 1, currentBlockLocalZ));
                                verts.Add(topLeftOfMesh +
                                          new Vector3(currentBlockLocalX + 1, y + 1, currentBlockLocalZ - 1));
                                verts.Add(topLeftOfMesh +
                                          new Vector3(currentBlockLocalX, y + 1, currentBlockLocalZ - 1));
                                numFaces++;
                            }

                            //world negative y direction
                            if (y > 0 && IsTransparent(_blocks[x, y - 1, z])) {
                                verts.Add(topLeftOfMesh + new Vector3(currentBlockLocalX, y, currentBlockLocalZ));
                                verts.Add(topLeftOfMesh +
                                          new Vector3(currentBlockLocalX + 1, y, currentBlockLocalZ));
                                verts.Add(topLeftOfMesh +
                                          new Vector3(currentBlockLocalX + 1, y, currentBlockLocalZ - 1));
                                verts.Add(topLeftOfMesh +
                                          new Vector3(currentBlockLocalX, y, currentBlockLocalZ - 1));
                                numFaces++;
                            }

                            //world positive z direction
                            if (IsTransparent(_blocks[x, y, z - 1])) {
                                verts.Add(topLeftOfMesh + new Vector3(currentBlockLocalX, y, currentBlockLocalZ));
                                verts.Add(topLeftOfMesh +
                                          new Vector3(currentBlockLocalX + 1, y, currentBlockLocalZ));
                                verts.Add(topLeftOfMesh +
                                          new Vector3(currentBlockLocalX + 1, y + 1, currentBlockLocalZ));
                                verts.Add(topLeftOfMesh +
                                          new Vector3(currentBlockLocalX, y + 1, currentBlockLocalZ));
                                numFaces++;
                            }

                            //world negative z direction
                            if (IsTransparent(_blocks[x, y, z + 1])) {
                                verts.Add(topLeftOfMesh +
                                          new Vector3(currentBlockLocalX, y, currentBlockLocalZ - 1));
                                verts.Add(topLeftOfMesh +
                                          new Vector3(currentBlockLocalX, y + 1, currentBlockLocalZ - 1));
                                verts.Add(topLeftOfMesh +
                                          new Vector3(currentBlockLocalX + 1, y + 1, currentBlockLocalZ - 1));
                                verts.Add(topLeftOfMesh +
                                          new Vector3(currentBlockLocalX + 1, y, currentBlockLocalZ - 1));
                                numFaces++;
                            }

                            //world positive x
                            if (IsTransparent(_blocks[x + 1, y, z])) {
                                verts.Add(topLeftOfMesh +
                                          new Vector3(currentBlockLocalX + 1, y, currentBlockLocalZ));
                                verts.Add(topLeftOfMesh +
                                          new Vector3(currentBlockLocalX + 1, y, currentBlockLocalZ - 1));
                                verts.Add(topLeftOfMesh +
                                          new Vector3(currentBlockLocalX + 1, y + 1, currentBlockLocalZ - 1));
                                verts.Add(topLeftOfMesh +
                                          new Vector3(currentBlockLocalX + 1, y + 1, currentBlockLocalZ));
                                numFaces++;
                            }

                            //world negative x
                            if (IsTransparent(_blocks[x - 1, y, z])) {
                                verts.Add(topLeftOfMesh +
                                          new Vector3(currentBlockLocalX, y, currentBlockLocalZ));
                                verts.Add(topLeftOfMesh +
                                          new Vector3(currentBlockLocalX, y + 1, currentBlockLocalZ));
                                verts.Add(topLeftOfMesh +
                                          new Vector3(currentBlockLocalX, y + 1, currentBlockLocalZ - 1));
                                verts.Add(topLeftOfMesh + new Vector3(currentBlockLocalX, y, currentBlockLocalZ - 1));
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