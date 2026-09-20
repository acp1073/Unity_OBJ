using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using UnityEngine;

/// <summary>
/// Small, dependency-free OBJ reader for runtime loading.
/// It supports v/vt/vn/f, negative indices, polygon triangulation and comments.
/// Materials and smoothing groups are intentionally ignored for now.
/// </summary>
public static class ObjParser
{
    private static readonly CultureInfo Invariant = CultureInfo.InvariantCulture;

    public static ObjMeshData ParseFile(string path)
    {
        if (!File.Exists(path))
        {
            throw new FileNotFoundException("OBJ 文件不存在。", path);
        }

        using (StreamReader reader = new StreamReader(path))
        {
            return Parse(reader, Path.GetFileNameWithoutExtension(path));
        }
    }

    public static ObjMeshData Parse(TextReader reader, string objectName)
    {
        ObjMeshData data = new ObjMeshData();
        data.ObjectName = objectName;

        string line;
        int lineNumber = 0;
        int currentSmoothingGroup = 0;
        while ((line = reader.ReadLine()) != null)
        {
            lineNumber++;
            line = line.Trim();
            if (line.Length == 0 || line.StartsWith("#", StringComparison.Ordinal))
            {
                continue;
            }

            string[] tokens = line.Split(new[] { ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries);
            if (tokens.Length == 0)
            {
                continue;
            }

            try
            {
                switch (tokens[0])
                {
                    case "v":
                        Require(tokens, 4, lineNumber);
                        data.Positions.Add(new Vector3(
                            ParseFloat(tokens[1]),
                            ParseFloat(tokens[2]),
                            ParseFloat(tokens[3])));
                        break;

                    case "vt":
                        Require(tokens, 3, lineNumber);
                        data.UVs.Add(new Vector2(ParseFloat(tokens[1]), ParseFloat(tokens[2])));
                        break;

                    case "vn":
                        Require(tokens, 4, lineNumber);
                        data.Normals.Add(new Vector3(
                            ParseFloat(tokens[1]),
                            ParseFloat(tokens[2]),
                            ParseFloat(tokens[3])));
                        break;

                    case "f":
                        Require(tokens, 4, lineNumber);
                        ObjFace face = new ObjFace();
                        face.SmoothingGroup = currentSmoothingGroup;
                        for (int i = 1; i < tokens.Length; i++)
                        {
                            face.Vertices.Add(ParseFaceVertex(tokens[i], data, lineNumber));
                        }

                        if (face.Vertices.Count >= 3)
                        {
                            data.Faces.Add(face);
                        }
                        break;

                    case "s":
                        if (tokens.Length < 2 || tokens[1].Equals("off", StringComparison.OrdinalIgnoreCase)
                            || tokens[1] == "0")
                        {
                            currentSmoothingGroup = 0;
                        }
                        else
                        {
                            int smoothingGroup;
                            currentSmoothingGroup = int.TryParse(
                                tokens[1], NumberStyles.Integer, Invariant, out smoothingGroup)
                                ? Mathf.Max(0, smoothingGroup)
                                : 0;
                        }
                        break;

                    case "o":
                    case "g":
                        if (tokens.Length > 1 && string.IsNullOrEmpty(data.ObjectName))
                        {
                            data.ObjectName = tokens[1];
                        }
                        break;

                    // mtllib, usemtl, s and l are valid OBJ statements but are
                    // not needed to construct the geometry viewer.
                    default:
                        break;
                }
            }
            catch (FormatException exception)
            {
                throw new FormatException("OBJ 第 " + lineNumber + " 行格式错误：" + line, exception);
            }
        }

        if (data.Positions.Count == 0)
        {
            throw new FormatException("OBJ 文件没有任何顶点（v）。");
        }

        if (data.Faces.Count == 0)
        {
            throw new FormatException("OBJ 文件没有可显示的面（f）。");
        }

        return data;
    }

    public static Mesh BuildUnityMesh(ObjMeshData data)
    {
        return BuildUnityMeshResult(data).Mesh;
    }

    public static ObjMeshBuildResult BuildUnityMeshResult(ObjMeshData data)
    {
        List<int> triangles = new List<int>(data.TriangleCount * 3);
        List<Vector3> renderVertices = new List<Vector3>();
        List<Vector2> renderUVs = new List<Vector2>();
        List<Vector3> renderNormals = new List<Vector3>();
        List<int> renderVertexSourceIndices = new List<int>();
        Dictionary<RenderVertexKey, int> vertexMap = new Dictionary<RenderVertexKey, int>();
        bool hasAnyUV = false;
        bool hasCompleteNormals = true;

        for (int faceIndex = 0; faceIndex < data.Faces.Count; faceIndex++)
        {
            ObjFace face = data.Faces[faceIndex];
            List<int> faceIndices = new List<int>(face.Vertices.Count);

            for (int i = 0; i < face.Vertices.Count; i++)
            {
                ObjFaceVertex vertex = face.Vertices[i];
                int normalKey = vertex.NormalIndex;
                if (normalKey < 0)
                {
                    // Explicit vn values define the split. Without vn, honor
                    // OBJ smoothing groups; with smoothing off, keep each face
                    // flat by giving it a unique normal key.
                    normalKey = face.SmoothingGroup > 0
                        ? -2 - face.SmoothingGroup
                        : int.MinValue + faceIndex;
                }

                RenderVertexKey key = new RenderVertexKey(
                    vertex.PositionIndex,
                    vertex.UVIndex,
                    normalKey);

                int renderIndex;
                if (!vertexMap.TryGetValue(key, out renderIndex))
                {
                    renderIndex = renderVertices.Count;
                    vertexMap.Add(key, renderIndex);
                    renderVertices.Add(data.Positions[vertex.PositionIndex]);
                    renderVertexSourceIndices.Add(vertex.PositionIndex);

                    if (vertex.UVIndex >= 0)
                    {
                        renderUVs.Add(data.UVs[vertex.UVIndex]);
                        hasAnyUV = true;
                    }
                    else
                    {
                        renderUVs.Add(Vector2.zero);
                    }

                    if (vertex.NormalIndex >= 0)
                    {
                        renderNormals.Add(data.Normals[vertex.NormalIndex]);
                    }
                    else
                    {
                        renderNormals.Add(Vector3.zero);
                        hasCompleteNormals = false;
                    }
                }
                else
                {
                    if (vertex.NormalIndex < 0)
                    {
                        hasCompleteNormals = false;
                    }
                }

                faceIndices.Add(renderIndex);
            }

            for (int i = 1; i < faceIndices.Count - 1; i++)
            {
                // Reverse winding when moving from OBJ's right-handed convention
                // to Unity's front-face convention.
                triangles.Add(faceIndices[0]);
                triangles.Add(faceIndices[i + 1]);
                triangles.Add(faceIndices[i]);
            }
        }

        Mesh mesh = new Mesh();
        mesh.name = string.IsNullOrEmpty(data.ObjectName) ? "OBJ Model" : data.ObjectName;
        if (renderVertices.Count > 65535)
        {
            mesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
        }

        mesh.vertices = renderVertices.ToArray();
        mesh.triangles = triangles.ToArray();
        if (hasAnyUV)
        {
            mesh.uv = renderUVs.ToArray();
        }

        if (hasCompleteNormals)
        {
            mesh.normals = renderNormals.ToArray();
        }
        else
        {
            mesh.RecalculateNormals();
        }

        mesh.RecalculateBounds();
        return new ObjMeshBuildResult(mesh, renderVertexSourceIndices.ToArray());
    }

    private struct RenderVertexKey : IEquatable<RenderVertexKey>
    {
        private readonly int positionIndex;
        private readonly int uvIndex;
        private readonly int normalIndex;

        public RenderVertexKey(int positionIndex, int uvIndex, int normalIndex)
        {
            this.positionIndex = positionIndex;
            this.uvIndex = uvIndex;
            this.normalIndex = normalIndex;
        }

        public bool Equals(RenderVertexKey other)
        {
            return positionIndex == other.positionIndex
                   && uvIndex == other.uvIndex
                   && normalIndex == other.normalIndex;
        }

        public override bool Equals(object obj)
        {
            return obj is RenderVertexKey && Equals((RenderVertexKey)obj);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                int hash = positionIndex;
                hash = hash * 397 ^ uvIndex;
                hash = hash * 397 ^ normalIndex;
                return hash;
            }
        }
    }

