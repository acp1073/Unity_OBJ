using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

public static class GlobalDeformationSceneMenu
{
    public const string ScenePath = "Assets/Scenes/GlobalDeformationScene.unity";

    [MenuItem("Tools/Global Deformation/Open Demo Scene")]
    public static void OpenDemoScene()
    {
        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
        EnsureScene();
        EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
    }

    public static void EnsureScene()
    {
        EnsureMaterials();
        if (AssetDatabase.LoadAssetAtPath<SceneAsset>(ScenePath) == null)
        {
            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            EditorSceneManager.SaveScene(scene, ScenePath);
        }
        Scene target = SceneManager.GetSceneByPath(ScenePath);
        bool closeAfter = !target.IsValid() || !target.isLoaded;
        if (closeAfter) target = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Additive);
        if (!target.GetRootGameObjects().Any(root => root.GetComponentInChildren<GlobalDeformationController>() != null))
        {
            GameObject demo = new GameObject("Global Deformation Demo");
            SceneManager.MoveGameObjectToScene(demo, target);
            demo.AddComponent<GlobalDeformationController>();
            EditorSceneManager.SaveScene(target);
        }
        if (closeAfter) EditorSceneManager.CloseScene(target, true);
        EditorBuildSettingsScene[] scenes = EditorBuildSettings.scenes;
        if (!scenes.Any(s => s.path == ScenePath))
            EditorBuildSettings.scenes = scenes.Concat(new[] { new EditorBuildSettingsScene(ScenePath, true) }).ToArray();
        AssetDatabase.SaveAssets();
        UnityEditor.AssetDatabase.Refresh();
    }

    // Batch preparation uses a normal editor shutdown to persist Build Settings.
    public static void Prepare()
    {
        EnsureScene();
        EditorSceneManager.OpenScene(ScenePath);
        AssetDatabase.SaveAssets();
    }

    private static void EnsureMaterials()
    {
        // Resources references keep runtime-created mesh shaders in standalone builds.
        if (!AssetDatabase.IsValidFolder("Assets/Resources")) AssetDatabase.CreateFolder("Assets", "Resources");
        if (!AssetDatabase.IsValidFolder("Assets/Resources/GlobalDeformation"))
            AssetDatabase.CreateFolder("Assets/Resources", "GlobalDeformation");
        CreateMaterial("ModelMaterial", "OBJ Viewer/DoubleSidedLit");
        CreateMaterial("ControlMaterial", "Unlit/Color");
    }

    private static void CreateMaterial(string name, string shader)
    {
        string path = "Assets/Resources/GlobalDeformation/" + name + ".mat";
        if (AssetDatabase.LoadAssetAtPath<Material>(path) == null)
            AssetDatabase.CreateAsset(new Material(Shader.Find(shader)) { name = name }, path);
    }
}
