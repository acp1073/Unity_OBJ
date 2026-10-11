using System.Globalization;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// Runtime demonstration of axis-angle rotations, quaternion composition,
/// vector rotation, and three rotation interpolation methods.
/// </summary>
public sealed class QuaternionRotationDemoController : MonoBehaviour
{
    private const float InterpolationDuration = 2f;

    private enum InterpolationMode
    {
        EulerAngles,
        QuaternionLerp,
        QuaternionSlerp
    }

    private GameObject returnTarget;
    private Camera demoCamera;
    private CameraState previousCameraState;
    private bool cameraStateCaptured;
    private bool cameraStateRestored;

    private GameObject visualizationRoot;
    private Transform rotatingCube;
    private Transform importedModelTransform;
    private Transform importedModelOriginalParent;
    private Vector3 importedModelOriginalLocalPosition;
    private Quaternion importedModelOriginalLocalRotation;
    private Vector3 importedModelOriginalLocalScale;
    private bool importedModelWasReparented;
    private LineRenderer rotatedVectorLine;
    private Material cubeMaterial;
    private Material[] axisMaterials;
    private Material vectorMaterial;

    private InputField axisXInput;
    private InputField axisYInput;
    private InputField axisZInput;
    private InputField angleInput;
    private Text rotationReadout;
    private Text vectorReadout;
    private Text statusReadout;
    private Text modelNameReadout;

    private Quaternion currentRotation = Quaternion.identity;
    private Quaternion interpolationStart = Quaternion.identity;
    private Quaternion interpolationTarget = Quaternion.identity;
    private Vector3 interpolationStartEuler;
    private Vector3 interpolationTargetEuler;
    private InterpolationMode interpolationMode;
    private float interpolationElapsed;
    private bool isInterpolating;
    private bool showNegativeQuaternion;

    private struct CameraState
    {
        public Vector3 position;
        public Quaternion rotation;
        public bool orthographic;
        public float orthographicSize;
        public float fieldOfView;
        public float nearClipPlane;
        public float farClipPlane;
        public CameraClearFlags clearFlags;
        public Color backgroundColor;
        public bool orbitCameraEnabled;
    }

    private void Awake()
    {
        SetupCamera();
        SetupVisualization();
        BuildInterface();
        UpdateVisualization();
        UpdateReadouts();
        SetStatus("未绑定 OBJ，当前使用默认立方体。", false);
    }

    private void Update()
    {
        if (!isInterpolating)
        {
            return;
        }

        interpolationElapsed += Time.deltaTime;
        float t = Mathf.Clamp01(interpolationElapsed / InterpolationDuration);
        currentRotation = EvaluateInterpolation(t);
        UpdateVisualization();
        UpdateReadouts();

        if (t >= 1f)
        {
            isInterpolating = false;
            SetStatus(GetInterpolationName(interpolationMode) + " 完成。", false);
        }
        else
        {
            SetStatus(GetInterpolationName(interpolationMode) + " 进行中，t = " + t.ToString("F2", CultureInfo.InvariantCulture), false);
        }
    }

    private void OnDestroy()
    {
        RestoreImportedModel();
        RestoreCamera();
        DestroyRuntimeMaterial(cubeMaterial);
        DestroyRuntimeMaterial(vectorMaterial);
        if (axisMaterials != null)
        {
            for (int i = 0; i < axisMaterials.Length; i++)
            {
                DestroyRuntimeMaterial(axisMaterials[i]);
            }
        }
    }

    public void SetReturnTarget(GameObject target)
    {
        returnTarget = target;
    }

