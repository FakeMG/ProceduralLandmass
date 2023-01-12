using ProceduralLandmassGeneration.Data.Minecraft;
using ProceduralLandmassGeneration.Generator;
using UnityEngine;

public class EditChunk : MonoBehaviour {
    [SerializeField] private TerrainGenerator terrainGenerator;
    [SerializeField] private float reachDistance = 3f;

    private Camera _mainCamera;

    private void Start() {
        _mainCamera = Camera.main;
    }

    private void Update() {
        if (Physics.Raycast(_mainCamera.transform.position, _mainCamera.transform.forward, out RaycastHit hitInfo,
                reachDistance)) {
            if (Input.GetMouseButtonDown(0) || Input.GetMouseButtonDown(1)) {
                Vector3 blockWorldPos = new Vector3();
                VoxelMod modData = new VoxelMod();
                
                if (Input.GetMouseButtonDown(1)) {
                    blockWorldPos = hitInfo.point - _mainCamera.transform.forward * 0.05f;
                    modData = new VoxelMod(blockWorldPos, 3);
                    terrainGenerator.AddChunkModData(modData);
                }
            
                if (Input.GetMouseButtonDown(0)) {
                    blockWorldPos = hitInfo.point + _mainCamera.transform.forward * 0.05f;
                    modData = new VoxelMod(blockWorldPos, 0);
                    terrainGenerator.AddChunkModData(modData);
                }
                
                // update chunk next to the block placement
                Vector2 currentChunkCoord = terrainGenerator.GetChunkCoordByWorldPos(new Vector2(blockWorldPos.x, blockWorldPos.z));
                foreach (var direction in VoxelData.DirectionsAroundBlock) {
                    Vector3 surroundingBlock = blockWorldPos + direction;
                    Vector2 surroundingBlock2D = new Vector2(surroundingBlock.x, surroundingBlock.z);
                    Vector2 surroundingChunkCoord = terrainGenerator.GetChunkCoordByWorldPos(surroundingBlock2D);
                    
                    if (surroundingChunkCoord != currentChunkCoord) {
                        terrainGenerator.AddChunkModData(surroundingChunkCoord, modData);
                    }
                }
            }
        }
        
    }
}