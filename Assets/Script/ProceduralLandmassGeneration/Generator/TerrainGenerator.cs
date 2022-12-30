using System.Collections.Generic;
using ProceduralLandmassGeneration.Data;
using UnityEngine;

namespace ProceduralLandmassGeneration {
    public class TerrainGenerator : MonoBehaviour {
        private const float VIEWER_MOVE_THRESHOLD_FOR_CHUNK_UPDATE = 25f;

        private const float SQR_VIEWER_MOVE_THRESHOLD_FOR_CHUNK_UPDATE =
            VIEWER_MOVE_THRESHOLD_FOR_CHUNK_UPDATE * VIEWER_MOVE_THRESHOLD_FOR_CHUNK_UPDATE;

        [Min(0)] public int colliderLODIndex;
        public LODInfo[] detailLevels;

        public MeshSettings meshSettings;
        public HeightMapSettings heightMapSettings;
        public TextureData textureSettings;

        public Transform viewer;
        public Material mapMaterial;

        private Vector2 _viewerPosition2DOld;

        private float _meshWorldSize;
        private int _chunksVisibleInViewDst;

        private readonly Dictionary<Vector2, TerrainChunk> _terrainChunkDictionary =
            new Dictionary<Vector2, TerrainChunk>();

        private readonly List<TerrainChunk> _visibleTerrainChunksList = new List<TerrainChunk>();

        private void Start() {
            textureSettings.ApplyToMaterial(mapMaterial);
            textureSettings.UpdateMeshHeights(mapMaterial, heightMapSettings.minHeight, heightMapSettings.maxHeight);

            float maxViewDst = detailLevels[detailLevels.Length - 1].visibleDstThreshold;
            _meshWorldSize = meshSettings.MeshWorldSize;
            _chunksVisibleInViewDst = Mathf.RoundToInt(maxViewDst / _meshWorldSize);

            UpdateChunksThatPlayerPassThrough();
        }

        private void Update() {
            if (ViewerPosition2D != _viewerPosition2DOld) {
                foreach (TerrainChunk chunk in _visibleTerrainChunksList) {
                    chunk.UpdateCollisionMesh();
                }
            }

            if ((_viewerPosition2DOld - ViewerPosition2D).sqrMagnitude > SQR_VIEWER_MOVE_THRESHOLD_FOR_CHUNK_UPDATE) {
                _viewerPosition2DOld = ViewerPosition2D;
                UpdateChunksThatPlayerPassThrough();
            }
        }

        private void UpdateChunksThatPlayerPassThrough() {
            HashSet<Vector2> alreadyUpdatedChunkCoords = new HashSet<Vector2>();
            UpdateVisibleChunks();

            UpdateChunksInRange();

            // some of the visible chunks may not be in range anymore
            // so we have to update them separately
            void UpdateVisibleChunks() {
                for (int i = _visibleTerrainChunksList.Count - 1; i >= 0; i--) {
                    _visibleTerrainChunksList[i].UpdateTerrainChunk();
                    if (i < _visibleTerrainChunksList.Count) {
                        alreadyUpdatedChunkCoords.Add(_visibleTerrainChunksList[i].Coord);
                    }
                }
            }

            void UpdateChunksInRange() {
                int currentChunkCoordX = Mathf.RoundToInt(ViewerPosition2D.x / _meshWorldSize);
                int currentChunkCoordY = Mathf.RoundToInt(ViewerPosition2D.y / _meshWorldSize);

                for (int yOffset = -_chunksVisibleInViewDst; yOffset <= _chunksVisibleInViewDst; yOffset++) {
                    for (int xOffset = -_chunksVisibleInViewDst; xOffset <= _chunksVisibleInViewDst; xOffset++) {
                        Vector2 viewedChunkCoord =
                            new Vector2(currentChunkCoordX + xOffset, currentChunkCoordY + yOffset);

                        if (!alreadyUpdatedChunkCoords.Contains(viewedChunkCoord)) {
                            if (_terrainChunkDictionary.ContainsKey(viewedChunkCoord)) {
                                _terrainChunkDictionary[viewedChunkCoord].UpdateTerrainChunk();
                            } else {
                                TerrainChunk newChunk = new TerrainChunk(viewedChunkCoord, heightMapSettings,
                                    meshSettings,
                                    detailLevels, colliderLODIndex, transform, viewer, mapMaterial);
                                _terrainChunkDictionary.Add(viewedChunkCoord, newChunk);
                                newChunk.OnVisibilityChanged += OnTerrainChunkVisibilityChanged;
                                newChunk.RequestHeightMap();
                            }
                        }
                    }
                }
            }
        }

        private void OnTerrainChunkVisibilityChanged(TerrainChunk chunk, bool isVisible) {
            if (isVisible) {
                _visibleTerrainChunksList.Add(chunk);
            } else {
                _visibleTerrainChunksList.Remove(chunk);
            }
        }

        private Vector2 ViewerPosition2D {
            get {
                Vector3 viewerPosition = viewer.position;
                return new Vector2(viewerPosition.x, viewerPosition.z);
            }
        }
    }

    [System.Serializable]
    public struct LODInfo {
        [Range(0, MeshSettings.NUM_SUPPORTED_LODS - 1)]
        public int lod;

        public float visibleDstThreshold;

        public float SqrVisibleDstThreshold => visibleDstThreshold * visibleDstThreshold;
    }
}