using ProceduralLandmassGeneration.Data;
using ProceduralLandmassGeneration.Generator.Mesh;
using UnityEngine;

namespace ProceduralLandmassGeneration.Generator {
    public class TerrainChunk {
        public event System.Action<TerrainChunk, bool> OnVisibilityChanged;

        // coordinate: ...,-2,-1,0,1,2,...
        // position: position in game world
        public Vector2 Coord;

        private const float COLLIDER_GENERATION_DISTANCE_THRESHOLD = 5;

        private readonly Vector2 _sampleCenter;
        private readonly float _maxViewDst;
        private Bounds _bounds;

        private readonly GameObject _meshObject;
        private readonly MeshFilter _meshFilter;
        private readonly MeshCollider _meshCollider;

        private readonly LODInfo[] _detailLevels;
        private LODMesh[] _lodMeshes;
        private readonly int _colliderLODIndex;
        private int _previousLODIndex = -1;

        private HeightMap _heightMap;
        private bool _heightMapReceived;

        private byte[,,] _blocksData;
        private bool _blocksDataReceived;

        private bool _hasSetCollider;

        private readonly HeightMapSettings _worldHeightMapSettings;
        private readonly HeightMapSettings _treeHeightMapSettings;
        private readonly MeshSettings _meshSettings;
        private readonly Transform _viewer;
        private readonly IMeshGenerator _meshGenerator;

        public TerrainChunk(Vector2 coord, TerrainGenerator terrainGenerator) {
            Coord = coord;
            _detailLevels = terrainGenerator.detailLevels;
            _colliderLODIndex = terrainGenerator.colliderLODIndex;
            _worldHeightMapSettings = terrainGenerator.worldHeightMapSettings;
            _treeHeightMapSettings = terrainGenerator.treeHeightMapSettings;
            _meshSettings = terrainGenerator.meshSettings;
            _viewer = terrainGenerator.viewer;

            _sampleCenter = coord * _meshSettings.MeshWorldSize / _meshSettings.meshScale;
            Vector2 chunkWorldPosition = coord * _meshSettings.MeshWorldSize;
            _bounds = new Bounds(chunkWorldPosition, Vector2.one * _meshSettings.MeshWorldSize);
            _maxViewDst = _detailLevels[_detailLevels.Length - 1].visibleDstThreshold;

            _meshGenerator = terrainGenerator.MeshGenerator;
            _meshObject = new GameObject("Sample center: " + _sampleCenter);
            var meshRenderer = _meshObject.AddComponent<MeshRenderer>();
            meshRenderer.material = terrainGenerator.mapMaterial;
            _meshFilter = _meshObject.AddComponent<MeshFilter>();
            _meshCollider = _meshObject.AddComponent<MeshCollider>();
            _meshObject.transform.position = new Vector3(chunkWorldPosition.x, 0, chunkWorldPosition.y);
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
                    _worldHeightMapSettings, _sampleCenter), OnHeightMapReceived);
        }


        private void OnHeightMapReceived(object heightMapObject) {
            _heightMap = (HeightMap)heightMapObject;
            _heightMapReceived = true;
            RequestBlocksData();
        }
        
        public void RequestBlocksData() {
            ThreadedDataRequester.RequestData(
                () => PopulateBlocksData(_heightMap.Values), OnBlocksDataReceived);
        }
        
        private void OnBlocksDataReceived(object blocksData) {
            _blocksData = (byte[,,])blocksData;
            _blocksDataReceived = true;

            UpdateTerrainChunk();
        }
        
        byte[,,] PopulateBlocksData(float[,] heightMap) {
            int chunkHeight = 64;
            int chunkWidth = (int)_meshSettings.MeshWorldSize + 2;
            int baseLandLevel = chunkHeight / 2;
            byte[,,] blocksData = new byte[chunkWidth, chunkHeight, chunkWidth];
            
            for (int x = 0; x < chunkWidth; x++) {
                for (int y = 0; y < chunkHeight; y++) {
                    for (int z = 0; z < chunkWidth; z++) {
                        if (y < Mathf.FloorToInt( baseLandLevel + heightMap[x, z])) {
                            blocksData[x, y, z] = 1;
                        } else if (y == Mathf.FloorToInt( baseLandLevel + heightMap[x, z])) {
                            blocksData[x, y, z] = 2;
                        } else {
                            blocksData[x, y, z] = 0;
                        }
                    }
                }
            }

            return blocksData;
        }

        public void UpdateTerrainChunk() {
            if (!_heightMapReceived || !_blocksDataReceived) return;

            bool wasVisible = IsVisible();
            bool visible = SqrDstFromViewerToEdge <= _maxViewDst * _maxViewDst;

            UpdateLODMesh();

            UpdateVisibility();

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

                    if (lodIndex != _previousLODIndex) {
                        LODMesh lodMesh = _lodMeshes[lodIndex];
                        if (lodMesh.HasMesh) {
                            _previousLODIndex = lodIndex;
                            _meshFilter.mesh = lodMesh.Mesh;
                        } else if (!lodMesh.HasRequestedMesh) {
                            lodMesh.RequestMesh(_blocksData, _meshGenerator);
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
            if (!_heightMapReceived || !_blocksDataReceived) return;
            if (_hasSetCollider) return;

            RequestMeshIfPlayerInRange();

            SetMeshColliderIfPlayerIsNear();


            void RequestMeshIfPlayerInRange() {
                if (SqrDstFromViewerToEdge < _detailLevels[_colliderLODIndex].SqrVisibleDstThreshold) {
                    if (!_lodMeshes[_colliderLODIndex].HasRequestedMesh) {
                        _lodMeshes[_colliderLODIndex].RequestMesh(_blocksData, _meshGenerator);
                    }
                }
            }

            void SetMeshColliderIfPlayerIsNear() {
                if (SqrDstFromViewerToEdge <
                    COLLIDER_GENERATION_DISTANCE_THRESHOLD * COLLIDER_GENERATION_DISTANCE_THRESHOLD) {
                    if (_lodMeshes[_colliderLODIndex].HasMesh) {
                        _meshCollider.sharedMesh = _lodMeshes[_colliderLODIndex].Mesh;
                        _hasSetCollider = true;
                    }
                }
            }
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
    }

    internal class LODMesh {
        public UnityEngine.Mesh Mesh;
        public bool HasRequestedMesh;
        public bool HasMesh;

        public event System.Action UpdateCallback;

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
            ThreadedDataRequester.RequestData(
                () => meshGenerator.GenerateMeshData(blocksData),
                OnMeshDataReceived);
        }
    }
}