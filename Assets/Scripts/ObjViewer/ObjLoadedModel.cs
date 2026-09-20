using UnityEngine;
using UnityEngine.Rendering;

/// <summary>
/// Runtime model handle exposed to future features.
/// SourceVertices are the exact zero-based OBJ `v` records, while UnityMesh is
/// the renderable mesh generated from the parsed faces.
/// </summary>
public sealed class ObjLoadedModel : MonoBehaviour
{
    [SerializeField] private Vector3[] sourceVertices = new Vector3[0];
    [SerializeField] private Vector2[] sourceUVs = new Vector2[0];
    [SerializeField] private Vector3[] sourceNormals = new Vector3[0];
    [SerializeField] private int[] sourceTriangleIndices = new int[0];
    [SerializeField] private int[] renderVertexSourceIndices = new int[0];
    [System.NonSerialized] private Mesh runtimeMesh;

    public Vector3[] SourceVertices
    {
        get { return sourceVertices; }
    }

    public int[] SourceTriangleIndices
    {
        get { return sourceTriangleIndices; }
    }

    public Vector2[] SourceUVs
    {
        get { return sourceUVs; }
    }

    public Vector3[] SourceNormals
    {
        get { return sourceNormals; }
    }

    public int[] RenderVertexSourceIndices
    {
        get { return renderVertexSourceIndices; }
    }

    public Mesh UnityMesh
    {
        get
        {
            if (runtimeMesh != null)
            {
                return runtimeMesh;
            }

            MeshFilter filter = GetComponent<MeshFilter>();
            return filter == null ? null : filter.sharedMesh;
        }
    }

    public ObjMeshData ParsedData { get; private set; }

    private void Awake()
    {
        ConfigureViewerMaterial();
    }

    private void OnValidate()
    {
        ConfigureViewerMaterial();
    }

    public void Initialize(ObjMeshData data, Mesh mesh)
    {
        Initialize(data, mesh, null);
    }

    public void Initialize(ObjMeshData data, Mesh mesh, int[] sourceIndexMap)
    {
        ParsedData = data;
        runtimeMesh = mesh;
        sourceVertices = data.Positions.ToArray();
        sourceUVs = data.UVs.ToArray();
        sourceNormals = data.Normals.ToArray();

        int[] triangles = new int[data.TriangleCount * 3];
        int cursor = 0;
        for (int faceIndex = 0; faceIndex < data.Faces.Count; faceIndex++)
        {
            ObjFace face = data.Faces[faceIndex];
            ObjFaceVertex first = face.Vertices[0];
            for (int i = 1; i < face.Vertices.Count - 1; i++)
            {
                ObjFaceVertex second = face.Vertices[i];
                ObjFaceVertex third = face.Vertices[i + 1];
                triangles[cursor++] = first.PositionIndex;
                triangles[cursor++] = third.PositionIndex;
                triangles[cursor++] = second.PositionIndex;
            }
        }

        sourceTriangleIndices = triangles;
        renderVertexSourceIndices = sourceIndexMap ?? new int[0];
        ConfigureViewerMaterial();
    }

    private void ConfigureViewerMaterial()
    {
        Renderer renderer = GetComponent<Renderer>();
        Material material = renderer == null ? null : renderer.sharedMaterial;
        if (material == null)
        {
            return;
        }

        // Old scene objects may still reference the built-in Standard shader.
        // Upgrade viewer-owned legacy materials in the editor, and use a
        // runtime instance when playing, so the viewer is always double-sided.
        Shader doubleSidedShader = Shader.Find("OBJ Viewer/DoubleSidedLit");
        bool isLegacyViewerShader = material.shader != null &&
            (material.shader.name == "Standard" || material.shader.name == "Universal Render Pipeline/Lit");

        if (doubleSidedShader != null && material.shader != doubleSidedShader &&
            (Application.isPlaying || isLegacyViewerShader))
        {
            bool useRuntimeInstance = Application.isPlaying;
            Material targetMaterial = useRuntimeInstance ?
                new Material(doubleSidedShader) : material;
            targetMaterial.name = useRuntimeInstance ?
                material.name + " (Runtime Double Sided)" : material.name;

            Color color = material.HasProperty("_Color") ?
                material.GetColor("_Color") : Color.white;
            Texture mainTexture = material.HasProperty("_MainTex") ?
                material.GetTexture("_MainTex") : null;

            targetMaterial.shader = doubleSidedShader;
            if (targetMaterial.HasProperty("_Color"))
            {
                targetMaterial.SetColor("_Color", color);
            }

            if (mainTexture != null && targetMaterial.HasProperty("_MainTex"))
            {
                targetMaterial.SetTexture("_MainTex", mainTexture);
            }

            if (useRuntimeInstance)
            {
                renderer.material = targetMaterial;
                material = targetMaterial;
            }
#if UNITY_EDITOR
            else
            {
                UnityEditor.EditorUtility.SetDirty(targetMaterial);
            }
#endif
        }

        if (material.HasProperty("_Cull"))
        {
            // A viewer should remain inspectable even when an OBJ's face
            // winding differs from Unity's convention.
            material.SetInt("_Cull", (int)CullMode.Off);
        }
    }
}
