using System.Collections.Generic;
using ProceduralLandmassGeneration.Data;
using ProceduralLandmassGeneration.Data.Minecraft;
using UnityEngine;

namespace ProceduralLandmassGeneration.Generator.Mesh {
    public class BlockyMeshGenerator : MonoBehaviour, IMeshGenerator {
        private TerrainGenerator _terrainGenerator;
        private MeshSettings _meshSettings;
        private List<BlockType> _blockTypes;
        private Vector2 _textureSize;
        
        private int _chunkHeight;
        private int _chunkWidth;
        private int _baseLandLevel;

        private void Start() {
            _terrainGenerator = GetComponent<TerrainGenerator>();
            _meshSettings = _terrainGenerator.meshSettings;
            _blockTypes = _terrainGenerator.blockTypes;
            _textureSize = new Vector2(_terrainGenerator.mapMaterial.mainTexture.width,
                _terrainGenerator.mapMaterial.mainTexture.height);
            
            _chunkHeight = 64;
            _chunkWidth = (int)_meshSettings.MeshWorldSize + 2;
            _baseLandLevel = _chunkHeight / 2;
        }

        public IMeshData GenerateMeshData(float[,] heightMap) {
            int[,,] blocks = new int[_chunkWidth, _chunkHeight, _chunkWidth];

            List<Vector3> verts = new List<Vector3>();
            List<int> tris = new List<int>();
            List<Vector2> uv = new List<Vector2>();

            // cần phải build mesh từ +z -> -z để lấy đối xứng với noiseMap
            Vector3 topLeftOfMesh = new Vector3(-1 * (_chunkWidth - 2) / 2.0f, 0, 1 * (_chunkWidth - 2) / 2.0f);

            PopulateBlocksData();

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

                                    AddTexture(_blockTypes[blocks[x,y,z]].GetTextureID(direction));

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

            BlockyMeshData blockyMeshData = new BlockyMeshData(verts, tris, uv);
            return blockyMeshData;

            void PopulateBlocksData() {
                for (int x = 0; x < _chunkWidth; x++) {
                    for (int y = 0; y < _chunkHeight; y++) {
                        for (int z = 0; z < _chunkWidth; z++) {
                            if (y <= _baseLandLevel + heightMap[x, z]) {
                                blocks[x, y, z] = 1;
                            } else {
                                blocks[x, y, z] = 0;
                            }
                        }
                    }
                }
            }

            bool IsTransparent(Vector3 index) {
                int x = Mathf.FloorToInt(index.x);
                int y = Mathf.FloorToInt(index.y);
                int z = Mathf.FloorToInt(index.z);

                if (x < 0 || x > _chunkWidth - 1 || y < 0 || y > _chunkHeight - 1 || z < 0 || z > _chunkWidth - 1) {
                    return true;
                }

                return !_blockTypes[blocks[x, y, z]].isSolid;
            }

            void AddTexture(Vector2Int texturePos) {
                int numOfBlockPerLine = (int)_textureSize.x / 16;
                float normalizedBlockSize = 1.0f / numOfBlockPerLine;
                float x = (float)texturePos.x / numOfBlockPerLine;
                float y = (float)texturePos.y / numOfBlockPerLine;

                uv.Add(new Vector2(x, y + normalizedBlockSize));
                uv.Add(new Vector2(x + normalizedBlockSize, y + normalizedBlockSize));
                uv.Add(new Vector2(x + normalizedBlockSize, y));
                uv.Add(new Vector2(x, y));
            }
        }
    }
}