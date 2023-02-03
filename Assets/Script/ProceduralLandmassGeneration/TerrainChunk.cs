using System;
using System.Collections.Generic;
using ProceduralLandmassGeneration.Data;
using ProceduralLandmassGeneration.Data.Minecraft;
using ProceduralLandmassGeneration.Generator.Mesh;
using UnityEngine;

namespace ProceduralLandmassGeneration.Generator {
    public class TerrainChunk {
        public event Action<TerrainChunk, bool> OnVisibilityChanged;

        // coordinate: ...,-2,-1,0,1,2,...
        // position: position in game world
        public Vector2 Coord;

        private const float COLLIDER_GENERATION_DISTANCE_THRESHOLD = 5;

        private readonly Vector2 _sampleCenter;
        private readonly Vector2 _topLeftOfMesh;
        private readonly float _maxViewDst;
        private Bounds _bounds;
        private Vector2 _chunkWorld2DPosition;

        private readonly GameObject _meshObject;
        private readonly MeshFilter _meshFilter;
        private readonly MeshCollider _meshCollider;
        private bool _hasAppliedMesh = true;

        private readonly LODInfo[] _detailLevels;
        private LODMesh[] _lodMeshes;
        private readonly int _colliderLODIndex;
        private int _previousLODIndex = -1;

        private HeightMap _groundHeightMap;
        private bool _groundHeightMapReceived;
        private HeightMap _forestHeightMap;
        private bool _forestHeightMapReceived;
        private HeightMap _treeHeightMap;
        private bool _treeHeightMapReceived;

        private byte[,,] _blocksData;
        public bool BlocksDataReceived;

        private bool _hasSetCollider;

        public Queue<VoxelMod> Modifications = new Queue<VoxelMod>();

        private readonly HeightMapSettings _groundHeightMapSettings;
        private readonly HeightMapSettings _forestHeightMapSettings;
        private readonly HeightMapSettings _treeHeightMapSettings;
        private readonly MeshSettings _meshSettings;
        private readonly Transform _viewer;
        private readonly IMeshGenerator _meshGenerator;
        private readonly TerrainGenerator _terrainGenerator;

        public TerrainChunk(Vector2 coord, TerrainGenerator terrainGenerator) {
            Coord = coord;
            _terrainGenerator = terrainGenerator;
            _detailLevels = terrainGenerator.detailLevels;
            _colliderLODIndex = terrainGenerator.colliderLODIndex;
            _groundHeightMapSettings = terrainGenerator.groundHeightMapSettings;
            _forestHeightMapSettings = terrainGenerator.forestHeightMapSettings;
            _treeHeightMapSettings = terrainGenerator.treeHeightMapSettings;
            _meshSettings = terrainGenerator.meshSettings;
            _viewer = terrainGenerator.viewer;

            _sampleCenter = Coord * _meshSettings.MeshWorldSize / _meshSettings.meshScale;
            float halfOfChunk = _meshSettings.MeshWorldSize / 2;
            _topLeftOfMesh = Coord * _meshSettings.MeshWorldSize + new Vector2(-halfOfChunk, halfOfChunk);
            _chunkWorld2DPosition = Coord * _meshSettings.MeshWorldSize;
            _bounds = new Bounds(_chunkWorld2DPosition, Vector2.one * _meshSettings.MeshWorldSize);
            _maxViewDst = _detailLevels[_detailLevels.Length - 1].visibleDstThreshold;

            _meshGenerator = terrainGenerator.MeshGenerator;
            _meshObject = new GameObject("Coord: " + Coord);
            var meshRenderer = _meshObject.AddComponent<MeshRenderer>();
            meshRenderer.material = terrainGenerator.mapMaterial;
            _meshFilter = _meshObject.AddComponent<MeshFilter>();
            _meshCollider = _meshObject.AddComponent<MeshCollider>();
            _meshObject.transform.position = new Vector3(_chunkWorld2DPosition.x, 0, _chunkWorld2DPosition.y);
            _meshObject.transform.parent = terrainGenerator.transform;
            SetVisible(false);

            InitializeLODMeshArray();

            void InitializeLODMeshArray() {
                _lodMeshes = new LODMesh[_detailLevels.Length];
                for (int i = 0; i < _detailLevels.Length; i++) {
                    _lodMeshes[i] = new LODMesh(_detailLevels[i].lod);
                    _lodMeshes[i].UpdateCallback += UpdateTerrainChunk;
                    if (i == _colliderLODIndex) {
                        _lodMeshes[i].UpdateCallback += UpdateCollisionMesh;
                    }
                }
            }
        }

