using System.Collections.Generic;
using ProceduralLandmassGeneration.Data;
using ProceduralLandmassGeneration.Data.Minecraft;
using UnityEngine;

namespace ProceduralLandmassGeneration.Generator.Mesh {
    public class BlockyMeshGenerator : IMeshGenerator {
        private int _chunkHeight;
        private int _chunkWidth;
        private int[,,] _blocks;
        private int _baseLandLevel;

        public MeshData GenerateMeshData(float[,] heightMap, MeshSettings meshSettings, int levelOfDetail = 0) {
            _chunkHeight = 64;
            _chunkWidth = (int)meshSettings.MeshWorldSize + 2;
            _blocks = new int[_chunkWidth, _chunkHeight, _chunkWidth];
            _baseLandLevel = _chunkHeight / 2;

            // cần phải build mesh từ +z -> -z để lấy đối xứng với noiseMap
            Vector3 topLeftOfMesh = new Vector3(-1 * (_chunkWidth - 2) / 2, 0, 1 * (_chunkWidth - 2) / 2);

            PopulateBlocksData(heightMap);

            List<Vector3> verts = new List<Vector3>();
            List<int> tris = new List<int>();
            List<Vector2> uvs = new List<Vector2>();

            for (int x = 1; x < _chunkWidth - 1; x++) {
                for (int y = 0; y < _chunkHeight; y++) {
                    for (int z = 1; z < _chunkWidth - 1; z++) {
                        Vector3 currentBlockIndex = new Vector3(x, y, z);

                        if (!IsTransparent(currentBlockIndex)) {
                            // x,z - 1: because there are 2 extra blocks line in chunk width that are used to calculate block face
                            // -(z-1) để lấy đối xứng với noise map
                            Vector3 currentLocalPos = new Vector3(x - 1, y, -(z - 1));

                            for (int direction = 0; direction < VoxelData.DirectionsAroundBlock.Length; direction++) {
                                if (IsTransparent(currentBlockIndex + VoxelData.DirectionsAroundBlock[direction])) {
                                    for (int index = 0; index < 4; index++) {
                                        verts.Add(topLeftOfMesh +
                                                  currentLocalPos +
                                                  VoxelData.VerticesLocalPos[
                                                      VoxelData.VerticesIndicesOfFace[direction, index]]);
                                    }

                                    int tl = verts.Count - 4;
                                    tris.AddRange(new[] {
                                        tl, tl + 1, tl + 2, tl, tl + 2, tl + 3
                                    });
                                }
                            }
                        }
                    }
                }
            }

            MeshData meshData = new MeshData(verts, tris);
            return meshData;
        }

        private void PopulateBlocksData(float[,] heightMap) {
            for (int x = 0; x < _chunkWidth; x++) {
                for (int y = 0; y < _chunkHeight; y++) {
                    for (int z = 0; z < _chunkWidth; z++) {
                        if (y <= _baseLandLevel + heightMap[x, z]) {
                            _blocks[x, y, z] = 1;
                        } else {
                            _blocks[x, y, z] = 0;
                        }
                    }
                }
            }
        }

        private bool IsTransparent(Vector3 index) {
            int x = Mathf.FloorToInt(index.x);
            int y = Mathf.FloorToInt(index.y);
            int z = Mathf.FloorToInt(index.z);

            if (x < 0 || x > _chunkWidth - 1 || y < 0 || y > _chunkHeight - 1 || z < 0 || z > _chunkWidth - 1) {
                return true;
            }

            return _blocks[x, y, z] == 0;
        }
    }
}