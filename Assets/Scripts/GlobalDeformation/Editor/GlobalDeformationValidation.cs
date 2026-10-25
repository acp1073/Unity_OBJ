using System;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

/// <summary>Run from the menu or Unity -batchmode -executeMethod GlobalDeformationValidation.Run.</summary>
[InitializeOnLoad]
public static class GlobalDeformationValidation
{
    private const string ActiveKey = "GlobalDeformationValidation.Active";
    private static readonly string Output = Environment.GetEnvironmentVariable("OBJ_DEFORMATION_OUTPUT") ??
        Path.GetFullPath("Temp/GlobalDeformationValidation");
    private static int frames;
    private static int stage;
    private static double deadline;
    private static string runtimeError;
    private static readonly StringBuilder results = new StringBuilder();

    static GlobalDeformationValidation()
    {
        EditorApplication.update += Tick;
        Application.logMessageReceived += (message, trace, type) =>
        {
            if (SessionState.GetBool(ActiveKey, false) && (type == LogType.Exception || type == LogType.Error))
                runtimeError = message + "\n" + trace;
        };
        deadline = EditorApplication.timeSinceStartup + 180;
    }

    [MenuItem("Tools/Global Deformation/Run Validation (enters Play Mode)")]
    public static void Run()
    {
        try
        {
            Directory.CreateDirectory(Output);
            results.Clear();
            CheckMath();
            File.WriteAllText(Path.Combine(Output, "validation.txt"), results.ToString());
            GlobalDeformationSceneMenu.EnsureScene();
            EditorSceneManager.OpenScene(GlobalDeformationSceneMenu.ScenePath);
            SessionState.SetBool(ActiveKey, true);
            frames = stage = 0;
            deadline = EditorApplication.timeSinceStartup + 180;
            EditorApplication.isPlaying = true;
        }
        catch (Exception exception) { Finish(exception); }
    }

    private static void CheckMath()
    {
        Bounds bounds = new Bounds(Vector3.zero, new Vector3(2, 4, 2));
        foreach (DeformationAxis axis in Enum.GetValues(typeof(DeformationAxis)))
        {
            Vector3 p = new Vector3(0.7f, 1.1f, -0.3f);
            Near(DeformationMath.FromAxis(DeformationMath.ToAxis(p, axis), axis), p, "Axis inverse " + axis);
            foreach (GlobalDeformationMode mode in Enum.GetValues(typeof(GlobalDeformationMode)))
                Near(DeformationMath.Evaluate(p, bounds, mode, axis, 0), p, "Zero identity " + mode + "/" + axis);
        }
        Near(DeformationMath.Evaluate(new Vector3(1, -2, 1), bounds, GlobalDeformationMode.Taper, DeformationAxis.Y, -0.5f),
            new Vector3(1, -2, 1), "Taper fixed bottom");
        Near(DeformationMath.Evaluate(new Vector3(1, 2, 1), bounds, GlobalDeformationMode.Taper, DeformationAxis.Y, -0.5f),
            new Vector3(0.5f, 2, 0.5f), "Taper top factor");
        Near(DeformationMath.Evaluate(new Vector3(1, 2, 0), bounds, GlobalDeformationMode.Twist, DeformationAxis.Y, 90),
            new Vector3(0, 2, 1), "Twist 90 degree top");
        Near(DeformationMath.Evaluate(new Vector3(1, -2, 0), bounds, GlobalDeformationMode.Twist, DeformationAxis.Y, 90),
            new Vector3(1, -2, 0), "Twist fixed bottom");
        float radius = 4 / (Mathf.PI * 0.5f);
        Near(DeformationMath.Evaluate(new Vector3(0, 2, 0), bounds, GlobalDeformationMode.Bend, DeformationAxis.Y, 90),
            new Vector3(radius, radius - 2, 0), "Circular bend centerline");
        Near(DeformationMath.Evaluate(new Vector3(1, -2, 0.2f), bounds, GlobalDeformationMode.Bend, DeformationAxis.Y, -90),
            new Vector3(1, -2, 0.2f), "Bend fixed bottom");
        Vector3[] rest = { bounds.min, bounds.max, Vector3.zero, new Vector3(0.3f, -0.7f, 0.6f) };
        Vector3[] output = new Vector3[rest.Length];
        for (int n = 2; n <= 6; n++)
        {
            FfdLattice lattice = new FfdLattice(rest, bounds, n);
            Require(lattice.Points.Length == n * n * n, "FFD n cubed n=" + n);
            Near(lattice.Points[0], bounds.min, "FFD min corner n=" + n);
            Near(lattice.Points[lattice.Points.Length - 1], bounds.max, "FFD max corner n=" + n);
            lattice.Evaluate(output);
            for (int i = 0; i < rest.Length; i++) Near(output[i], rest[i], "FFD identity n=" + n + " v=" + i);
            Vector3 offset = new Vector3(0.3f, -0.2f, 0.7f);
            for (int i = 0; i < lattice.Points.Length; i++) lattice.Points[i] += offset;
            lattice.Evaluate(output);
            for (int i = 0; i < rest.Length; i++) Near(output[i], rest[i] + offset, "FFD translation n=" + n + " v=" + i);
            lattice.Reset();
            Vector3 cornerOffset = new Vector3(0.8f, 0.4f, 0);
            lattice.Points[lattice.Points.Length - 1] += cornerOffset;
            lattice.Evaluate(output);
            Near(output[1], rest[1] + cornerOffset, "FFD displaced endpoint n=" + n);
            Near(output[2], cornerOffset * Mathf.Pow(0.5f, 3 * (n - 1)), "FFD center analytic weight n=" + n);
        }
        Vector3[] planar = { new Vector3(-1, -1, 0), new Vector3(1, 1, 0) };
        FfdLattice flat = new FfdLattice(planar, new Bounds(Vector3.zero, new Vector3(2, 2, 0)), 3);
        Vector3[] flatOutput = new Vector3[2];
        flat.Evaluate(flatOutput);
        Near(flatOutput[0], planar[0], "Planar OBJ identity");
        Near(flatOutput[1], planar[1], "Planar OBJ identity 2");
    }