        // used when being created
        public void RequestHeightMap() {
            ThreadedDataRequester.RequestData(
                () => HeightMapGenerator.GenerateHeightMap((int)_meshSettings.MeshWorldSize + 2,
                    (int)_meshSettings.MeshWorldSize + 2,
                    _groundHeightMapSettings, _sampleCenter), OnGroundHeightMapReceived);
            ThreadedDataRequester.RequestData(
                () => HeightMapGenerator.GenerateHeightMap((int)_meshSettings.MeshWorldSize + 2,
                    (int)_meshSettings.MeshWorldSize + 2,
                    _forestHeightMapSettings, _sampleCenter), OnForestHeightMapReceived);
            ThreadedDataRequester.RequestData(
                () => HeightMapGenerator.GenerateHeightMap((int)_meshSettings.MeshWorldSize + 2,
                    (int)_meshSettings.MeshWorldSize + 2,
                    _treeHeightMapSettings, _sampleCenter), OnTreeHeightMapReceived);
        }


        private void OnGroundHeightMapReceived(object heightMapObject) {
            _groundHeightMap = (HeightMap)heightMapObject;
            _groundHeightMapReceived = true;
            if (!_forestHeightMapReceived || !_treeHeightMapReceived) return;
            RequestBlocksData();
        }

        private void OnForestHeightMapReceived(object heightMapObject) {
            _forestHeightMap = (HeightMap)heightMapObject;
            _forestHeightMapReceived = true;
            if (!_groundHeightMapReceived || !_treeHeightMapReceived) return;
            RequestBlocksData();
        }

        private void OnTreeHeightMapReceived(object heightMapObject) {
            _treeHeightMap = (HeightMap)heightMapObject;
            _treeHeightMapReceived = true;
            if (!_groundHeightMapReceived || !_forestHeightMapReceived) return;
            RequestBlocksData();
        }

        private void RequestBlocksData() {
            ThreadedDataRequester.RequestData(PopulateBlocksData, OnBlocksDataReceived);
        }