    private static ObjFaceVertex ParseFaceVertex(string token, ObjMeshData data, int lineNumber)
    {
        string[] parts = token.Split('/');
        int positionIndex = ResolveIndex(parts, 0, data.Positions.Count, true, lineNumber);
        int uvIndex = ResolveIndex(parts, 1, data.UVs.Count, false, lineNumber);
        int normalIndex = ResolveIndex(parts, 2, data.Normals.Count, false, lineNumber);
        return new ObjFaceVertex(positionIndex, uvIndex, normalIndex);
    }

    private static int ResolveIndex(string[] parts, int partIndex, int count, bool required, int lineNumber)
    {
        if (partIndex >= parts.Length || string.IsNullOrEmpty(parts[partIndex]))
        {
            if (required)
            {
                throw new FormatException("第 " + lineNumber + " 行缺少顶点索引。");
            }

            return -1;
        }

        int objIndex;
        if (!int.TryParse(parts[partIndex], NumberStyles.Integer, Invariant, out objIndex) || objIndex == 0)
        {
            throw new FormatException("第 " + lineNumber + " 行包含无效索引。");
        }

        int resolved = objIndex > 0 ? objIndex - 1 : count + objIndex;
        if (resolved < 0 || resolved >= count)
        {
            throw new FormatException("第 " + lineNumber + " 行索引超出范围。");
        }

        return resolved;
    }

    private static float ParseFloat(string token)
    {
        return float.Parse(token, NumberStyles.Float, Invariant);
    }

    private static void Require(string[] tokens, int count, int lineNumber)
    {
        if (tokens.Length < count)
        {
            throw new FormatException("第 " + lineNumber + " 行参数不足。");
        }
    }
}

public sealed class ObjMeshBuildResult
{
    public Mesh Mesh { get; private set; }
    public int[] RenderVertexSourceIndices { get; private set; }

    public ObjMeshBuildResult(Mesh mesh, int[] renderVertexSourceIndices)
    {
        Mesh = mesh;
        RenderVertexSourceIndices = renderVertexSourceIndices;
    }
}