    public void SetObjModel(ObjLoadedModel model)
    {
        if (model == null || model.UnityMesh == null || visualizationRoot == null)
        {
            return;
        }

        Renderer modelRenderer = model.GetComponent<Renderer>();
        if (modelRenderer == null)
        {
            return;
        }

        if (rotatingCube != null)
        {
            rotatingCube.gameObject.SetActive(false);
        }

        importedModelTransform = model.transform;
        importedModelOriginalParent = importedModelTransform.parent;
        importedModelOriginalLocalPosition = importedModelTransform.localPosition;
        importedModelOriginalLocalRotation = importedModelTransform.localRotation;
        importedModelOriginalLocalScale = importedModelTransform.localScale;

        Bounds bounds = modelRenderer.bounds;
        GameObject pivotObject = new GameObject("Rotating OBJ Pivot");
        Transform pivot = pivotObject.transform;
        pivot.SetParent(visualizationRoot.transform, false);
        pivot.position = bounds.center;

        importedModelTransform.SetParent(pivot, true);
        importedModelWasReparented = true;

        float largestDimension = Mathf.Max(bounds.size.x, Mathf.Max(bounds.size.y, bounds.size.z));
        if (largestDimension > 4f)
        {
            pivot.localScale = Vector3.one * (4f / largestDimension);
        }

        rotatingCube = pivot;
        if (modelNameReadout != null)
        {
            modelNameReadout.text = "OBJ: " + model.name;
        }

        CreateAxisLine("OBJ Local X", Vector3.zero, Vector3.right * 2f, axisMaterials[0], 0.045f, rotatingCube);
        CreateAxisLine("OBJ Local Y", Vector3.zero, Vector3.up * 2f, axisMaterials[1], 0.045f, rotatingCube);
        CreateAxisLine("OBJ Local Z", Vector3.zero, Vector3.forward * 2f, axisMaterials[2], 0.045f, rotatingCube);
        SetStatus("当前旋转模型：" + model.name, false);
    }

    public void ApplyAxisAngle()
    {
        Vector3 axis;
        float angle;
        if (!TryReadAxisAngle(out axis, out angle))
        {
            SetStatus("请输入有效的数字；旋转轴不能为零向量。", true);
            return;
        }

        isInterpolating = false;
        currentRotation = Quaternion.AngleAxis(angle, axis);
        showNegativeQuaternion = false;
        UpdateVisualization();
        UpdateReadouts();
        SetStatus("已应用 q = AngleAxis(角度, 归一化旋转轴)。", false);
    }

    public void ComposeFortyFiveDegrees()
    {
        Vector3 axis;
        float ignoredAngle;
        if (!TryReadAxisAngle(out axis, out ignoredAngle))
        {
            SetStatus("请输入有效的数字；旋转轴不能为零向量。", true);
            return;
        }

        isInterpolating = false;
        Quaternion delta = Quaternion.AngleAxis(45f, axis);
        currentRotation = delta * currentRotation;
        showNegativeQuaternion = false;
        UpdateVisualization();
        UpdateReadouts();
        SetStatus("已组合旋转：q新 = q增量 × q当前（世界坐标系）。", false);
    }

    public void PlayEulerAngleInterpolation()
    {
        StartInterpolation(InterpolationMode.EulerAngles);
    }

    public void PlayQuaternionLerp()
    {
        StartInterpolation(InterpolationMode.QuaternionLerp);
    }

    public void PlayQuaternionSlerp()
    {
        StartInterpolation(InterpolationMode.QuaternionSlerp);
    }

    public void ToggleQuaternionSign()
    {
        showNegativeQuaternion = !showNegativeQuaternion;
        UpdateReadouts();
        SetStatus("q 与 -q 表示同一个三维旋转；仅改变显示的四元数符号。", false);
    }

    public void ResetRotation()
    {
        isInterpolating = false;
        currentRotation = Quaternion.identity;
        showNegativeQuaternion = false;
        UpdateVisualization();
        UpdateReadouts();
        SetStatus("已重置为单位四元数；保留目标轴角，方便比较不同插值方法。", false);
    }

    public void ReturnToCurveDemo()
    {
        isInterpolating = false;
        RestoreImportedModel();
        RestoreCamera();
        if (returnTarget != null)
        {
            returnTarget.SetActive(true);
        }

        Destroy(gameObject);
    }

