using System.Collections.Generic;
using UnityEngine;

namespace ProceduralLandmassGeneration.Generator.Mesh {
    public class MeshData {
        // The negative numbers are the border vertices
        // -1  -2  -3  -4  -5
        // -6   0   1   2  -7
        // -8   3   4   5  -9
        // -10  6   7   8  -11
        // -12 -13 -14 -15 -16
        private Vector3[] _vertices;
        private readonly int[] _triangles;
        private Vector2[] _uvs;
        private Vector3[] _bakedNormalsArray;
        
        List<Vector3> verts = new List<Vector3>();
        List<int> tris = new List<int>();
        List<Vector2> uvs = new List<Vector2>();

        // outOfMeshVertices only contains negative value
        private readonly Vector3[] _outOfMeshVertices;

        // outOfMeshTriangles consists of vertices that make up the border triangle
        // can contain both negative and positive value
        private readonly int[] _outOfMeshTriangles;

        private int _triangleIndex;
        private int _outOfMeshTriangleIndex;

        private readonly bool _useFlatShading;

        public MeshData(List<Vector3> verts, List<int> tris) {
            this.verts = verts;
            this.tris = tris;
        }
        
        public MeshData(int numVertsPerLine, int skipIncrement, bool useFlatShading) {
            _useFlatShading = useFlatShading;

            int numMeshEdgeVertices = (numVertsPerLine - 2) * 4 - 4;
            int numEdgeConnectionVertices = (skipIncrement - 1) * (numVertsPerLine - 5) / skipIncrement * 4;
            int numMainVerticesPerLine = (numVertsPerLine - 5) / skipIncrement + 1;
            int numMainVertices = numMainVerticesPerLine * numMainVerticesPerLine;

            _vertices = new Vector3[numMeshEdgeVertices + numEdgeConnectionVertices + numMainVertices];
            _uvs = new Vector2[_vertices.Length];

            int numMeshEdgeTriangles = 8 * (numVertsPerLine - 4);
            int numMainTriangles = (numMainVerticesPerLine - 1) * (numMainVerticesPerLine - 1) * 2;
            _triangles = new int[(numMeshEdgeTriangles + numMainTriangles) * 3];


            _outOfMeshVertices = new Vector3[numVertsPerLine * 4 - 4];
            _outOfMeshTriangles = new int[24 * (numVertsPerLine - 2)]; // this is the final formula
        }

        public void AddVertex(Vector3 vertexPosition, Vector2 uv, int vertexIndex) {
            if (vertexIndex < 0) {
                _outOfMeshVertices[-vertexIndex - 1] = vertexPosition;
            } else {
                _vertices[vertexIndex] = vertexPosition;
                _uvs[vertexIndex] = uv;
            }
        }

        public void AddTriangle(int a, int b, int c) {
            if (a < 0 || b < 0 || c < 0) {
                _outOfMeshTriangles[_outOfMeshTriangleIndex] = a;
                _outOfMeshTriangles[_outOfMeshTriangleIndex + 1] = b;
                _outOfMeshTriangles[_outOfMeshTriangleIndex + 2] = c;
                _outOfMeshTriangleIndex += 3;
            } else {
                _triangles[_triangleIndex] = a;
                _triangles[_triangleIndex + 1] = b;
                _triangles[_triangleIndex + 2] = c;
                _triangleIndex += 3;
            }
        }

        private Vector3[] CalculateNormals() {
            Vector3[] vertexNormals = new Vector3[_vertices.Length];

            CalculateNormalInside();

            CalculateNormalAtTheEdge();

            for (int i = 0; i < vertexNormals.Length; i++) {
                vertexNormals[i].Normalize();
            }

            return vertexNormals;

            void CalculateNormalInside() {
                int triangleCount = _triangles.Length / 3;
                for (int i = 0; i < triangleCount; i++) {
                    int normalTriangleIndex = i * 3;
                    int vertexIndexA = _triangles[normalTriangleIndex];
                    int vertexIndexB = _triangles[normalTriangleIndex + 1];
                    int vertexIndexC = _triangles[normalTriangleIndex + 2];

                    Vector3 triangleNormal = SurfaceNormalFromIndices(vertexIndexA, vertexIndexB, vertexIndexC);
                    vertexNormals[vertexIndexA] += triangleNormal;
                    vertexNormals[vertexIndexB] += triangleNormal;
                    vertexNormals[vertexIndexC] += triangleNormal;
                }
            }

            void CalculateNormalAtTheEdge() {
                int borderTriangleCount = _outOfMeshTriangles.Length / 3;
                for (int i = 0; i < borderTriangleCount; i++) {
                    int normalTriangleIndex = i * 3;
                    int vertexIndexA = _outOfMeshTriangles[normalTriangleIndex];
                    int vertexIndexB = _outOfMeshTriangles[normalTriangleIndex + 1];
                    int vertexIndexC = _outOfMeshTriangles[normalTriangleIndex + 2];

                    Vector3 triangleNormal = SurfaceNormalFromIndices(vertexIndexA, vertexIndexB, vertexIndexC);
                    if (vertexIndexA >= 0) {
                        vertexNormals[vertexIndexA] += triangleNormal;
                    }

                    if (vertexIndexB >= 0) {
                        vertexNormals[vertexIndexB] += triangleNormal;
                    }

                    if (vertexIndexC >= 0) {
                        vertexNormals[vertexIndexC] += triangleNormal;
                    }
                }
            }
        }

        private Vector3 SurfaceNormalFromIndices(int indexA, int indexB, int indexC) {
            Vector3 pointA = (indexA < 0) ? _outOfMeshVertices[-indexA - 1] : _vertices[indexA];
            Vector3 pointB = (indexB < 0) ? _outOfMeshVertices[-indexB - 1] : _vertices[indexB];
            Vector3 pointC = (indexC < 0) ? _outOfMeshVertices[-indexC - 1] : _vertices[indexC];

            Vector3 sideAb = pointB - pointA;
            Vector3 sideAc = pointC - pointA;
            return Vector3.Cross(sideAb, sideAc).normalized;
        }

        public void ProcessMesh() {
            if (_useFlatShading) {
                FlatShading();
            } else {
                BakeNormals();
            }
        }

        private void BakeNormals() {
            _bakedNormalsArray = CalculateNormals();
        }

        private void FlatShading() {
            Vector3[] flatShadedVertices = new Vector3[_triangles.Length];
            Vector2[] flatShadedUvs = new Vector2[_triangles.Length];

            for (int i = 0; i < _triangles.Length; i++) {
                flatShadedVertices[i] = _vertices[_triangles[i]];
                flatShadedUvs[i] = _uvs[_triangles[i]];
                _triangles[i] = i;
            }

            _vertices = flatShadedVertices;
            _uvs = flatShadedUvs;
        }

        // public UnityEngine.Mesh CreateMesh() {
        //     UnityEngine.Mesh mesh = new UnityEngine.Mesh {
        //         vertices = _vertices,
        //         triangles = _triangles,
        //         uv = _uvs
        //     };
        //     
        //     if (_useFlatShading) {
        //         mesh.RecalculateNormals();
        //     } else {
        //         mesh.normals = _bakedNormalsArray;
        //     }
        //
        //     return mesh;
        // }
        
        public UnityEngine.Mesh CreateMesh() {
            UnityEngine.Mesh mesh = new UnityEngine.Mesh {
                vertices = verts.ToArray(),
                triangles = tris.ToArray()
            };

            mesh.RecalculateNormals();
            return mesh;
        }
    }
}