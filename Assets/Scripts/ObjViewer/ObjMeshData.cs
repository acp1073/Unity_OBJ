using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Intermediate representation of an OBJ file.
/// The source vertex list is intentionally kept separate from the Unity Mesh
/// so future tools can work with the original OBJ vertex indices directly.
/// </summary>
[Serializable]
public sealed class ObjMeshData
{
    public readonly List<Vector3> Positions = new List<Vector3>();
    public readonly List<Vector2> UVs = new List<Vector2>();
    public readonly List<Vector3> Normals = new List<Vector3>();
    public readonly List<ObjFace> Faces = new List<ObjFace>();

    public string ObjectName;

    public int SourceVertexCount
    {
        get { return Positions.Count; }
    }

    public int TriangleCount
    {
        get
        {
            int count = 0;
            for (int i = 0; i < Faces.Count; i++)
            {
                count += Math.Max(0, Faces[i].Vertices.Count - 2);
            }

            return count;
        }
    }
}

[Serializable]
public sealed class ObjFace
{
    public readonly List<ObjFaceVertex> Vertices = new List<ObjFaceVertex>();
    public int SmoothingGroup;
}

[Serializable]
public struct ObjFaceVertex
{
    // OBJ indices are converted to zero-based indices by ObjParser.
    public int PositionIndex;
    public int UVIndex;
    public int NormalIndex;

    public ObjFaceVertex(int positionIndex, int uvIndex, int normalIndex)
    {
        PositionIndex = positionIndex;
        UVIndex = uvIndex;
        NormalIndex = normalIndex;
    }
}