    private void SetupCamera()
    {
        demoCamera = Camera.main;
        if (demoCamera == null)
        {
            GameObject cameraObject = new GameObject("Quaternion Demo Camera");
            demoCamera = cameraObject.AddComponent<Camera>();
            cameraObject.AddComponent<AudioListener>();
            cameraObject.tag = "MainCamera";
        }

        ObjOrbitCamera orbitCamera = demoCamera.GetComponent<ObjOrbitCamera>();
        previousCameraState = new CameraState
        {
            position = demoCamera.transform.position,
            rotation = demoCamera.transform.rotation,
            orthographic = demoCamera.orthographic,
            orthographicSize = demoCamera.orthographicSize,
            fieldOfView = demoCamera.fieldOfView,
            nearClipPlane = demoCamera.nearClipPlane,
            farClipPlane = demoCamera.farClipPlane,
            clearFlags = demoCamera.clearFlags,
            backgroundColor = demoCamera.backgroundColor,
            orbitCameraEnabled = orbitCamera != null && orbitCamera.enabled
        };
        cameraStateCaptured = true;

        if (orbitCamera != null)
        {
            orbitCamera.enabled = false;
        }

        demoCamera.transform.position = new Vector3(4.5f, 3.2f, -7.5f);
        demoCamera.transform.rotation = Quaternion.LookRotation(-demoCamera.transform.position.normalized, Vector3.up);
        demoCamera.orthographic = false;
        demoCamera.fieldOfView = 42f;
        demoCamera.nearClipPlane = 0.05f;
        demoCamera.farClipPlane = 100f;
        demoCamera.clearFlags = CameraClearFlags.SolidColor;
        demoCamera.backgroundColor = new Color(0.055f, 0.075f, 0.11f, 1f);
    }

    private void RestoreCamera()
    {
        if (!cameraStateCaptured || cameraStateRestored || demoCamera == null)
        {
            return;
        }

        demoCamera.transform.position = previousCameraState.position;
        demoCamera.transform.rotation = previousCameraState.rotation;
        demoCamera.orthographic = previousCameraState.orthographic;
        demoCamera.orthographicSize = previousCameraState.orthographicSize;
        demoCamera.fieldOfView = previousCameraState.fieldOfView;
        demoCamera.nearClipPlane = previousCameraState.nearClipPlane;
        demoCamera.farClipPlane = previousCameraState.farClipPlane;
        demoCamera.clearFlags = previousCameraState.clearFlags;
        demoCamera.backgroundColor = previousCameraState.backgroundColor;

        ObjOrbitCamera orbitCamera = demoCamera.GetComponent<ObjOrbitCamera>();
        if (orbitCamera != null)
        {
            orbitCamera.enabled = previousCameraState.orbitCameraEnabled;
        }

        cameraStateRestored = true;
    }

    private void SetupVisualization()
    {
        visualizationRoot = new GameObject("Quaternion Rotation Visualization");
        visualizationRoot.transform.SetParent(transform, false);

        axisMaterials = new[]
        {
            CreateMaterial(new Color(0.95f, 0.28f, 0.28f, 1f)),
            CreateMaterial(new Color(0.3f, 0.9f, 0.42f, 1f)),
            CreateMaterial(new Color(0.32f, 0.58f, 1f, 1f))
        };
        vectorMaterial = CreateMaterial(new Color(1f, 0.78f, 0.18f, 1f));
        cubeMaterial = CreateMaterial(new Color(0.24f, 0.64f, 0.92f, 1f));

        CreateAxisLine("World X", Vector3.zero, Vector3.right * 2.7f, axisMaterials[0], 0.035f, visualizationRoot.transform);
        CreateAxisLine("World Y", Vector3.zero, Vector3.up * 2.7f, axisMaterials[1], 0.035f, visualizationRoot.transform);
        CreateAxisLine("World Z", Vector3.zero, Vector3.forward * 2.7f, axisMaterials[2], 0.035f, visualizationRoot.transform);

        GameObject cube = GameObject.CreatePrimitive(PrimitiveType.Cube);
        cube.name = "Quaternion Rotating Cube";
        cube.transform.SetParent(visualizationRoot.transform, false);
        cube.transform.localScale = Vector3.one * 1.45f;
        cube.GetComponent<Renderer>().sharedMaterial = cubeMaterial;
        rotatingCube = cube.transform;

        CreateAxisLine("Local X", Vector3.zero, Vector3.right * 1.7f, axisMaterials[0], 0.045f, rotatingCube);
        CreateAxisLine("Local Y", Vector3.zero, Vector3.up * 1.7f, axisMaterials[1], 0.045f, rotatingCube);
        CreateAxisLine("Local Z", Vector3.zero, Vector3.forward * 1.7f, axisMaterials[2], 0.045f, rotatingCube);
        rotatedVectorLine = CreateAxisLine(
            "Rotated Vector",
            Vector3.zero,
            Vector3.right * 2f,
            vectorMaterial,
            0.07f,
            visualizationRoot.transform);
    }