        byte[,,] PopulateBlocksData() {
            int chunkHeight = 128;
            int chunkWidth = (int)_meshSettings.MeshWorldSize + 2;
            int baseLandLevel = chunkHeight / 2;
            byte[,,] blocksData = new byte[chunkWidth, chunkHeight, chunkWidth];
            float[,] heightMap = _groundHeightMap.Values;
            float[,] forestHeightMap = _forestHeightMap.Values;
            float[,] treeHeightMap = _treeHeightMap.Values;

            for (int x = 0; x < chunkWidth; x++) {
                for (int y = 0; y < chunkHeight; y++) {
                    for (int z = 0; z < chunkWidth; z++) {
                        if (y < Mathf.FloorToInt(baseLandLevel + heightMap[x, z])) {
                            blocksData[x, y, z] = 1;
                        } else if (y == Mathf.FloorToInt(baseLandLevel + heightMap[x, z])) {
                            blocksData[x, y, z] = 2;

                            if (forestHeightMap[x, z] >= 0.5f) {
                                Modifications.Enqueue(new VoxelMod(
                                    new Vector3(x - 1 + _topLeftOfMesh.x, y, -(z - 1) + _topLeftOfMesh.y), 3));

                                if (treeHeightMap[x, z] <= 0.2f) {
                                    for (int i = -2; i <= 2; i++) {
                                        for (int j = 0; j <= 1; j++) {
                                            for (int k = -2; k <= 2; k++) {
                                                Vector3 localPos = new Vector3(x + i, y + 4 + j, z + k);
                                                Vector3 blockWorldPos = LocalToWorldPos(localPos);

                                                if (localPos.x <= 0 || localPos.x >= chunkWidth - 1 ||
                                                    localPos.z <= 0 || localPos.z >= chunkWidth - 1) {
                                                    _terrainGenerator.AddChunkModData(new VoxelMod(blockWorldPos, 4));
                                                } else if (localPos.y >= 0 && localPos.y <= chunkHeight - 1) {
                                                    Modifications.Enqueue(new VoxelMod(blockWorldPos, 4));
                                                }
                                            }
                                        }
                                    }

                                    for (int k = 0; k < 2; k++) {
                                        for (int i = 0; i <= 1; i++) {
                                            for (int j = -1; j <= 1; j++) {
                                                int temp = 0 + i;
                                                Vector3 localPos = new Vector3(x + j * (temp - 1), y + 6 + k,
                                                    z + j * temp);
                                                Vector3 blockWorldPos = LocalToWorldPos(localPos);

                                                if (localPos.x <= 0 || localPos.x >= chunkWidth - 1 ||
                                                    localPos.z <= 0 || localPos.z >= chunkWidth - 1) {
                                                    _terrainGenerator.AddChunkModData(new VoxelMod(blockWorldPos, 4));
                                                } else if (localPos.y >= 0 && localPos.y <= chunkHeight - 1) {
                                                    Modifications.Enqueue(new VoxelMod(blockWorldPos, 4));
                                                }
                                            }
                                        }
                                    }

                                    for (int i = 1; i <= 5; i++) {
                                        Vector3 localPos = new Vector3(x, y + i, z);
                                        Vector3 blockWorldPos = LocalToWorldPos(localPos);
                                        Modifications.Enqueue(new VoxelMod(blockWorldPos, 4));
                                    }
                                }
                            }
                        } else {
                            blocksData[x, y, z] = 0;
                        }
                    }
                }
            }

            if (_groundHeightMap.MaxValue + baseLandLevel > 80) {
                PerlinWorm worm = new PerlinWorm(_sampleCenter,
                    new Vector3(_chunkWorld2DPosition.x, 0, _chunkWorld2DPosition.y) + Vector3.up * (chunkHeight / 3),
                    _groundHeightMapSettings);
                List<Vector3> wormParts = worm.GenerateWorm();

                foreach (var blockWorldPos in wormParts) {
                    Vector3 localPos = WorldToLocalPos(blockWorldPos);
                    
                    if (blockWorldPos.y <= baseLandLevel + _groundHeightMap.MinValue && blockWorldPos.y > 0) {
                        if (localPos.x <= 0 || localPos.x >= chunkWidth - 1 ||
                            localPos.z <= 0 || localPos.z >= chunkWidth - 1) {
                            _terrainGenerator.AddChunkModData(new VoxelMod(blockWorldPos, 0));
                            
                            Vector2 currentChunkCoord = _terrainGenerator.GetChunkCoordByWorldPos(new Vector2(blockWorldPos.x, blockWorldPos.z));
                            foreach (var direction in VoxelData.DirectionsAroundBlock) {
                                Vector3 surroundingBlock = blockWorldPos + direction;
                                Vector2 surroundingBlock2D = new Vector2(surroundingBlock.x, surroundingBlock.z);
                                Vector2 surroundingChunkCoord = _terrainGenerator.GetChunkCoordByWorldPos(surroundingBlock2D);
                                
                                if (surroundingChunkCoord != currentChunkCoord) {
                                    _terrainGenerator.AddChunkModData(surroundingChunkCoord, new VoxelMod(blockWorldPos, 0));
                                }
                            }
                            
                        } else if (localPos.y >= 0 && localPos.y <= chunkHeight - 1) {
                            Modifications.Enqueue(new VoxelMod(blockWorldPos, 0));
                            
                            foreach (var direction in VoxelData.DirectionsAroundBlock) {
                                Vector3 surroundingBlock = blockWorldPos + direction;
                                Vector2 surroundingBlock2D = new Vector2(surroundingBlock.x, surroundingBlock.z);
                                Vector2 surroundingChunkCoord = _terrainGenerator.GetChunkCoordByWorldPos(surroundingBlock2D);
                                
                                if (surroundingChunkCoord != Coord) {
                                    _terrainGenerator.AddChunkModData(surroundingChunkCoord, new VoxelMod(blockWorldPos, 0));
                                }
                            }
                        }
                    }
                }
            }


            return blocksData;
        }

        private void OnBlocksDataReceived(object blocksData) {
            _blocksData = (byte[,,])blocksData;
            BlocksDataReceived = true;

            UpdateTerrainChunk();
        }

        public void UpdateTerrainChunk() {
            if (!_groundHeightMapReceived || !BlocksDataReceived) return;

            bool wasVisible = IsVisible();
            bool visible = SqrDstFromViewerToEdge <= _maxViewDst * _maxViewDst;

            UpdateLODMesh();

            UpdateVisibility();

            //TODO: this doesn't work with _detailLevels.Length > 0, need update
            void UpdateLODMesh() {
                if (visible) {
                    int lodIndex = 0;

                    for (int i = 0; i < _detailLevels.Length - 1; i++) {
                        if (SqrDstFromViewerToEdge > _detailLevels[i].SqrVisibleDstThreshold) {
                            lodIndex = i + 1;
                        } else {
                            break;
                        }
                    }

                    if (lodIndex != _previousLODIndex || !_hasAppliedMesh) {
                        LODMesh lodMesh = _lodMeshes[lodIndex];
                        if (lodMesh.HasMesh) {
                            _previousLODIndex = lodIndex;
                            _meshFilter.mesh = lodMesh.Mesh;
                            _hasAppliedMesh = true;
                        } else if (!lodMesh.HasRequestedMesh) {
                            if (Modifications.Count == 0) {
                                lodMesh.RequestMesh(_blocksData, _meshGenerator);
                            }
                        }
                    }
                }
            }

            void UpdateVisibility() {
                if (wasVisible != visible) {
                    SetVisible(visible);
                    OnVisibilityChanged?.Invoke(this, visible);
                }
            }
        }

