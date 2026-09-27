using System.Collections.Generic;
using UnityEngine;

[ExecuteAlways]
[RequireComponent(typeof(LineRenderer))]
public sealed class ParametricCurveModel : MonoBehaviour
{
    [Header("Curve")]
    [SerializeField]
    private ParametricCurveType curveType = ParametricCurveType.Bezier;

    [SerializeField, Min(8)]
    [Tooltip("Number of line segments used to preview the curve.")]
    private int sampleCount = 128;

    [SerializeField]
    private bool showControlPolygon = true;

    [Header("Control Points")]
    [SerializeField]
    private List<Vector3> controlPoints = new List<Vector3>
    {
        new Vector3(-4f, -2f, 0f),
        new Vector3(-2f, 3f, 0f),
        new Vector3(2f, -3f, 0f),
        new Vector3(4f, 2f, 0f)
    };

    [System.NonSerialized]
    private LineRenderer lineRenderer;

    [System.NonSerialized]
    private Material previewMaterial;

    public ParametricCurveType CurveType
    {
        get { return curveType; }
        set
        {
            curveType = value;
            Rebuild();
        }
    }

    public int SampleCount
    {
        get { return sampleCount; }
        set
        {
            sampleCount = Mathf.Max(8, value);
            Rebuild();
        }
    }

    public bool ShowControlPolygon
    {
        get { return showControlPolygon; }
        set { showControlPolygon = value; }
    }

    public List<Vector3> ControlPoints
    {
        get { return controlPoints; }
    }

    private void OnEnable()
    {
        EnsureLineRenderer();
        Rebuild();
    }

    private void OnValidate()
    {
        EnsureLineRenderer();

        if (controlPoints == null)
        {
            controlPoints = new List<Vector3>();
        }

        while (controlPoints.Count < 2)
        {
            controlPoints.Add(controlPoints.Count == 0 ? Vector3.zero : Vector3.right);
        }

        sampleCount = Mathf.Max(8, sampleCount);
        Rebuild();
    }

    private void OnDisable()
    {
        ReleasePreviewMaterial();
    }

    private void OnDestroy()
    {
        ReleasePreviewMaterial();
    }

    public void Rebuild()
    {
        EnsureLineRenderer();
        if (lineRenderer == null || controlPoints == null || controlPoints.Count < 2)
        {
            return;
        }

        List<Vector3> samples = ParametricCurveMath.Sample(curveType, controlPoints, sampleCount);
        lineRenderer.useWorldSpace = false;
        lineRenderer.positionCount = samples.Count;
        lineRenderer.SetPositions(samples.ToArray());
        lineRenderer.widthMultiplier = 0.06f;
        lineRenderer.numCapVertices = 4;
        lineRenderer.numCornerVertices = 4;
        lineRenderer.startColor = new Color(0.18f, 0.78f, 1f, 1f);
        lineRenderer.endColor = new Color(0.18f, 0.78f, 1f, 1f);
    }

    public void ConvertTo(ParametricCurveType targetType)
    {
        if (controlPoints == null || controlPoints.Count < 2)
        {
            return;
        }

        controlPoints = ParametricCurveMath.Convert(
            curveType,
            controlPoints,
            targetType,
            controlPoints.Count);
        curveType = targetType;
        Rebuild();
    }

    public void ResetDefaultPoints()
    {
        controlPoints = new List<Vector3>
        {
            new Vector3(-4f, -2f, 0f),
            new Vector3(-2f, 3f, 0f),
            new Vector3(2f, -3f, 0f),
            new Vector3(4f, 2f, 0f)
        };
        curveType = ParametricCurveType.Bezier;
        Rebuild();
    }

    public void AddControlPoint()
    {
        Vector3 point = controlPoints.Count == 0
            ? Vector3.zero
            : controlPoints[controlPoints.Count - 1] + new Vector3(1f, 0.5f, 0f);
        controlPoints.Add(point);
        Rebuild();
    }

    public void RemoveLastControlPoint()
    {
        if (controlPoints.Count <= 2)
        {
            return;
        }

        controlPoints.RemoveAt(controlPoints.Count - 1);
        Rebuild();
    }

    private void EnsureLineRenderer()
    {
        if (lineRenderer == null)
        {
            lineRenderer = GetComponent<LineRenderer>();
        }

        if (lineRenderer == null)
        {
            return;
        }

        lineRenderer.loop = false;
        lineRenderer.alignment = LineAlignment.TransformZ;
        lineRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        lineRenderer.receiveShadows = false;

        if (lineRenderer.sharedMaterial == null)
        {
            Shader shader = Shader.Find("Sprites/Default");
            if (shader != null)
            {
                previewMaterial = new Material(shader);
                previewMaterial.name = "Parametric Curve Preview Material";
                previewMaterial.hideFlags = HideFlags.HideAndDontSave;
                lineRenderer.sharedMaterial = previewMaterial;
            }
        }
    }

    private void ReleasePreviewMaterial()
    {
        if (previewMaterial == null)
        {
            return;
        }

        if (lineRenderer != null && lineRenderer.sharedMaterial == previewMaterial)
        {
            lineRenderer.sharedMaterial = null;
        }

        if (Application.isPlaying)
        {
            Destroy(previewMaterial);
        }
        else
        {
            DestroyImmediate(previewMaterial);
        }

        previewMaterial = null;
    }
}