    private void RestoreImportedModel()
    {
        if (!importedModelWasReparented || importedModelTransform == null)
        {
            return;
        }

        importedModelTransform.SetParent(importedModelOriginalParent, false);
        importedModelTransform.localPosition = importedModelOriginalLocalPosition;
        importedModelTransform.localRotation = importedModelOriginalLocalRotation;
        importedModelTransform.localScale = importedModelOriginalLocalScale;
        importedModelWasReparented = false;
    }

    private void BuildInterface()
    {
        EnsureEventSystem();

        GameObject canvasObject = new GameObject("Quaternion Rotation UI", typeof(RectTransform));
        canvasObject.transform.SetParent(transform, false);
        Canvas canvas = canvasObject.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        CanvasScaler scaler = canvasObject.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1280f, 720f);
        canvasObject.AddComponent<GraphicRaycaster>();

        GameObject panel = CreateUIObject("Quaternion Controls", canvasObject.transform);
        Image panelImage = panel.AddComponent<Image>();
        panelImage.color = new Color(0.035f, 0.05f, 0.075f, 0.95f);
        SetRect(panel, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(18f, -18f), new Vector2(405f, 480f), new Vector2(0f, 1f));

        CreateText(panel.transform, "QUATERNION ROTATION", 20, Color.white, new Vector2(16f, -14f), new Vector2(373f, 30f));
        modelNameReadout = CreateText(panel.transform, "OBJ: Default Cube", 11, new Color(0.68f, 0.76f, 0.86f), new Vector2(16f, -44f), new Vector2(373f, 28f));

        CreateText(panel.transform, "Axis X", 11, Color.white, new Vector2(16f, -80f), new Vector2(70f, 20f));
        CreateText(panel.transform, "Axis Y", 11, Color.white, new Vector2(99f, -80f), new Vector2(70f, 20f));
        CreateText(panel.transform, "Axis Z", 11, Color.white, new Vector2(182f, -80f), new Vector2(70f, 20f));
        CreateText(panel.transform, "Angle °", 11, Color.white, new Vector2(265f, -80f), new Vector2(80f, 20f));

        axisXInput = CreateInputField(panel.transform, "Axis X", "0", new Vector2(16f, -105f), new Vector2(72f, 30f));
        axisYInput = CreateInputField(panel.transform, "Axis Y", "1", new Vector2(99f, -105f), new Vector2(72f, 30f));
        axisZInput = CreateInputField(panel.transform, "Axis Z", "0", new Vector2(182f, -105f), new Vector2(72f, 30f));
        angleInput = CreateInputField(panel.transform, "Angle", "90", new Vector2(265f, -105f), new Vector2(86f, 30f));

        CreateButton(panel.transform, "APPLY AXIS-ANGLE", new Vector2(16f, -149f), new Vector2(180f, 34f), ApplyAxisAngle);
        CreateButton(panel.transform, "COMPOSE +45°", new Vector2(206f, -149f), new Vector2(145f, 34f), ComposeFortyFiveDegrees);
        CreateButton(panel.transform, "ANGLE LERP", new Vector2(16f, -192f), new Vector2(105f, 32f), PlayEulerAngleInterpolation);
        CreateButton(panel.transform, "QUAT LERP", new Vector2(131f, -192f), new Vector2(105f, 32f), PlayQuaternionLerp);
        CreateButton(panel.transform, "QUAT SLERP", new Vector2(246f, -192f), new Vector2(105f, 32f), PlayQuaternionSlerp);
        CreateButton(panel.transform, "RESET", new Vector2(16f, -234f), new Vector2(90f, 32f), ResetRotation);
        CreateButton(panel.transform, "SHOW -q", new Vector2(116f, -234f), new Vector2(90f, 32f), ToggleQuaternionSign);
        CreateButton(panel.transform, "BACK TO CURVE", new Vector2(216f, -234f), new Vector2(135f, 32f), ReturnToCurveDemo);

