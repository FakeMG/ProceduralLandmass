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

        private readonly GameObject _meshObject;
        private readonly Vector2 _sampleCenter;
        private Bounds _bounds;

        private readonly MeshFilter _meshFilter;
        private readonly MeshCollider _meshCollider;
        private readonly IMeshGenerator _meshGenerator;

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

        public TerrainChunk(Vector2 coord, TerrainGenerator terrainGenerator) {
            Coord = coord;
            _detailLevels = terrainGenerator.detailLevels;
            _colliderLODIndex = terrainGenerator.colliderLODIndex;
            _heightMapSettings = terrainGenerator.heightMapSettings;
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
                            lodMesh.RequestMesh(_heightMap, _meshGenerator);
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
                        _lodMeshes[_colliderLODIndex].RequestMesh(_heightMap, _meshGenerator);
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

        public void RequestMesh(HeightMap heightMap, IMeshGenerator meshGenerator) {
            HasRequestedMesh = true;
            ThreadedDataRequester.RequestData(
                () => meshGenerator.GenerateMeshData(heightMap.Values),
                OnMeshDataReceived);
        }
    }
}