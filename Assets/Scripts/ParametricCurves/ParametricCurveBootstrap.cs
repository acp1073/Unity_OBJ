using UnityEngine;
using UnityEngine.SceneManagement;

public static class ParametricCurveBootstrap
{
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void CreateDemo()
    {
        if (SceneManager.GetActiveScene().name != "ParametricCurveScene")
        {
            return;
        }

        if (Object.FindObjectOfType<ParametricCurveController>() == null)
        {
            GameObject demo = new GameObject("Parametric Curve Demo");
            demo.AddComponent<ParametricCurveController>();
        }
    }
}