        statusReadout = CreateText(panel.transform, "", 11, Color.white, new Vector2(16f, -278f), new Vector2(373f, 35f));
        rotationReadout = CreateText(panel.transform, "", 12, new Color(0.82f, 0.9f, 1f), new Vector2(16f, -320f), new Vector2(373f, 58f));
        vectorReadout = CreateText(panel.transform, "", 12, new Color(1f, 0.84f, 0.38f), new Vector2(16f, -385f), new Vector2(373f, 35f));
        CreateText(panel.transform, "q rotates v by q * v; q and -q represent the same orientation.", 10, new Color(0.62f, 0.72f, 0.83f), new Vector2(16f, -438f), new Vector2(373f, 30f));
    }

    private bool TryReadAxisAngle(out Vector3 axis, out float angle)
    {
        angle = 0f;
        float x = 0f;
        float y = 0f;
        float z = 0f;
        bool valid = TryParse(axisXInput.text, out x)
                     && TryParse(axisYInput.text, out y)
                     && TryParse(axisZInput.text, out z)
                     && TryParse(angleInput.text, out angle);
        axis = new Vector3(x, y, z);
        if (!valid || axis.sqrMagnitude < 0.000001f)
        {
            angle = 0f;
            return false;
        }

        axis.Normalize();
        return true;
    }

    private static bool TryParse(string text, out float value)
    {
        return float.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out value)
               || float.TryParse(text, NumberStyles.Float, CultureInfo.CurrentCulture, out value);
    }

    private void StartInterpolation(InterpolationMode mode)
    {
        Vector3 axis;
        float angle;
        if (!TryReadAxisAngle(out axis, out angle))
        {
            SetStatus("请输入有效的数字；旋转轴不能为零向量。", true);
            return;
        }

        interpolationMode = mode;
        interpolationStart = currentRotation;
        interpolationTarget = Quaternion.AngleAxis(angle, axis);
        interpolationStartEuler = interpolationStart.eulerAngles;
        interpolationTargetEuler = interpolationTarget.eulerAngles;
        interpolationElapsed = 0f;
        isInterpolating = true;
        showNegativeQuaternion = false;
        SetStatus("从当前姿态插值到输入的目标姿态：" + GetInterpolationName(mode), false);
    }

    private Quaternion EvaluateInterpolation(float t)
    {
        switch (interpolationMode)
        {
            case InterpolationMode.EulerAngles:
                Vector3 euler = new Vector3(
                    Mathf.LerpAngle(interpolationStartEuler.x, interpolationTargetEuler.x, t),
                    Mathf.LerpAngle(interpolationStartEuler.y, interpolationTargetEuler.y, t),
                    Mathf.LerpAngle(interpolationStartEuler.z, interpolationTargetEuler.z, t));
                return Quaternion.Euler(euler);
            case InterpolationMode.QuaternionLerp:
                return Quaternion.Lerp(interpolationStart, interpolationTarget, t);
            default:
                return Quaternion.Slerp(interpolationStart, interpolationTarget, t);
        }
    }

    private static string GetInterpolationName(InterpolationMode mode)
    {
        switch (mode)
        {
            case InterpolationMode.EulerAngles:
                return "欧拉角分量插值";
            case InterpolationMode.QuaternionLerp:
                return "四元数线性插值 Lerp";
            default:
                return "四元数球面线性插值 Slerp";
        }
    }

    private void UpdateVisualization()
    {
        if (rotatingCube != null)
        {
            rotatingCube.rotation = currentRotation;
        }

        if (rotatedVectorLine != null)
        {
            Vector3 rotatedVector = currentRotation * Vector3.right;
            rotatedVectorLine.SetPosition(0, Vector3.zero);
            rotatedVectorLine.SetPosition(1, rotatedVector * 2.3f);
        }
    }

    private void UpdateReadouts()
    {
        Quaternion shown = showNegativeQuaternion
            ? new Quaternion(-currentRotation.x, -currentRotation.y, -currentRotation.z, -currentRotation.w)
            : currentRotation;
        Vector3 euler = currentRotation.eulerAngles;
        Vector3 rotatedVector = currentRotation * Vector3.right;

        if (rotationReadout != null)
        {
            rotationReadout.text = string.Format(
                CultureInfo.InvariantCulture,
                "q = ({0:F3}, {1:F3}, {2:F3}, {3:F3})\nEuler XYZ = ({4:F1}°, {5:F1}°, {6:F1}°)",
                shown.x, shown.y, shown.z, shown.w, euler.x, euler.y, euler.z);
        }

        if (vectorReadout != null)
        {
            vectorReadout.text = string.Format(
                CultureInfo.InvariantCulture,
                "v = (1, 0, 0)  →  q·v = ({0:F2}, {1:F2}, {2:F2})",
                rotatedVector.x, rotatedVector.y, rotatedVector.z);
        }
    }

    private void SetStatus(string message, bool isError)
    {
        if (statusReadout != null)
        {
            statusReadout.text = message;
            statusReadout.color = isError ? new Color(1f, 0.48f, 0.42f) : Color.white;
        }
    }

    private LineRenderer CreateAxisLine(string name, Vector3 start, Vector3 end, Material material, float width, Transform parent)
    {
        GameObject lineObject = new GameObject(name);
        lineObject.transform.SetParent(parent, false);
        LineRenderer line = lineObject.AddComponent<LineRenderer>();
        line.useWorldSpace = false;
        line.positionCount = 2;
        line.SetPosition(0, start);
        line.SetPosition(1, end);
        line.widthMultiplier = width;
        line.numCapVertices = 3;
        line.sharedMaterial = material;
        line.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        line.receiveShadows = false;
        return line;
    }

    private Material CreateMaterial(Color color)
    {
        Shader shader = Shader.Find("Unlit/Color");
        if (shader == null)
        {
            shader = Shader.Find("Sprites/Default");
        }

        Material material = new Material(shader);
        material.color = color;
        return material;
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

    private GameObject CreateUIObject(string objectName, Transform parent)
    {
        GameObject result = new GameObject(objectName, typeof(RectTransform));
        result.transform.SetParent(parent, false);
        return result;
    }

    private Text CreateText(Transform parent, string content, int fontSize, Color color, Vector2 position, Vector2 size)
    {
        GameObject textObject = CreateUIObject("Text", parent);
        Text text = textObject.AddComponent<Text>();
        text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        text.text = content;
        text.fontSize = fontSize;
        text.color = color;
        text.alignment = TextAnchor.UpperLeft;
        text.horizontalOverflow = HorizontalWrapMode.Wrap;
        text.verticalOverflow = VerticalWrapMode.Truncate;
        SetRect(textObject, new Vector2(0f, 1f), new Vector2(0f, 1f), position, size, new Vector2(0f, 1f));
        return text;
    }

    private InputField CreateInputField(Transform parent, string objectName, string initialValue, Vector2 position, Vector2 size)
    {
        GameObject fieldObject = CreateUIObject(objectName, parent);
        Image background = fieldObject.AddComponent<Image>();
        background.color = new Color(0.12f, 0.15f, 0.2f, 1f);
        InputField input = fieldObject.AddComponent<InputField>();
        Text text = CreateText(fieldObject.transform, initialValue, 13, Color.white, new Vector2(7f, 0f), new Vector2(size.x - 14f, size.y));
        text.alignment = TextAnchor.MiddleLeft;
        text.horizontalOverflow = HorizontalWrapMode.Overflow;
        text.verticalOverflow = VerticalWrapMode.Truncate;
        input.textComponent = text;
        input.text = initialValue;
        SetRect(fieldObject, new Vector2(0f, 1f), new Vector2(0f, 1f), position, size, new Vector2(0f, 1f));
        return input;
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

    private void SetRect(GameObject target, Vector2 anchorMin, Vector2 anchorMax, Vector2 position, Vector2 size, Vector2 pivot)
    {
        RectTransform rect = target.GetComponent<RectTransform>();
        rect.anchorMin = anchorMin;
        rect.anchorMax = anchorMax;
        rect.pivot = pivot;
        rect.anchoredPosition = position;
        rect.sizeDelta = size;
    }

    private static void DestroyRuntimeMaterial(Material material)
    {
        if (material != null)
        {
            Destroy(material);
        }
    }
}
