using System;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

/// <summary>
/// Editor-only importer that creates a persistent Mesh asset and a scene object.
/// The runtime viewer can then browse the imported object without loading a
/// second copy of the sample model.
/// </summary>
public static class ObjSceneImporter
{
    private const string GeneratedFolder = "Assets/GeneratedOBJ";

    [MenuItem("Tools/OBJ Viewer/Import OBJ Into Current Scene...")]
    private static void ImportFromDialog()
    {
        string sourcePath = EditorUtility.OpenFilePanel("选择 OBJ 文件", Application.dataPath, "obj");
        if (!string.IsNullOrEmpty(sourcePath))
        {
            ImportIntoCurrentScene(sourcePath);
        }
    }

    public static void ImportIntoCurrentScene(string sourcePath)
    {
        try
        {
            ObjMeshData data = ObjParser.ParseFile(sourcePath);
            ObjMeshBuildResult buildResult = ObjParser.BuildUnityMeshResult(data);
            Mesh mesh = buildResult.Mesh;
            mesh.name = string.IsNullOrEmpty(data.ObjectName)
                ? Path.GetFileNameWithoutExtension(sourcePath)
                : data.ObjectName;

            EnsureFolder(GeneratedFolder);
            string safeName = MakeSafeAssetName(mesh.name);
            string meshPath = AssetDatabase.GenerateUniqueAssetPath(GeneratedFolder + "/" + safeName + ".asset");
            AssetDatabase.CreateAsset(mesh, meshPath);

            Shader shader = Shader.Find("OBJ Viewer/DoubleSidedLit");
            if (shader == null)
            {
                shader = Shader.Find("Standard");
            }
            if (shader == null)
            {
                shader = Shader.Find("Universal Render Pipeline/Lit");
            }

            if (shader == null)
            {
                throw new InvalidOperationException("找不到可用的 Unity 材质 Shader。");
            }

            Material material = new Material(shader);
            material.name = safeName + " Material";
            material.color = new Color(0.38f, 0.68f, 0.95f, 1f);
            if (material.HasProperty("_Cull"))
            {
                material.SetInt("_Cull", (int)CullMode.Off);
            }
            string materialPath = AssetDatabase.GenerateUniqueAssetPath(GeneratedFolder + "/" + safeName + ".mat");
            AssetDatabase.CreateAsset(material, materialPath);
            AssetDatabase.SaveAssets();

            GameObject modelObject = new GameObject(mesh.name);
            Undo.RegisterCreatedObjectUndo(modelObject, "Import OBJ Into Scene");

            MeshFilter filter = modelObject.AddComponent<MeshFilter>();
            MeshRenderer renderer = modelObject.AddComponent<MeshRenderer>();
            ObjLoadedModel loadedModel = modelObject.AddComponent<ObjLoadedModel>();
            filter.sharedMesh = mesh;
            renderer.sharedMaterial = material;
            loadedModel.Initialize(data, mesh, buildResult.RenderVertexSourceIndices);

            // Keep source vertices untouched; only move the scene object so the
            // model's bounds center is at the world origin.
            modelObject.transform.position = -mesh.bounds.center;
            Selection.activeGameObject = modelObject;
            EditorGUIUtility.PingObject(modelObject);

            Scene activeScene = SceneManager.GetActiveScene();
            EditorSceneManager.MarkSceneDirty(activeScene);
            if (SceneView.lastActiveSceneView != null)
            {
                SceneView.lastActiveSceneView.FrameSelected();
            }

            Debug.Log("OBJ 已导入当前场景：" + modelObject.name
                      + "，顶点 " + data.SourceVertexCount
                      + "，三角形 " + data.TriangleCount);
            EditorUtility.DisplayDialog("OBJ 导入完成", "模型已添加到当前场景。请按 Ctrl+S 保存场景。", "确定");
        }
        catch (Exception exception)
        {
            Debug.LogException(exception);
            EditorUtility.DisplayDialog("OBJ 导入失败", exception.Message, "确定");
        }
    }

    private static void EnsureFolder(string folderPath)
    {
        if (AssetDatabase.IsValidFolder(folderPath))
        {
            return;
        }

        string parent = Path.GetDirectoryName(folderPath).Replace('\\', '/');
        string folderName = Path.GetFileName(folderPath);
        AssetDatabase.CreateFolder(parent, folderName);
    }

    private static string MakeSafeAssetName(string name)
    {
        foreach (char invalidCharacter in Path.GetInvalidFileNameChars())
        {
            name = name.Replace(invalidCharacter, '_');
        }

        return string.IsNullOrEmpty(name) ? "OBJModel" : name;
    }
}