    private static void Tick()
    {
        if (!SessionState.GetBool(ActiveKey, false)) return;
        if (EditorApplication.timeSinceStartup > deadline) { Finish(new TimeoutException("Runtime validation timed out.")); return; }
        if (!EditorApplication.isPlaying || EditorApplication.isCompiling) return;
        GlobalDeformationController demo = UnityEngine.Object.FindObjectOfType<GlobalDeformationController>();
        if (++frames < 20) return;
        if (stage != 15 && (demo == null || demo.DisplayMesh == null)) return;
        try
        {
            if (runtimeError != null) throw new Exception(runtimeError);
            if (stage == 0)
            {
                results.Clear();
                results.Append(File.ReadAllText(Path.Combine(Output, "validation.txt")));
                CheckRuntime(demo);
                demo.LoadSample();
                demo.SetMode(GlobalDeformationMode.Taper);
                demo.SetAmount(-0.8f);
                demo.FocusModel();
                stage = 1;
                frames = 0;
            }
            else if (stage >= 1 && stage <= 4)
            {
                string[] names = { "", "taper", "twist", "bend", "ffd" };
                CapturePreview(demo, Path.Combine(Output, names[stage] + ".png"));
                stage += 10;
                frames = 0;
            }
            else if (stage == 11)
            {
                demo.SetMode(GlobalDeformationMode.Twist);
                demo.SetAmount(210);
                demo.FocusModel();
                stage = 2;
                frames = 0;
            }
            else if (stage == 12)
            {
                demo.SetMode(GlobalDeformationMode.Bend);
                demo.SetAmount(110);
                demo.FocusModel();
                stage = 3;
                frames = 0;
            }
            else if (stage == 13)
            {
                demo.SetMode(GlobalDeformationMode.FFD);
                for (int i = 0; i < 3; i++)
                    for (int k = 0; k < 3; k++)
                    {
                        int index = demo.Lattice.Index(i, 2, k);
                        demo.SetControlPoint(index, demo.Lattice.RestPoints[index] + new Vector3(1f, 0.4f, 0));
                    }
                demo.SelectPoint(demo.Lattice.Index(2, 2, 2));
                demo.FocusModel();
                stage = 4;
                frames = 0;
            }
            else if (stage == 14)
            {
                EditorSceneManager.LoadSceneInPlayMode("Assets/Scenes/SampleScene.unity",
                    new UnityEngine.SceneManagement.LoadSceneParameters(UnityEngine.SceneManagement.LoadSceneMode.Single));
                GameObject viewer = new GameObject("Validation OBJ Viewer");
                UnityEngine.Object.DontDestroyOnLoad(viewer);
                viewer.AddComponent<ObjViewerController>();
                stage = 15;
                frames = 0;
            }
            else if (stage == 15)
            {
                bool found = false;
                foreach (Button button in UnityEngine.Object.FindObjectsOfType<Button>())
                    if (button.name == "Deformation") { button.onClick.Invoke(); found = true; break; }
                Require(found, "OBJ viewer deformation entry button");
                stage = 16;
                frames = 0;
            }
            else if (stage == 16)
            {
                Require(UnityEngine.SceneManagement.SceneManager.GetActiveScene().name == GlobalDeformationController.SceneName,
                    "Viewer entry loads registered deformation scene");
                Require(UnityEngine.Object.FindObjectOfType<ObjViewerController>() == null, "Persistent viewer UI and model hidden on scene entry");
                File.WriteAllText(Path.Combine(Output, "validation.txt"), results + "PASS: Runtime smoke validation completed.\n");
                Finish(null);
            }
        }
        catch (Exception exception) { Finish(exception); }
    }

