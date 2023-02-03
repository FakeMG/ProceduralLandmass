using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using ProceduralLandmassGeneration.Data;
using ProceduralLandmassGeneration.Generator.Mesh;
using UnityEngine;

namespace ProceduralLandmassGeneration.Generator {
    public class TerrainGenerator : MonoBehaviour {
        private const float VIEWER_MOVE_THRESHOLD_FOR_CHUNK_UPDATE = 25f;

        private const float SQR_VIEWER_MOVE_THRESHOLD_FOR_CHUNK_UPDATE =
            VIEWER_MOVE_THRESHOLD_FOR_CHUNK_UPDATE * VIEWER_MOVE_THRESHOLD_FOR_CHUNK_UPDATE;

        [Min(0)] public int colliderLODIndex;
        public LODInfo[] detailLevels;

        public MeshSettings meshSettings;
        public HeightMapSettings groundHeightMapSettings;
        public HeightMapSettings forestHeightMapSettings;

        public HeightMapSettings treeHeightMapSettings;
        // public TextureData textureSettings;

        public Transform viewer;
        public Material mapMaterial;
        public IMeshGenerator MeshGenerator;

        public List<BlockType> blockTypes = new List<BlockType>();

        [SerializeField] private float transferModificationDelay = 0.25f;
        private Vector2 _viewerPosition2DOld;

        private float _meshWorldSize;
        private int _chunksVisibleInViewDst;

        private readonly Dictionary<Vector2, Queue<VoxelMod>>
            _chunkModData = new Dictionary<Vector2, Queue<VoxelMod>>();

        private readonly Dictionary<Vector2, TerrainChunk> _terrainChunkDictionary =
            new Dictionary<Vector2, TerrainChunk>();

        private readonly List<TerrainChunk> _visibleTerrainChunksList = new List<TerrainChunk>();

        private void Start() {
            // textureSettings.ApplyToMaterial(mapMaterial);
            // textureSettings.UpdateMeshHeights(mapMaterial, heightMapSettings.minHeight, heightMapSettings.maxHeight);

            MeshGenerator = GetComponent<IMeshGenerator>();

            float maxViewDst = detailLevels[detailLevels.Length - 1].visibleDstThreshold;
            _meshWorldSize = meshSettings.MeshWorldSize;
            _chunksVisibleInViewDst = Mathf.RoundToInt(maxViewDst / _meshWorldSize);

            UpdateChunksThatPlayerPassThrough();

            StartCoroutine(ApplyModData());
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

        //multithreading method
        public void AddChunkModData(VoxelMod modData) {
            Vector3 blockWorldPos = modData.WorldPosition;
            Vector2 chunkCoord = GetChunkCoordByWorldPos(new Vector2(blockWorldPos.x, blockWorldPos.z));

            lock (_chunkModData) {
                Queue<VoxelMod> mods;
                if (_chunkModData.ContainsKey(chunkCoord)) {
                    mods = _chunkModData[chunkCoord];
                } else {
                    mods = new Queue<VoxelMod>();
                    _chunkModData.Add(chunkCoord, mods);
                }

                mods.Enqueue(modData);
            }
        }

        public void AddChunkModData(Vector2 chunkCoord, VoxelMod modData) {
            lock (_chunkModData) {
                Queue<VoxelMod> mods;
                if (_chunkModData.ContainsKey(chunkCoord)) {
                    mods = _chunkModData[chunkCoord];
                } else {
                    mods = new Queue<VoxelMod>();
                    _chunkModData.Add(chunkCoord, mods);
                }

                mods.Enqueue(modData);
            }
        }

        private IEnumerator ApplyModData() {
            while (true) {
                TransferModData();
                UpdateMeshForChunksInRange();
                yield return new WaitForSeconds(transferModificationDelay);
            }
        }

        private void TransferModData() {
            int currentChunkCoordX = Mathf.RoundToInt(ViewerPosition2D.x / _meshWorldSize);
            int currentChunkCoordY = Mathf.RoundToInt(ViewerPosition2D.y / _meshWorldSize);

            for (int yOffset = -_chunksVisibleInViewDst; yOffset <= _chunksVisibleInViewDst; yOffset++) {
                for (int xOffset = -_chunksVisibleInViewDst; xOffset <= _chunksVisibleInViewDst; xOffset++) {
                    Vector2 viewedChunkCoord = new Vector2(currentChunkCoordX + xOffset, currentChunkCoordY + yOffset);
                    if (!_chunkModData.ContainsKey(viewedChunkCoord)) continue;
                    if (!_terrainChunkDictionary.ContainsKey(viewedChunkCoord)) continue;
                    if (!_terrainChunkDictionary[viewedChunkCoord].BlocksDataReceived) continue;

                    lock (_chunkModData) {
                        Queue<VoxelMod> modQueue = _chunkModData[viewedChunkCoord];
                        while (modQueue.Count > 0) {
                            VoxelMod modData = modQueue.Dequeue();
                            _terrainChunkDictionary[viewedChunkCoord].Modifications.Enqueue(modData);
                        }
                    }
                }
            }
        }

        private void UpdateMeshForChunksInRange() {
            int currentChunkCoordX = Mathf.RoundToInt(ViewerPosition2D.x / _meshWorldSize);
            int currentChunkCoordY = Mathf.RoundToInt(ViewerPosition2D.y / _meshWorldSize);

            for (int yOffset = -_chunksVisibleInViewDst; yOffset <= _chunksVisibleInViewDst; yOffset++) {
                for (int xOffset = -_chunksVisibleInViewDst; xOffset <= _chunksVisibleInViewDst; xOffset++) {
                    Vector2 viewedChunkCoord = new Vector2(currentChunkCoordX + xOffset, currentChunkCoordY + yOffset);
                    if (_terrainChunkDictionary.ContainsKey(viewedChunkCoord)) {
                        _terrainChunkDictionary[viewedChunkCoord].ApplyModData();
                    }
                }
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
                                TerrainChunk newChunk = new TerrainChunk(viewedChunkCoord, this);
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

        public Vector2 GetChunkCoordByWorldPos(Vector2 worldPos) {
            worldPos += new Vector2(0.01f, -0.01f);
            int currentChunkCoordX = Mathf.RoundToInt(worldPos.x / _meshWorldSize);
            int currentChunkCoordY = Mathf.RoundToInt(worldPos.y / _meshWorldSize);
            return new Vector2(currentChunkCoordX, currentChunkCoordY);
        }
    }

    [Serializable]
    public struct LODInfo {
        [Range(0, MeshSettings.NUM_SUPPORTED_LODS - 1)]
        public int lod;

        public float visibleDstThreshold;

        public float SqrVisibleDstThreshold => visibleDstThreshold * visibleDstThreshold;
    }

    [Serializable]
    public class BlockType {
        public string blockName;
        public bool isSolid;

        [Header("Texture Values")] public Vector2Int positiveYTexture;
        public Vector2Int negativeYTexture;
        public Vector2Int positiveZTexture;
        public Vector2Int negativeZTexture;
        public Vector2Int positiveXTexture;
        public Vector2Int negativeXTexture;

        // Back, Front, Top, Bottom, Left, Right

        public Vector2Int GetTextureID(int faceIndex) {
            switch (faceIndex) {
                case 0:
                    return positiveYTexture;
                case 1:
                    return negativeYTexture;
                case 2:
                    return positiveZTexture;
                case 3:
                    return negativeZTexture;
                case 4:
                    return positiveXTexture;
                case 5:
                    return negativeXTexture;
                default:
                    Debug.Log("Error in GetTextureID; invalid face index");
                    return Vector2Int.zero;
            }
        }
    }
}