        public void UpdateCollisionMesh() {
            if (!_groundHeightMapReceived || !BlocksDataReceived) return;
            if (_hasSetCollider) return;

            RequestMeshIfPlayerInRange();

            SetMeshColliderIfPlayerIsNear();


            void RequestMeshIfPlayerInRange() {
                if (!(SqrDstFromViewerToEdge < _detailLevels[_colliderLODIndex].SqrVisibleDstThreshold)) return;
                if (_lodMeshes[_colliderLODIndex].HasRequestedMesh) return;

                if (Modifications.Count == 0) {
                    _lodMeshes[_colliderLODIndex].RequestMesh(_blocksData, _meshGenerator);
                }
            }

            void SetMeshColliderIfPlayerIsNear() {
                if (!(SqrDstFromViewerToEdge <
                      COLLIDER_GENERATION_DISTANCE_THRESHOLD * COLLIDER_GENERATION_DISTANCE_THRESHOLD)) return;
                if (!_lodMeshes[_colliderLODIndex].HasMesh) return;

                _meshCollider.sharedMesh = _lodMeshes[_colliderLODIndex].Mesh;
                _hasSetCollider = true;
            }
        }

        public void ApplyModData() {
            Vector3 value = Vector3.zero;
            try {
                if (!BlocksDataReceived) return;
                if (!_hasAppliedMesh) return;

                if (Modifications.Count > 0) {
                    while (Modifications.Count > 0) {
                        VoxelMod modData = Modifications.Dequeue();

                        Vector3 localPos = WorldToLocalPos(modData.WorldPosition);
                        value = localPos;
                        _blocksData[(int)localPos.x, (int)localPos.y, (int)localPos.z] = modData.BlockID;
                    }

                    UpdateMesh();
                }
            } catch (Exception e) {
                Debug.Log(Coord + " " + value + " " + e);
            }
        }

        private void UpdateMesh() {
            foreach (LODMesh lodMesh in _lodMeshes) {
                lodMesh.HasMesh = false;
                lodMesh.RequestMesh(_blocksData, _meshGenerator);
            }

            _hasAppliedMesh = false;
            _hasSetCollider = false;
        }

        public void SetVisible(bool visible) {
            _meshObject.SetActive(visible);
        }

        public bool IsVisible() {
            return _meshObject.activeSelf;
        }

        private float SqrDstFromViewerToEdge {
            get {
                Vector3 position = _viewer.position;
                return _bounds.SqrDistance(new Vector2(position.x, position.z));
            }
        }

        private Vector3 WorldToLocalPos(Vector3 worldPos) {
            worldPos += new Vector3(0.01f, 0, -0.01f);
            return new Vector3((int)(worldPos.x - _topLeftOfMesh.x + 1), (int)worldPos.y, -(int)(worldPos.z - _topLeftOfMesh.y - 1));
        }

        private Vector3 LocalToWorldPos(Vector3 localPos) {
            return new Vector3(localPos.x - 1 + _topLeftOfMesh.x, localPos.y, -(localPos.z - 1) + _topLeftOfMesh.y);
        }
    }

    internal class LODMesh {
        public UnityEngine.Mesh Mesh;
        public bool HasRequestedMesh;
        public bool HasMesh;

        public event Action UpdateCallback;

        private readonly int _lod;

        public LODMesh(int lod) {
            _lod = lod;
        }

        private void OnMeshDataReceived(object meshDataObject) {
            Mesh = ((IMeshData)meshDataObject).CreateMesh();
            HasMesh = true;

            UpdateCallback?.Invoke();
        }

        public void RequestMesh(byte[,,] blocksData, IMeshGenerator meshGenerator) {
            HasRequestedMesh = true;
            ThreadedDataRequester.RequestData(() => meshGenerator.GenerateMeshData(blocksData), OnMeshDataReceived);
        }
    }

    public class VoxelMod {
        public Vector3 WorldPosition;
        public readonly byte BlockID;

        public VoxelMod() {
            WorldPosition = new Vector3();
            BlockID = 0;
        }

        public VoxelMod(Vector3 worldPosition, byte blockID) {
            WorldPosition = worldPosition;
            BlockID = blockID;
        }
    }
}