    private static void CheckRuntime(GlobalDeformationController demo)
    {
        Require(UnityEngine.Object.FindObjectOfType<ObjViewerController>() == null, "Dedicated scene has no overlapping OBJ viewer");
        Require(UnityEngine.Object.FindObjectsOfType<Camera>().Length == 1, "Dedicated camera");
        foreach (string file in new[] { "SampleCube.obj", "CustomPyramid.obj", "al.obj" })
        {
            string path = Path.Combine(Application.dataPath, "Models", file);
            if (!File.Exists(path)) continue;
            Require(demo.LoadModelFromPath(path), "Import " + file);
            Vector3[] baseline = demo.DisplayMesh.vertices;
            foreach (GlobalDeformationMode mode in new[] { GlobalDeformationMode.Taper, GlobalDeformationMode.Twist, GlobalDeformationMode.Bend })
                foreach (DeformationAxis axis in Enum.GetValues(typeof(DeformationAxis)))
                {
                    demo.SetMode(mode);
                    demo.SetAxis(axis);
                    demo.SetAmount(mode == GlobalDeformationMode.Taper ? -0.6f : 80f);
                    Vector3[] actual = demo.DisplayMesh.vertices;
                    float expectedMovement = 0;
                    for (int v = 0; v < baseline.Length; v++)
                    {
                        Vector3 expected = DeformationMath.Evaluate(baseline[v], demo.RestBounds, mode, axis, demo.Amount);
                        if (Vector3.Distance(expected, actual[v]) > 0.0002f)
                            throw new Exception("Analytic render mapping mismatch: " + file + " " + mode + "/" + axis);
                        expectedMovement = Mathf.Max(expectedMovement, Vector3.Distance(expected, baseline[v]));
                    }
                    Require(expectedMovement < 0.001f || MaxDistance(baseline, actual) > 0.001f,
                        "Analytic mesh mapping " + file + " " + mode + "/" + axis);
                    demo.ResetDeformation();
                    Require(MaxDistance(baseline, demo.DisplayMesh.vertices) < 0.0001f, "Rest pose restored " + mode + "/" + axis);
                }
            demo.SetMode(GlobalDeformationMode.FFD);
            for (int n = 2; n <= 6; n++)
            {
                demo.SetResolution(n);
                Require(MaxDistance(baseline, demo.DisplayMesh.vertices) < 0.0001f, "Runtime FFD identity " + file + " n=" + n);
                // Translation of the entire lattice must translate every render vertex, including OBJ seams.
                for (int i = 0; i < demo.Lattice.Points.Length; i++) demo.Lattice.Points[i] += Vector3.right * 0.4f;
                demo.SetControlPoint(0, demo.Lattice.Points[0]);
                Vector3[] moved = demo.DisplayMesh.vertices;
                for (int i = 0; i < moved.Length; i++)
                    if (Vector3.Distance(moved[i], baseline[i] + Vector3.right * 0.4f) > 0.0002f)
                        throw new Exception("FFD source/render seam map mismatch");
                Require(true, "Runtime FFD source/render mapping " + file + " n=" + n);
                demo.ResetDeformation();
            }
            Mesh before = demo.DisplayMesh;
            Require(!demo.LoadModelFromPath("missing-deformation-test.obj") && demo.DisplayMesh == before,
                "Failed load preserves displayed mesh");
        }
        demo.SetAxis(DeformationAxis.Y);
        demo.SetResolution(3);
        demo.LoadSample();
        // Exercise actual UI listeners, including mode switching and the slider.
        foreach (Button button in UnityEngine.Object.FindObjectsOfType<Button>(true))
            if (button.name == "弯曲 Bend") button.onClick.Invoke();
        Require(demo.Mode == GlobalDeformationMode.Bend, "Bend UI button listener");
        UnityEngine.Object.FindObjectOfType<Slider>().value = 65;
        Require(Mathf.Abs(demo.Amount - 65) < 0.001f, "Parameter UI slider listener");
        foreach (Button button in UnityEngine.Object.FindObjectsOfType<Button>(true))
            if (button.name == "FFD 自由变形") button.onClick.Invoke();
        Require(demo.Mode == GlobalDeformationMode.FFD, "FFD UI button listener");
    }

