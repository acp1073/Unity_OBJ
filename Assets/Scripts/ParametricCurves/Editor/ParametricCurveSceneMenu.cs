#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine.SceneManagement;

public static class ParametricCurveSceneMenu
{
    private const string ScenePath = "Assets/Scenes/ParametricCurveScene.unity";

    [MenuItem("Tools/Parametric Curves/Open Demo Scene")]
    private static void OpenDemoScene()
    {
        Scene scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
        if (!scene.IsValid())
        {
            EditorUtility.DisplayDialog("Parametric Curves", "Demo scene not found. Use Create Demo Scene first.", "OK");
        }
    }

    [MenuItem("Tools/Parametric Curves/Create Demo Scene")]
    public static void CreateDemoScene()
    {
        if (AssetDatabase.LoadAssetAtPath<SceneAsset>(ScenePath) != null)
        {
            EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            return;
        }

        Scene scene = EditorSceneManager.NewScene(NewSceneSetup.DefaultGameObjects, NewSceneMode.Single);
        EditorSceneManager.SaveScene(scene, ScenePath);
        AssetDatabase.Refresh();
        EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
    }
}
#endif
