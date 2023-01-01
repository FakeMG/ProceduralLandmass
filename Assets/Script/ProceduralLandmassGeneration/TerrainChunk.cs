using ProceduralLandmassGeneration.Data;
using ProceduralLandmassGeneration.Generator.Mesh;
using UnityEngine;

namespace ProceduralLandmassGeneration {
    public class TerrainChunk {
        public event System.Action<TerrainChunk, bool> OnVisibilityChanged;
        // coordinate: ...,-2,-1,0,1,2,...
        // position: position in game world
        public Vector2 Coord;

        private const float COLLIDER_GENERATION_DISTANCE_THRESHOLD = 5;

        private readonly GameObject _meshObject;
        private readonly Vector2 _sampleCenter;
        private Bounds _bounds;

        private readonly MeshRenderer _meshRenderer;
        private readonly MeshFilter _meshFilter;
        private readonly MeshCollider _meshCollider;

        private readonly LODInfo[] _detailLevels;
        private LODMesh[] _lodMeshes;
        private readonly int _colliderLODIndex;
        private int _previousLODIndex = -1;

        private HeightMap _heightMap;
        private bool _heightMapReceived;
        private bool _hasSetCollider;
        private readonly float _maxViewDst;

        private readonly HeightMapSettings _heightMapSettings;
        private readonly MeshSettings _meshSettings;
        private readonly Transform _viewer;

        public TerrainChunk(Vector2 coord, HeightMapSettings heightMapSettings, MeshSettings meshSettings,
            LODInfo[] detailLevels, int colliderLODIndex, Transform parent, Transform viewer, Material material) {
            Coord = coord;
            _detailLevels = detailLevels;
            _colliderLODIndex = colliderLODIndex;
            _heightMapSettings = heightMapSettings;
            _meshSettings = meshSettings;
            _viewer = viewer;

            _sampleCenter = coord * meshSettings.MeshWorldSize / meshSettings.meshScale;
            Vector2 actualChunkPosition = coord * meshSettings.MeshWorldSize;
            _bounds = new Bounds(actualChunkPosition, Vector2.one * meshSettings.MeshWorldSize);

            _meshObject = new GameObject("Terrain Chunk");
            _meshRenderer = _meshObject.AddComponent<MeshRenderer>();
            _meshFilter = _meshObject.AddComponent<MeshFilter>();
            _meshCollider = _meshObject.AddComponent<MeshCollider>();
            _meshRenderer.material = material;

            _meshObject.transform.position = new Vector3(actualChunkPosition.x, 0, actualChunkPosition.y);
            _meshObject.transform.parent = parent;
            _meshObject.name = "sample center: " + _sampleCenter;
            SetVisible(false);

            InitializeLODMeshArray();

            _maxViewDst = detailLevels[detailLevels.Length - 1].visibleDstThreshold;

            void InitializeLODMeshArray() {
                _lodMeshes = new LODMesh[detailLevels.Length];
                for (int i = 0; i < detailLevels.Length; i++) {
                    _lodMeshes[i] = new LODMesh(detailLevels[i].lod);
                    _lodMeshes[i].UpdateCallback += UpdateTerrainChunk;
                    if (i == colliderLODIndex) {
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
                    _heightMapSettings, _sampleCenter), OnHeightMapReceived);
        }


        private void OnHeightMapReceived(object heightMapObject) {
            _heightMap = (HeightMap)heightMapObject;
            _heightMapReceived = true;

            UpdateTerrainChunk();
        }

        public void UpdateTerrainChunk() {
            if (!_heightMapReceived) return;

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
                            lodMesh.RequestMesh(_heightMap, _meshSettings);
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
            if (_hasSetCollider) return;

            RequestMeshIfPlayerInRange();

            SetMeshColliderIfPlayerIsNear();


            void RequestMeshIfPlayerInRange() {
                if (SqrDstFromViewerToEdge < _detailLevels[_colliderLODIndex].SqrVisibleDstThreshold) {
                    if (!_lodMeshes[_colliderLODIndex].HasRequestedMesh) {
                        _lodMeshes[_colliderLODIndex].RequestMesh(_heightMap, _meshSettings);
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
        public Mesh Mesh;
        public bool HasRequestedMesh;
        public bool HasMesh;

        public event System.Action UpdateCallback;

        private readonly int _lod;

        public LODMesh(int lod) {
            _lod = lod;
        }

        private void OnMeshDataReceived(object meshDataObject) {
            Mesh = ((MeshData)meshDataObject).CreateMesh();
            HasMesh = true;

            UpdateCallback?.Invoke();
        }

        public void RequestMesh(HeightMap heightMap, MeshSettings meshSettings) {
            HasRequestedMesh = true;
            BlockyMeshGenerator generator = new BlockyMeshGenerator();
            ThreadedDataRequester.RequestData(
                () => generator.GenerateMeshData(heightMap.Values, meshSettings, _lod),
                OnMeshDataReceived);
        }
    }
}