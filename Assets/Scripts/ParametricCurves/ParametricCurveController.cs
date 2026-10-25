using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public sealed class ParametricCurveController : MonoBehaviour
{
    private const int SampleSegments = 160;
    private const string CurveShaderName = "Unlit/Color";

    private readonly List<Vector3> controlPoints = new List<Vector3>();
    private readonly List<ParametricControlPointHandle> handles = new List<ParametricControlPointHandle>();

    private ParametricCurveType curveType = ParametricCurveType.Bezier;
    private ParametricCurveType conversionTarget = ParametricCurveType.CatmullRom;
    private LineRenderer curveLine;
    private LineRenderer controlPolygonLine;
    private Camera curveCamera;
    private ObjOrbitCamera disabledOrbitCamera;
    private Material curveMaterial;
    private Material controlMaterial;
    private Material selectedMaterial;
    private Dropdown typeDropdown;
    private Dropdown targetDropdown;
    private Text statusText;
    private Text statsText;
    private int draggingIndex = -1;
    private int selectedIndex = -1;
    private bool initialized;

    private void Awake()
    {
        Initialize();
    }

    private void Update()
    {
        if (!initialized)
        {
            return;
        }

        ProcessPointerInput();
    }

    private void OnDestroy()
    {
        if (disabledOrbitCamera != null)
        {
            disabledOrbitCamera.enabled = true;
        }

        DestroyRuntimeMaterial(curveMaterial);
        DestroyRuntimeMaterial(controlMaterial);
        DestroyRuntimeMaterial(selectedMaterial);
    }

    public void Initialize()
    {
        if (initialized)
        {
            return;
        }

        initialized = true;
        EnsureEventSystem();
        SetupCamera();
        SetupMaterials();
        SetupCurveObjects();
        ResetCurve();
        BuildInterface();
    }

    public void SetCurveType(int value)
    {
        curveType = (ParametricCurveType)Mathf.Clamp(value, 0, 3);
        RebuildCurve();
        SetStatus("Preview: " + GetCurveName(curveType));
    }

    public void SetConversionTarget(int value)
    {
        conversionTarget = (ParametricCurveType)Mathf.Clamp(value, 0, 3);
    }

    public void ConvertCurve()
    {
        if (controlPoints.Count < 2)
        {
            return;
        }

        List<Vector3> converted = ParametricCurveMath.Convert(
            curveType,
            controlPoints,
            conversionTarget,
            controlPoints.Count);
        controlPoints.Clear();
        controlPoints.AddRange(converted);
        curveType = conversionTarget;
        typeDropdown.value = (int)curveType;
        typeDropdown.RefreshShownValue();
        RebuildHandles();
        RebuildCurve();
        SetStatus("Converted to " + GetCurveName(curveType) + " (sampled approximation)");
    }

    public void ResetCurve()
    {
        controlPoints.Clear();
        controlPoints.Add(new Vector3(-5.2f, -2.0f, 0f));
        controlPoints.Add(new Vector3(-2.3f, 3.2f, 0f));
        controlPoints.Add(new Vector3(0.5f, -3.0f, 0f));
        controlPoints.Add(new Vector3(3.0f, 2.5f, 0f));
        controlPoints.Add(new Vector3(5.2f, -0.8f, 0f));
        selectedIndex = -1;
        RebuildHandles();
        RebuildCurve();
        SetStatus("Ready: drag points, Shift + click to add, right click to remove");
    }

    public void AddPoint()
    {
        Vector3 point = controlPoints.Count == 0
            ? Vector3.zero
            : controlPoints[controlPoints.Count - 1] + new Vector3(1.2f, 0.8f, 0f);
        point.x = Mathf.Clamp(point.x, -6.2f, 6.2f);
        point.y = Mathf.Clamp(point.y, -4.2f, 4.2f);
        controlPoints.Add(point);
        selectedIndex = controlPoints.Count - 1;
        RebuildHandles();
        RebuildCurve();
    }

    public void RemovePoint()
    {
        if (selectedIndex < 0 || selectedIndex >= controlPoints.Count || controlPoints.Count <= 2)
        {
            SetStatus("Select a point first; at least two points are required");
            return;
        }

        controlPoints.RemoveAt(selectedIndex);
        selectedIndex = Mathf.Min(selectedIndex, controlPoints.Count - 1);
        RebuildHandles();
        RebuildCurve();
    }

    public void ReturnToObjViewer()
    {
        ObjViewerController viewer = FindObjectOfType<ObjViewerController>(true);
        if (viewer == null)
        {
            SetStatus("OBJ viewer is not available in this scene");
            return;
        }

        viewer.gameObject.SetActive(true);
        viewer.FocusModel();
        Destroy(gameObject);
    }

    public void OpenQuaternionRotationDemo()
    {
        GameObject demoObject = new GameObject("Quaternion Rotation Demo");
        QuaternionRotationDemoController demo = demoObject.AddComponent<QuaternionRotationDemoController>();
        demo.SetReturnTarget(gameObject);
        ObjViewerController viewer = FindObjectOfType<ObjViewerController>(true);
        ObjLoadedModel model = viewer == null ? null : viewer.LoadedModel;
        if (model == null)
        {
            ObjLoadedModel[] sceneModels = FindObjectsOfType<ObjLoadedModel>(true);
            for (int i = 0; i < sceneModels.Length; i++)
            {
                if (sceneModels[i] != null && sceneModels[i].UnityMesh != null)
                {
                    model = sceneModels[i];
                    break;
                }
            }
        }

        if (model != null)
        {
            demo.SetObjModel(model);
        }

        gameObject.SetActive(false);
    }

    private void SetupCamera()
    {
        curveCamera = Camera.main;
        if (curveCamera == null)
        {
            GameObject cameraObject = new GameObject("Parametric Curve Camera");
            curveCamera = cameraObject.AddComponent<Camera>();
            cameraObject.AddComponent<AudioListener>();
            cameraObject.tag = "MainCamera";
        }

        disabledOrbitCamera = curveCamera.GetComponent<ObjOrbitCamera>();
        if (disabledOrbitCamera != null)
        {
            disabledOrbitCamera.enabled = false;
        }

        curveCamera.transform.position = new Vector3(0f, 0f, -14f);
        curveCamera.transform.rotation = Quaternion.identity;
        curveCamera.orthographic = true;
        curveCamera.orthographicSize = 6f;
        curveCamera.nearClipPlane = 0.01f;
        curveCamera.farClipPlane = 100f;
        curveCamera.clearFlags = CameraClearFlags.SolidColor;
        curveCamera.backgroundColor = new Color(0.025f, 0.035f, 0.055f, 1f);
    }

    private void SetupMaterials()
    {
        Shader shader = Shader.Find(CurveShaderName);
        if (shader == null)
        {
            shader = Shader.Find("Sprites/Default");
        }

        curveMaterial = new Material(shader);
        curveMaterial.color = new Color(0.2f, 0.78f, 1f, 1f);
        controlMaterial = new Material(shader);
        controlMaterial.color = new Color(0.25f, 0.48f, 0.78f, 1f);
        selectedMaterial = new Material(shader);
        selectedMaterial.color = new Color(1f, 0.72f, 0.18f, 1f);
    }

    private void SetupCurveObjects()
    {
        GameObject curveObject = new GameObject("Parametric Curve");
        curveObject.transform.SetParent(transform, false);
        curveLine = curveObject.AddComponent<LineRenderer>();
        curveLine.material = curveMaterial;
        curveLine.widthMultiplier = 0.075f;
        curveLine.numCapVertices = 4;
        curveLine.useWorldSpace = true;
        curveLine.alignment = LineAlignment.TransformZ;

        GameObject polygonObject = new GameObject("Control Polygon");
        polygonObject.transform.SetParent(transform, false);
        controlPolygonLine = polygonObject.AddComponent<LineRenderer>();
        controlPolygonLine.material = controlMaterial;
        controlPolygonLine.widthMultiplier = 0.025f;
        controlPolygonLine.numCapVertices = 2;
        controlPolygonLine.useWorldSpace = true;
    }

    private void BuildInterface()
    {
        GameObject canvasObject = new GameObject("Parametric Curve UI");
        canvasObject.transform.SetParent(transform, false);
        Canvas canvas = canvasObject.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        CanvasScaler scaler = canvasObject.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1280f, 720f);
        canvasObject.AddComponent<GraphicRaycaster>();

        GameObject panel = CreateUIObject("Curve Controls", canvasObject.transform);
        Image panelImage = panel.AddComponent<Image>();
        panelImage.color = new Color(0.035f, 0.05f, 0.075f, 0.95f);
        SetRect(panel, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(18f, -18f), new Vector2(460f, 322f), new Vector2(0f, 1f));

        CreateText(panel.transform, "PARAMETRIC CURVES", 21, Color.white, new Vector2(18f, -17f), new Vector2(420f, 30f));
        CreateText(panel.transform, "Drag points | Shift + click: add | Right click: remove", 12, new Color(0.67f, 0.75f, 0.86f), new Vector2(18f, -46f), new Vector2(420f, 24f));
        CreateText(panel.transform, "Curve type", 13, Color.white, new Vector2(18f, -79f), new Vector2(105f, 28f));
        typeDropdown = CreateDropdown(panel.transform, new Vector2(120f, -78f), new Vector2(150f, 30f), (int)curveType, SetCurveType);
        CreateText(panel.transform, "Convert to", 13, Color.white, new Vector2(18f, -117f), new Vector2(105f, 28f));
        targetDropdown = CreateDropdown(panel.transform, new Vector2(120f, -116f), new Vector2(150f, 30f), (int)conversionTarget, SetConversionTarget);
        CreateButton(panel.transform, "CONVERT", new Vector2(282f, -116f), new Vector2(145f, 30f), ConvertCurve);
        CreateButton(panel.transform, "RESET", new Vector2(18f, -157f), new Vector2(100f, 32f), ResetCurve);
        CreateButton(panel.transform, "ADD POINT", new Vector2(128f, -157f), new Vector2(110f, 32f), AddPoint);
        CreateButton(panel.transform, "REMOVE", new Vector2(248f, -157f), new Vector2(100f, 32f), RemovePoint);
        CreateButton(panel.transform, "OBJ VIEWER", new Vector2(358f, -157f), new Vector2(70f, 32f), ReturnToObjViewer);
        CreateButton(panel.transform, "QUATERNION", new Vector2(18f, -199f), new Vector2(140f, 32f), OpenQuaternionRotationDemo);
        CreateButton(panel.transform, "DEFORM / FFD", new Vector2(168f, -199f), new Vector2(140f, 32f),
            () => UnityEngine.SceneManagement.SceneManager.LoadScene(GlobalDeformationController.SceneName));
        statusText = CreateText(panel.transform, "", 12, Color.white, new Vector2(18f, -244f), new Vector2(420f, 34f));
        statusText.horizontalOverflow = HorizontalWrapMode.Wrap;
        statsText = CreateText(canvasObject.transform, "", 15, new Color(0.82f, 0.88f, 0.95f), new Vector2(18f, 18f), new Vector2(700f, 28f));
        RectTransform statsRect = statsText.GetComponent<RectTransform>();
        statsRect.anchorMin = Vector2.zero;
        statsRect.anchorMax = Vector2.zero;
        statsRect.pivot = Vector2.zero;
        statsRect.anchoredPosition = new Vector2(18f, 18f);
    }

    private void ProcessPointerInput()
    {
        if (EventSystem.current != null && EventSystem.current.IsPointerOverGameObject())
        {
            if (Input.GetMouseButtonUp(0))
            {
                draggingIndex = -1;
            }
            return;
        }

        if (Input.GetMouseButtonDown(0))
        {
            ParametricControlPointHandle hitHandle = RaycastHandle();
            if (hitHandle != null)
            {
                selectedIndex = hitHandle.Index;
                draggingIndex = selectedIndex;
                RefreshHandleMaterials();
                return;
            }

            if (Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift))
            {
                Vector3 point;
                if (TryGetPlanePoint(out point))
                {
                    controlPoints.Add(point);
                    selectedIndex = controlPoints.Count - 1;
                    RebuildHandles();
                    RebuildCurve();
                }
            }
        }

        if (draggingIndex >= 0 && Input.GetMouseButton(0))
        {
            Vector3 point;
            if (TryGetPlanePoint(out point))
            {
                point.x = Mathf.Clamp(point.x, -7.2f, 7.2f);
                point.y = Mathf.Clamp(point.y, -5.1f, 5.1f);
                controlPoints[draggingIndex] = point;
                handles[draggingIndex].transform.position = point;
                RebuildCurve();
            }
        }

        if (Input.GetMouseButtonUp(0))
        {
            draggingIndex = -1;
        }

        if (Input.GetMouseButtonDown(1))
        {
            ParametricControlPointHandle hitHandle = RaycastHandle();
            if (hitHandle != null)
            {
                selectedIndex = hitHandle.Index;
                RemovePoint();
            }
        }
    }

    private ParametricControlPointHandle RaycastHandle()
    {
        Ray ray = curveCamera.ScreenPointToRay(Input.mousePosition);
        RaycastHit hit;
        if (Physics.Raycast(ray, out hit, 100f))
        {
            return hit.collider.GetComponent<ParametricControlPointHandle>();
        }

        return null;
    }

    private bool TryGetPlanePoint(out Vector3 point)
    {
        Ray ray = curveCamera.ScreenPointToRay(Input.mousePosition);
        Plane plane = new Plane(Vector3.forward, Vector3.zero);
        float distance;
        if (plane.Raycast(ray, out distance))
        {
            point = ray.GetPoint(distance);
            point.z = 0f;
            return true;
        }

        point = Vector3.zero;
        return false;
    }

    private void RebuildHandles()
    {
        for (int i = 0; i < handles.Count; i++)
        {
            if (handles[i] != null)
            {
                Destroy(handles[i].gameObject);
            }
        }

        handles.Clear();
        for (int i = 0; i < controlPoints.Count; i++)
        {
            GameObject pointObject = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            pointObject.transform.SetParent(transform, false);
            pointObject.transform.position = controlPoints[i];
            pointObject.transform.localScale = Vector3.one * 0.28f;
            ParametricControlPointHandle handle = pointObject.AddComponent<ParametricControlPointHandle>();
            handle.SetIndex(i);
            handles.Add(handle);
        }

        RefreshHandleMaterials();
    }

    private void RefreshHandleMaterials()
    {
        for (int i = 0; i < handles.Count; i++)
        {
            Renderer renderer = handles[i].GetComponent<Renderer>();
            renderer.sharedMaterial = i == selectedIndex ? selectedMaterial : controlMaterial;
        }
    }

    private void RebuildCurve()
    {
        if (curveLine == null || controlPolygonLine == null)
        {
            return;
        }

        List<Vector3> samples = ParametricCurveMath.Sample(curveType, controlPoints, SampleSegments);
        curveLine.positionCount = samples.Count;
        curveLine.SetPositions(samples.ToArray());
        controlPolygonLine.positionCount = controlPoints.Count;
        controlPolygonLine.SetPositions(controlPoints.ToArray());

        if (statsText != null)
        {
            statsText.text = GetCurveName(curveType) + "    Control points: " + controlPoints.Count
                             + "    Samples: " + samples.Count
                             + (selectedIndex >= 0 ? "    Selected: P" + selectedIndex : string.Empty);
        }

        RefreshHandleMaterials();
    }

    private Dropdown CreateDropdown(Transform parent, Vector2 position, Vector2 size, int value, UnityEngine.Events.UnityAction<int> action)
    {
        GameObject dropdownObject = CreateUIObject("Curve Type Dropdown", parent);
        Image image = dropdownObject.AddComponent<Image>();
        image.color = new Color(0.11f, 0.14f, 0.19f, 1f);
        Dropdown dropdown = dropdownObject.AddComponent<Dropdown>();
        dropdown.options.Clear();
        dropdown.options.Add(new Dropdown.OptionData("Bezier"));
        dropdown.options.Add(new Dropdown.OptionData("Catmull-Rom"));
        dropdown.options.Add(new Dropdown.OptionData("Uniform B-Spline"));
        dropdown.options.Add(new Dropdown.OptionData("Lagrange"));
        dropdown.value = value;
        dropdown.onValueChanged.AddListener(action);
        Text label = CreateText(dropdownObject.transform, "", 12, Color.white, new Vector2(10f, 0f), new Vector2(size.x - 30f, size.y));
        label.alignment = TextAnchor.MiddleLeft;
        dropdown.captionText = label;
        GameObject templateObject = CreateUIObject("Template", dropdownObject.transform);
        Image templateImage = templateObject.AddComponent<Image>();
        templateImage.color = new Color(0.08f, 0.1f, 0.14f, 1f);
        ScrollRect scrollRect = templateObject.AddComponent<ScrollRect>();
        scrollRect.horizontal = false;
        scrollRect.vertical = true;
        scrollRect.movementType = ScrollRect.MovementType.Clamped;

        GameObject viewportObject = CreateUIObject("Viewport", templateObject.transform);
        Image viewportImage = viewportObject.AddComponent<Image>();
        viewportImage.color = new Color(1f, 1f, 1f, 0.01f);
        Mask mask = viewportObject.AddComponent<Mask>();
        mask.showMaskGraphic = false;
        GameObject contentObject = CreateUIObject("Content", viewportObject.transform);
        scrollRect.viewport = viewportObject.GetComponent<RectTransform>();
        scrollRect.content = contentObject.GetComponent<RectTransform>();

        GameObject itemObject = CreateUIObject("Item", contentObject.transform);
        Toggle itemToggle = itemObject.AddComponent<Toggle>();
        Image itemBackground = itemObject.AddComponent<Image>();
        itemBackground.color = new Color(0.12f, 0.34f, 0.58f, 1f);
        itemToggle.targetGraphic = itemBackground;
        Text itemLabel = CreateText(itemObject.transform, "", 12, Color.white, new Vector2(8f, 0f), new Vector2(size.x - 16f, 28f));
        itemLabel.alignment = TextAnchor.MiddleLeft;
        dropdown.template = templateObject.GetComponent<RectTransform>();
        dropdown.itemText = itemLabel;
        SetRect(templateObject, new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(0f, 0f), new Vector2(0f, 120f), new Vector2(0f, 1f));
        SetRect(viewportObject, new Vector2(0f, 0f), new Vector2(1f, 1f), Vector2.zero, Vector2.zero, new Vector2(0f, 0f));
        SetRect(contentObject, new Vector2(0f, 1f), new Vector2(1f, 1f), Vector2.zero, new Vector2(0f, 120f), new Vector2(0f, 1f));
        SetRect(itemObject, new Vector2(0f, 1f), new Vector2(1f, 1f), Vector2.zero, new Vector2(0f, 28f), new Vector2(0f, 1f));
        templateObject.SetActive(false);
        SetRect(dropdownObject, new Vector2(0f, 1f), new Vector2(0f, 1f), position, size, new Vector2(0f, 1f));
        return dropdown;
    }

    private Button CreateButton(Transform parent, string label, Vector2 position, Vector2 size, UnityEngine.Events.UnityAction action)
    {
        GameObject buttonObject = CreateUIObject(label, parent);
        Image image = buttonObject.AddComponent<Image>();
        image.color = new Color(0.12f, 0.34f, 0.58f, 1f);
        Button button = buttonObject.AddComponent<Button>();
        button.onClick.AddListener(action);
        Text text = CreateText(buttonObject.transform, label, 11, Color.white, Vector2.zero, size);
        text.alignment = TextAnchor.MiddleCenter;
        SetRect(buttonObject, new Vector2(0f, 1f), new Vector2(0f, 1f), position, size, new Vector2(0f, 1f));
        return button;
    }

    private Text CreateText(Transform parent, string content, int fontSize, Color color, Vector2 position, Vector2 size)
    {
        GameObject textObject = CreateUIObject("Text", parent);
        Text text = textObject.AddComponent<Text>();
        text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        text.text = content;
        text.fontSize = fontSize;
        text.color = color;
        text.horizontalOverflow = HorizontalWrapMode.Overflow;
        text.verticalOverflow = VerticalWrapMode.Truncate;
        SetRect(textObject, new Vector2(0f, 1f), new Vector2(0f, 1f), position, size, new Vector2(0f, 1f));
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

    private void SetStatus(string message)
    {
        if (statusText != null)
        {
            statusText.text = message;
        }
    }

    private static string GetCurveName(ParametricCurveType type)
    {
        switch (type)
        {
            case ParametricCurveType.CatmullRom:
                return "Catmull-Rom";
            case ParametricCurveType.UniformBSpline:
                return "Uniform B-Spline";
            case ParametricCurveType.Lagrange:
                return "Lagrange";
            default:
                return "Bezier";
        }
    }

    private static void DestroyRuntimeMaterial(Material material)
    {
        if (material != null)
        {
            Destroy(material);
        }
    }
}