    private static float MaxDistance(Vector3[] a, Vector3[] b)
    {
        if (a.Length != b.Length) return float.PositiveInfinity;
        float max = 0;
        for (int i = 0; i < a.Length; i++) max = Mathf.Max(max, Vector3.Distance(a[i], b[i]));
        return max;
    }

    private static void CapturePreview(GlobalDeformationController demo, string path)
    {
        // Explicit GPU render also works in batch mode, where the Game view does not repaint.
        Canvas canvas = demo.GetComponentInChildren<Canvas>();
        Camera camera = demo.GetComponentInChildren<Camera>();
        RenderTexture target = new RenderTexture(1440, 900, 24);
        RenderTexture previousActive = RenderTexture.active;
        RenderTexture previousTarget = camera.targetTexture;
        Rect previousRect = camera.rect;
        int previousMask = camera.cullingMask;
        Transform[] children = canvas.GetComponentsInChildren<Transform>(true);
        int[] previousLayers = new int[children.Length];
        GameObject overlay = new GameObject("Validation UI Camera", typeof(Camera));
        Camera uiCamera = overlay.GetComponent<Camera>();
        Texture2D pixels = null;
        try
        {
            for (int i = 0; i < children.Length; i++) { previousLayers[i] = children[i].gameObject.layer; children[i].gameObject.layer = 5; }
            uiCamera.cullingMask = 1 << 5;
            uiCamera.clearFlags = CameraClearFlags.Depth;
            uiCamera.orthographic = true;
            uiCamera.nearClipPlane = 0.1f;
            uiCamera.farClipPlane = 10f;
            uiCamera.targetTexture = target;
            uiCamera.enabled = false;
            canvas.renderMode = RenderMode.ScreenSpaceCamera;
            canvas.worldCamera = uiCamera;
            canvas.planeDistance = 1;
            camera.targetTexture = target;
            camera.rect = new Rect(376f / 1440f, 0, 1f - 376f / 1440f, 1);
            camera.cullingMask &= ~(1 << 5);
            Canvas.ForceUpdateCanvases();
            RenderTexture.active = target;
            GL.Clear(true, true, new Color(0.025f, 0.035f, 0.055f));
            camera.Render();
            uiCamera.Render();
            pixels = new Texture2D(1440, 900, TextureFormat.RGB24, false);
            pixels.ReadPixels(new Rect(0, 0, 1440, 900), 0, 0);
            pixels.Apply();
            File.WriteAllBytes(path, pixels.EncodeToPNG());
        }
        finally
        {
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.worldCamera = null;
            camera.targetTexture = previousTarget;
            camera.rect = previousRect;
            camera.cullingMask = previousMask;
            for (int i = 0; i < children.Length; i++) children[i].gameObject.layer = previousLayers[i];
            RenderTexture.active = previousActive;
            UnityEngine.Object.DestroyImmediate(overlay);
            if (pixels != null) UnityEngine.Object.DestroyImmediate(pixels);
            target.Release();
            UnityEngine.Object.DestroyImmediate(target);
        }
    }

    private static void Near(Vector3 actual, Vector3 expected, string label)
    {
        Require(Vector3.Distance(actual, expected) < 0.0002f, label + " actual=" + actual + " expected=" + expected);
    }

    private static void Require(bool condition, string label)
    {
        if (!condition) throw new Exception("FAIL: " + label);
        results.AppendLine("PASS: " + label);
    }

    private static void Finish(Exception exception)
    {
        SessionState.SetBool(ActiveKey, false);
        if (exception != null)
        {
            Debug.LogException(exception);
            Directory.CreateDirectory(Output);
            File.WriteAllText(Path.Combine(Output, "validation.txt"), results + "FAIL: " + exception + "\n");
        }
        else Debug.Log("GLOBAL_DEFORMATION_VALIDATION_PASSED");
        if (Application.isBatchMode) EditorApplication.Exit(exception == null ? 0 : 1);
        else EditorApplication.isPlaying = false;
    }
}
