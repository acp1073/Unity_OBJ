using System;
using System.IO;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Makes dragging an OBJ from the Project window or Windows Explorer into the
/// Scene View use the same vertex-preserving importer as the menu command.
/// </summary>
[InitializeOnLoad]
public static class ObjSceneDragHandler
{
    static ObjSceneDragHandler()
    {
        SceneView.duringSceneGui += OnSceneGUI;
    }

    private static void OnSceneGUI(SceneView sceneView)
    {
        Event currentEvent = Event.current;
        if (currentEvent.type != EventType.DragUpdated && currentEvent.type != EventType.DragPerform)
        {
            return;
        }

        string objPath = FindDraggedObjPath();
        if (string.IsNullOrEmpty(objPath))
        {
            return;
        }

        DragAndDrop.visualMode = DragAndDropVisualMode.Copy;
        if (currentEvent.type == EventType.DragPerform)
        {
            DragAndDrop.AcceptDrag();
            ObjSceneImporter.ImportIntoCurrentScene(objPath);
        }

        currentEvent.Use();
    }

    private static string FindDraggedObjPath()
    {
        for (int i = 0; i < DragAndDrop.paths.Length; i++)
        {
            string path = DragAndDrop.paths[i];
            if (string.IsNullOrEmpty(path) || !path.EndsWith(".obj", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            if (path.StartsWith("Assets/", StringComparison.OrdinalIgnoreCase)
                || path.StartsWith("Assets\\", StringComparison.OrdinalIgnoreCase))
            {
                string projectRoot = Directory.GetParent(Application.dataPath).FullName;
                return Path.GetFullPath(Path.Combine(projectRoot, path));
            }

            if (Path.IsPathRooted(path) && File.Exists(path))
            {
                return path;
            }
        }

        return null;
    }
}
