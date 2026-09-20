using UnityEngine;

public static class ObjViewerBootstrap
{
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void CreateViewer()
    {
        if (Object.FindObjectOfType<ObjViewerController>() != null)
        {
            return;
        }

        GameObject viewer = new GameObject("OBJ Viewer");
        Object.DontDestroyOnLoad(viewer);
        viewer.AddComponent<ObjViewerController>();
    }
}
