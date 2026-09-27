using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(ParametricCurveModel))]
public sealed class ParametricCurveModelEditor : Editor
{
    private SerializedProperty curveTypeProperty;
    private SerializedProperty sampleCountProperty;
    private SerializedProperty showControlPolygonProperty;
    private SerializedProperty controlPointsProperty;
    private ParametricCurveType conversionTarget;

    private void OnEnable()
    {
        curveTypeProperty = serializedObject.FindProperty("curveType");
        sampleCountProperty = serializedObject.FindProperty("sampleCount");
        showControlPolygonProperty = serializedObject.FindProperty("showControlPolygon");
        controlPointsProperty = serializedObject.FindProperty("controlPoints");
    }

    public override void OnInspectorGUI()
    {
        ParametricCurveModel model = (ParametricCurveModel)target;
        serializedObject.Update();

        EditorGUILayout.LabelField("Parametric Curve", EditorStyles.boldLabel);
        EditorGUILayout.HelpBox(
            "在 Scene 视图中直接按住黄色 P 点拖动；滚轮缩放，右键旋转，中键平移。",
            MessageType.Info);
        EditorGUILayout.PropertyField(curveTypeProperty, new GUIContent("Curve Type"));
        EditorGUILayout.PropertyField(sampleCountProperty, new GUIContent("Sample Count"));
        EditorGUILayout.PropertyField(showControlPolygonProperty, new GUIContent("Show Control Polygon"));

        EditorGUILayout.Space(6f);
        EditorGUILayout.LabelField("Control Points", EditorStyles.boldLabel);
        for (int i = 0; i < controlPointsProperty.arraySize; i++)
        {
            SerializedProperty point = controlPointsProperty.GetArrayElementAtIndex(i);
            EditorGUILayout.PropertyField(point, new GUIContent("P" + i));
        }

        EditorGUILayout.BeginHorizontal();
        if (GUILayout.Button("Add Point"))
        {
            Undo.RecordObject(model, "Add Curve Control Point");
            model.AddControlPoint();
            EditorUtility.SetDirty(model);
            serializedObject.Update();
        }

        if (GUILayout.Button("Remove Last"))
        {
            Undo.RecordObject(model, "Remove Curve Control Point");
            model.RemoveLastControlPoint();
            EditorUtility.SetDirty(model);
            serializedObject.Update();
        }
        EditorGUILayout.EndHorizontal();

        EditorGUILayout.BeginHorizontal();
        if (GUILayout.Button("Reset"))
        {
            Undo.RecordObject(model, "Reset Parametric Curve");
            model.ResetDefaultPoints();
            EditorUtility.SetDirty(model);
            serializedObject.Update();
        }

        if (GUILayout.Button("Rebuild"))
        {
            model.Rebuild();
            EditorUtility.SetDirty(model);
            SceneView.RepaintAll();
        }
        EditorGUILayout.EndHorizontal();

        EditorGUILayout.Space(6f);
        conversionTarget = (ParametricCurveType)EditorGUILayout.EnumPopup("Convert To", conversionTarget);
        if (GUILayout.Button("Convert Curve"))
        {
            Undo.RecordObject(model, "Convert Parametric Curve");
            model.ConvertTo(conversionTarget);
            EditorUtility.SetDirty(model);
            serializedObject.Update();
            SceneView.RepaintAll();
        }

        serializedObject.ApplyModifiedProperties();
        if (GUI.changed)
        {
            model.Rebuild();
            EditorUtility.SetDirty(model);
            SceneView.RepaintAll();
        }
    }

}
