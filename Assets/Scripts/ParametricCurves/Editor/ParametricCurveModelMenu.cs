using UnityEditor;
using UnityEngine;

public static class ParametricCurveModelMenu
{
    [MenuItem("Tools/Parametric Curves/Create Curve Object")]
    [MenuItem("GameObject/Parametric Curves/Curve Object", false, 10)]
    private static void CreateCurveObject()
    {
        GameObject curveObject = new GameObject(
            "Parametric Curve",
            typeof(LineRenderer),
            typeof(ParametricCurveModel));

        Undo.RegisterCreatedObjectUndo(curveObject, "Create Parametric Curve");
        Selection.activeGameObject = curveObject;
        EditorGUIUtility.PingObject(curveObject);
    }
}
