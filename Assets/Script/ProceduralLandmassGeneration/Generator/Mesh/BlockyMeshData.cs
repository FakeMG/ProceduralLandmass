using System.Collections.Generic;
using UnityEngine;

namespace ProceduralLandmassGeneration.Generator.Mesh {
    public class BlockyMeshData : IMeshData {
        private readonly List<Vector3> _verts;
        private readonly List<int> _tris;
        private readonly List<Vector2> _uvs;
        
        public BlockyMeshData(List<Vector3> verts, List<int> tris, List<Vector2> uvs) {
            _verts = verts;
            _tris = tris;
            _uvs = uvs;
        }
        
        public UnityEngine.Mesh CreateMesh() {
            UnityEngine.Mesh mesh = new UnityEngine.Mesh {
                vertices = _verts.ToArray(),
                triangles = _tris.ToArray(),
                uv = _uvs.ToArray()
            };

            mesh.RecalculateNormals();
            return mesh;
        }
    }
}