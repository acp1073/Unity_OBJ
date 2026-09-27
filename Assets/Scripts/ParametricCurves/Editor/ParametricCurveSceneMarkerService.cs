using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

/// <summary>
/// Keeps visible, non-serialized Scene-view markers for the selected curve.
/// This is independent of the custom editor's OnSceneGUI callback, so points
/// remain visible even when that callback is not drawing handles.
/// </summary>
[InitializeOnLoad]
internal static class ParametricCurveSceneMarkerService
{
    private const string LegacyMarkerPrefix = "Curve Point Preview ";

    static ParametricCurveSceneMarkerService()
    {
        RemoveLegacyMarkers();
        SceneView.duringSceneGui += DrawSelectedCurveControls;
    }

    private static void DrawSelectedCurveControls(SceneView sceneView)
    {
        if (sceneView == null || sceneView.camera == null || EditorApplication.isPlayingOrWillChangePlaymode)
        {
            return;
        }

        GameObject selectedObject = Selection.activeGameObject;
        ParametricCurveModel model = selectedObject != null
            ? selectedObject.GetComponent<ParametricCurveModel>()
            : null;
        if (model == null || model.ControlPoints == null || model.ControlPoints.Count == 0)
        {
            return;
        }

        DrawCurvePreview(model);
        DrawControlPolygon(model);

        CompareFunction previousZTest = Handles.zTest;
        Handles.zTest = CompareFunction.Always;
        try
        {
            Event currentEvent = Event.current;
            for (int i = 0; i < model.ControlPoints.Count; i++)
            {
                Vector3 worldPosition = model.transform.TransformPoint(model.ControlPoints[i]);
                float size = Mathf.Max(HandleUtility.GetHandleSize(worldPosition) * 0.13f, 0.1f);
                int controlId = GUIUtility.GetControlID(
                    unchecked(model.GetInstanceID() * 397 ^ i),
                    FocusType.Passive);

                Handles.color = new Color(1f, 0.72f, 0.08f, 1f);
                EditorGUI.BeginChangeCheck();
                Vector3 movedPosition = Handles.FreeMoveHandle(
                    controlId,
                    worldPosition,
                    size,
                    Vector3.zero,
                    Handles.SphereHandleCap);
                if (EditorGUI.EndChangeCheck())
                {
                    Undo.RecordObject(model, "Move Curve Control Point");
                    model.ControlPoints[i] = model.transform.InverseTransformPoint(movedPosition);
                    model.Rebuild();
                    EditorUtility.SetDirty(model);
                    SceneView.RepaintAll();
                }

                if (currentEvent.type == EventType.Repaint)
                {
                    Handles.color = Color.black;
                    Handles.DrawWireDisc(worldPosition, sceneView.camera.transform.forward, size * 0.72f);
                }

                Handles.Label(worldPosition + Vector3.up * (size * 0.85f), "P" + i, EditorStyles.boldLabel);
            }
        }
        finally
        {
            Handles.zTest = previousZTest;
        }
    }

    private static void RemoveLegacyMarkers()
    {
        GameObject[] loadedObjects = Resources.FindObjectsOfTypeAll<GameObject>();
        for (int i = 0; i < loadedObjects.Length; i++)
        {
            GameObject candidate = loadedObjects[i];
            if (candidate != null
                && candidate.name.StartsWith(LegacyMarkerPrefix, System.StringComparison.Ordinal)
                && (candidate.hideFlags & HideFlags.DontSave) != 0)
            {
                Object.DestroyImmediate(candidate);
            }
        }
    }

    private static void DrawCurvePreview(ParametricCurveModel model)
    {
        var samples = ParametricCurveMath.Sample(
            model.CurveType,
            model.ControlPoints,
            Mathf.Max(8, model.SampleCount));

        Vector3[] worldSamples = new Vector3[samples.Count];
        for (int i = 0; i < samples.Count; i++)
        {
            worldSamples[i] = model.transform.TransformPoint(samples[i]);
        }

        Handles.color = new Color(0.15f, 0.8f, 1f, 1f);
        Handles.DrawAAPolyLine(3f, worldSamples);
    }

    private static void DrawControlPolygon(ParametricCurveModel model)
    {
        if (!model.ShowControlPolygon || model.ControlPoints.Count < 2)
        {
            return;
        }

        Vector3[] worldPoints = new Vector3[model.ControlPoints.Count];
        for (int i = 0; i < model.ControlPoints.Count; i++)
        {
            worldPoints[i] = model.transform.TransformPoint(model.ControlPoints[i]);
        }

        Handles.color = new Color(1f, 0.7f, 0.15f, 0.45f);
        Handles.DrawAAPolyLine(2f, worldPoints);
    }

}
