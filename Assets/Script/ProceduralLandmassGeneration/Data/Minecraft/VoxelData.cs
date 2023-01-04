using UnityEngine;

namespace ProceduralLandmassGeneration.Data.Minecraft {
    public static class VoxelData {
        public static readonly Vector3[] DirectionsAroundBlock = {
            new Vector3(0, 1, 0),
            new Vector3(0, -1, 0),
            new Vector3(0, 0, -1), //positive z direction
            new Vector3(0, 0, 1), //negative z direction
            new Vector3(1, 0, 0),
            new Vector3(-1, 0, 0),
        };

        public static readonly Vector3[] VerticesLocalPos = {
            new Vector3(0, 0, 0),
            new Vector3(1, 0, 0),
            new Vector3(1, 0, -1),
            new Vector3(0, 0, -1),

            new Vector3(0, 1, 0),
            new Vector3(1, 1, 0),
            new Vector3(1, 1, -1),
            new Vector3(0, 1, -1)
        };

        public static readonly byte[,] VerticesIndicesOfFace = {
            { 4, 5, 6, 7 }, //positive y direction
            { 1, 0, 3, 2 }, //negative y direction
            { 5, 4, 0, 1 }, //positive z direction
            { 7, 6, 2, 3 }, //negative z direction
            { 6, 5, 1, 2 }, //positive x direction
            { 4, 7, 3, 0 }  //negative x direction
        };
    }
}