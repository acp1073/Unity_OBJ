using System;
using System.IO;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public sealed class ObjViewerController : MonoBehaviour
{
    private const string DefaultModelPath = "Assets/Models/SampleCube.obj";

    private Transform modelRoot;
    private ObjLoadedModel loadedModel;
    private ObjOrbitCamera orbitCamera;
    private InputField pathInput;
    private Text statusText;
    private Text statsText;
    private GameObject modelObject;
    private Material modelMaterial;
    private bool ownsLoadedModel;

    private void Awake()
    {
        EnsureEventSystem();
        SetupModelRoot();
        SetupCamera();
        BuildInterface();
    }

    private void Start()
    {
        ObjLoadedModel sceneModel = FindObjectOfType<ObjLoadedModel>();
        if (sceneModel != null && sceneModel.UnityMesh != null)
        {
            BindSceneModel(sceneModel);
            return;
        }

        pathInput.text = DefaultModelPath;
        LoadModel();
    }

    private void BindSceneModel(ObjLoadedModel sceneModel)
    {
        loadedModel = sceneModel;
        modelObject = sceneModel.gameObject;
        ownsLoadedModel = false;

        Renderer renderer = modelObject.GetComponent<Renderer>();
        if (renderer != null)
        {
            orbitCamera.Focus(renderer.bounds);
        }

        pathInput.text = "场景中的已导入模型";
        SetStatus("正在浏览场景模型：" + modelObject.name, false);
        if (statsText != null)
        {
            statsText.text = "顶点 " + sceneModel.SourceVertices.Length
                             + "   三角形 " + (sceneModel.SourceTriangleIndices.Length / 3);
        }
    }

    public void LoadModel()
    {
        string requestedPath = pathInput == null ? DefaultModelPath : pathInput.text.Trim();
        if (string.IsNullOrEmpty(requestedPath))
        {
            SetStatus("请输入 OBJ 文件路径。", true);
            return;
        }

        string resolvedPath = ResolvePath(requestedPath);
        if (!File.Exists(resolvedPath))
        {
            SetStatus("找不到文件：" + resolvedPath, true);
            return;
        }

        try
        {
            ObjMeshData data = ObjParser.ParseFile(resolvedPath);
            ObjMeshBuildResult buildResult = ObjParser.BuildUnityMeshResult(data);
            Mesh mesh = buildResult.Mesh;
            ReplaceModel(data, buildResult);
            SetStatus("已加载：" + Path.GetFileName(resolvedPath), false);
            if (statsText != null)
            {
                statsText.text = "顶点 " + data.SourceVertexCount + "   三角形 " + data.TriangleCount;
            }
        }
        catch (Exception exception)
        {
            SetStatus("加载失败：" + exception.Message, true);
        }
    }

    public void ClearModel()
    {
        if (modelObject != null && ownsLoadedModel)
        {
            if (loadedModel != null && loadedModel.UnityMesh != null)
            {
                Destroy(loadedModel.UnityMesh);
            }

            Destroy(modelObject);
            modelObject = null;
            loadedModel = null;
            ownsLoadedModel = false;
        }
        else if (modelObject != null)
        {
            // A scene-imported model belongs to the scene and must not be
            // destroyed by the runtime viewer's Clear button.
            modelObject = null;
            loadedModel = null;
            ownsLoadedModel = false;
        }

        if (statsText != null)
        {
            statsText.text = "顶点 0   三角形 0";
        }
        SetStatus("已清除模型。", false);
    }

    public void FocusModel()
    {
        Renderer renderer = modelObject == null ? null : modelObject.GetComponent<Renderer>();
        if (renderer == null)
        {
            SetStatus("当前没有可聚焦的模型。", true);
            return;
        }

        orbitCamera.Focus(renderer.bounds);
    }

    public void OpenParametricCurveDemo()
    {
        if (FindObjectOfType<ParametricCurveController>() == null)
        {
            GameObject curveDemo = new GameObject("Parametric Curve Demo");
            curveDemo.AddComponent<ParametricCurveController>();
        }

        // The curve demo provides an OBJ VIEWER button to return here.
        gameObject.SetActive(false);
    }

    public void ChooseFile()
    {
#if UNITY_EDITOR
        string selected = UnityEditor.EditorUtility.OpenFilePanel("选择 OBJ 文件", Application.dataPath, "obj");
        if (!string.IsNullOrEmpty(selected))
        {
            pathInput.text = selected;
            LoadModel();
        }
#else
        SetStatus("打包后请输入 OBJ 的绝对路径，或放入 StreamingAssets 后填写相对路径。", true);
#endif
    }

    private void ReplaceModel(ObjMeshData data, ObjMeshBuildResult buildResult)
    {
        ClearModel();

        Mesh mesh = buildResult.Mesh;

        modelObject = new GameObject(string.IsNullOrEmpty(data.ObjectName) ? "OBJ Model" : data.ObjectName);
        modelObject.transform.SetParent(modelRoot, false);
        MeshFilter filter = modelObject.AddComponent<MeshFilter>();
        Renderer renderer = modelObject.AddComponent<MeshRenderer>();
        loadedModel = modelObject.AddComponent<ObjLoadedModel>();
        ownsLoadedModel = true;
        filter.sharedMesh = mesh;
        renderer.sharedMaterial = GetModelMaterial();
        loadedModel.Initialize(data, mesh, buildResult.RenderVertexSourceIndices);

        Bounds bounds = mesh.bounds;
        float largestDimension = Mathf.Max(bounds.size.x, Mathf.Max(bounds.size.y, bounds.size.z));
        float scale = largestDimension > 0.0001f ? 4f / largestDimension : 1f;
        modelObject.transform.localPosition = -bounds.center;
        modelObject.transform.localScale = Vector3.one * scale;

        renderer = modelObject.GetComponent<Renderer>();
        orbitCamera.Focus(renderer.bounds);
    }

    private string ResolvePath(string requestedPath)
    {
        if (Path.IsPathRooted(requestedPath))
        {
            return requestedPath;
        }

        if (requestedPath.StartsWith("Assets/", StringComparison.OrdinalIgnoreCase)
            || requestedPath.StartsWith("Assets\\", StringComparison.OrdinalIgnoreCase))
        {
            string projectRoot = Directory.GetParent(Application.dataPath).FullName;
            return Path.Combine(projectRoot, requestedPath.Replace('/', Path.DirectorySeparatorChar));
        }

        return Path.Combine(Application.persistentDataPath, requestedPath);
    }

    private void SetupModelRoot()
    {
        GameObject root = new GameObject("Loaded OBJ Models");
        root.transform.SetParent(transform, false);
        modelRoot = root.transform;
    }

    private void SetupCamera()
    {
        Camera camera = Camera.main;
        if (camera == null)
        {
            GameObject cameraObject = new GameObject("OBJ Viewer Camera");
            camera = cameraObject.AddComponent<Camera>();
            cameraObject.AddComponent<AudioListener>();
            camera.tag = "MainCamera";
        }

        camera.clearFlags = CameraClearFlags.SolidColor;
        camera.backgroundColor = new Color(0.035f, 0.045f, 0.065f, 1f);
        camera.fieldOfView = 45f;
        camera.nearClipPlane = 0.01f;
        camera.farClipPlane = 5000f;
        orbitCamera = camera.GetComponent<ObjOrbitCamera>();
        if (orbitCamera == null)
        {
            orbitCamera = camera.gameObject.AddComponent<ObjOrbitCamera>();
        }
    }

    private void BuildInterface()
    {
        GameObject canvasObject = new GameObject("OBJ Viewer UI");
        canvasObject.transform.SetParent(transform, false);
        Canvas canvas = canvasObject.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        CanvasScaler scaler = canvasObject.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1280f, 720f);
        canvasObject.AddComponent<GraphicRaycaster>();

        GameObject panel = CreateUIObject("Control Panel", canvasObject.transform);
        Image panelImage = panel.AddComponent<Image>();
        panelImage.color = new Color(0.035f, 0.05f, 0.075f, 0.94f);
        CreateButton("Curves", panel.transform, "CURVES", new Vector2(360f, -122f), new Vector2(58f, 32f), OpenParametricCurveDemo);
        SetRect(panel, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(18f, -18f), new Vector2(430f, 170f), new Vector2(0f, 1f));

        CreateText("Title", panel.transform, "OBJ 模型浏览器", 22, Color.white, new Vector2(18f, -18f), new Vector2(394f, 34f), new Vector2(0f, 1f));
        CreateText("Hint", panel.transform, "右键旋转 · 中键平移 · 滚轮缩放", 13, new Color(0.65f, 0.72f, 0.82f), new Vector2(18f, -49f), new Vector2(394f, 24f), new Vector2(0f, 1f));

        pathInput = CreateInputField(panel.transform, "路径（可填绝对路径或 Assets/...）", new Vector2(18f, -80f), new Vector2(394f, 34f));
        CreateButton("Choose", panel.transform, "选择文件", new Vector2(18f, -122f), new Vector2(94f, 32f), ChooseFile);
        CreateButton("Load", panel.transform, "加载", new Vector2(120f, -122f), new Vector2(74f, 32f), LoadModel);
        CreateButton("Fit", panel.transform, "聚焦", new Vector2(200f, -122f), new Vector2(74f, 32f), FocusModel);
        CreateButton("Clear", panel.transform, "清除", new Vector2(280f, -122f), new Vector2(74f, 32f), ClearModel);

        statusText = CreateText("Status", panel.transform, "准备加载模型…", 13, Color.white, new Vector2(18f, -151f), new Vector2(394f, 22f), new Vector2(0f, 1f));
        statsText = CreateText("Stats", canvasObject.transform, "顶点 0   三角形 0", 15, new Color(0.8f, 0.86f, 0.93f), new Vector2(18f, 18f), new Vector2(300f, 28f), new Vector2(0f, 0f));
    }

    private InputField CreateInputField(Transform parent, string placeholder, Vector2 position, Vector2 size)
    {
        GameObject fieldObject = CreateUIObject("Path Input", parent);
        Image image = fieldObject.AddComponent<Image>();
        image.color = new Color(0.11f, 0.14f, 0.19f, 1f);
        InputField field = fieldObject.AddComponent<InputField>();
        Text text = CreateText("Text", fieldObject.transform, string.Empty, 13, Color.white, new Vector2(8f, 0f), new Vector2(size.x - 16f, size.y), new Vector2(0f, 0.5f));
        text.alignment = TextAnchor.MiddleLeft;
        Text hint = CreateText("Placeholder", fieldObject.transform, placeholder, 13, new Color(0.52f, 0.58f, 0.68f), new Vector2(8f, 0f), new Vector2(size.x - 16f, size.y), new Vector2(0f, 0.5f));
        hint.alignment = TextAnchor.MiddleLeft;
        field.textComponent = text;
        field.placeholder = hint;
        SetRect(fieldObject, new Vector2(0f, 1f), new Vector2(0f, 1f), position, size, new Vector2(0f, 1f));
        return field;
    }

    private Button CreateButton(string name, Transform parent, string label, Vector2 position, Vector2 size, UnityEngine.Events.UnityAction action)
    {
        GameObject buttonObject = CreateUIObject(name, parent);
        Image image = buttonObject.AddComponent<Image>();
        image.color = new Color(0.12f, 0.34f, 0.58f, 1f);
        Button button = buttonObject.AddComponent<Button>();
        button.onClick.AddListener(action);
        Text text = CreateText("Label", buttonObject.transform, label, 13, Color.white, Vector2.zero, size, new Vector2(0.5f, 0.5f));
        text.alignment = TextAnchor.MiddleCenter;
        SetRect(buttonObject, new Vector2(0f, 1f), new Vector2(0f, 1f), position, size, new Vector2(0f, 1f));
        return button;
    }

    private Text CreateText(string name, Transform parent, string content, int fontSize, Color color, Vector2 position, Vector2 size, Vector2 pivot)
    {
        GameObject textObject = CreateUIObject(name, parent);
        Text text = textObject.AddComponent<Text>();
        text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        text.text = content;
        text.fontSize = fontSize;
        text.color = color;
        text.horizontalOverflow = HorizontalWrapMode.Overflow;
        text.verticalOverflow = VerticalWrapMode.Truncate;
        SetRect(textObject, pivot, pivot, position, size, pivot);
        return text;
    }

    private GameObject CreateUIObject(string name, Transform parent)
    {
        GameObject result = new GameObject(name, typeof(RectTransform));
        result.transform.SetParent(parent, false);
        return result;
    }

    private void SetRect(GameObject target, Vector2 anchorMin, Vector2 anchorMax, Vector2 position, Vector2 size, Vector2 pivot)
    {
        RectTransform rect = target.GetComponent<RectTransform>();
        rect.anchorMin = anchorMin;
        rect.anchorMax = anchorMax;
        rect.pivot = pivot;
        rect.anchoredPosition = position;
        rect.sizeDelta = size;
    }

    private void EnsureEventSystem()
    {
        if (FindObjectOfType<EventSystem>() != null)
        {
            return;
        }

        GameObject eventSystem = new GameObject("EventSystem", typeof(EventSystem), typeof(StandaloneInputModule));
        DontDestroyOnLoad(eventSystem);
    }

    private Material GetModelMaterial()
    {
        if (modelMaterial != null)
        {
            return modelMaterial;
        }

        Shader shader = Shader.Find("OBJ Viewer/DoubleSidedLit");
        if (shader == null)
        {
            shader = Shader.Find("Standard");
        }
        if (shader == null)
        {
            shader = Shader.Find("Universal Render Pipeline/Lit");
        }

        modelMaterial = new Material(shader);
        modelMaterial.name = "OBJ Viewer Material";
        modelMaterial.color = new Color(0.38f, 0.68f, 0.95f, 1f);
        return modelMaterial;
    }

    private void SetStatus(string message, bool isError)
    {
        if (statusText == null)
        {
            return;
        }

        statusText.text = message;
        statusText.color = isError ? new Color(1f, 0.52f, 0.48f) : Color.white;
    }
}
