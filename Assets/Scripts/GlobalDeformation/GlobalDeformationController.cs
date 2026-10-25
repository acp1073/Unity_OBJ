using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>Owns the runtime mesh, UI and control cage; imported assets remain unchanged.</summary>
public sealed class GlobalDeformationController : MonoBehaviour
{
    public const string SceneName = "GlobalDeformationScene";
    public GlobalDeformationMode Mode { get; private set; }
    public DeformationAxis Axis { get; private set; } = DeformationAxis.Y;
    public FfdLattice Lattice { get; private set; }
    public Mesh DisplayMesh { get; private set; }
    public Vector3[] RestVertices { get; private set; }
    public Bounds RestBounds { get; private set; }
    public int SelectedPoint { get; private set; } = -1;
    public float Amount { get; private set; }

    private Camera viewCamera;
    private ObjOrbitCamera orbit;
    private DeformationUI ui;
    private MeshRenderer modelRenderer;
    private Material modelMaterial, cageMaterial, pointMaterial, selectedMaterial;
    private GameObject modelObject, cageRoot;
    private Mesh cageMesh;
    private readonly List<Transform> handles = new List<Transform>();
    private int[] renderSourceMap;
    private Vector3[] deformedSource, renderVertices;
    private Vector3[] restNormals;
    private int resolution = 3;
    private InputField pathInput, nInput;
    private readonly InputField[] indexInput = new InputField[3];
    private readonly InputField[] coordinateInput = new InputField[3];
    private Text status, statistics, amountLabel, selectedLabel;
    private Slider amountSlider;
    private GameObject analyticPanel, ffdPanel;
    private readonly Button[] modeButtons = new Button[4];
    private readonly Button[] axisButtons = new Button[3];
    private bool dragging;
    private Plane dragPlane;
    private Vector3 dragOffset;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Bootstrap()
    {
        if (SceneManager.GetActiveScene().name == SceneName && FindObjectOfType<GlobalDeformationController>() == null)
            new GameObject("Global Deformation Demo").AddComponent<GlobalDeformationController>();
    }

    private void Awake()
    {
        if (FindObjectOfType<EventSystem>() == null)
            new GameObject("EventSystem", typeof(EventSystem), typeof(StandaloneInputModule));
        GameObject cameraObject = new GameObject("Deformation Camera", typeof(Camera), typeof(AudioListener));
        cameraObject.transform.SetParent(transform, false);
        viewCamera = cameraObject.GetComponent<Camera>();
        viewCamera.tag = "MainCamera";
        viewCamera.clearFlags = CameraClearFlags.SolidColor;
        viewCamera.backgroundColor = new Color(0.065f, 0.09f, 0.13f);
        viewCamera.fieldOfView = 45;
        viewCamera.nearClipPlane = 0.01f;
        viewCamera.farClipPlane = 1000;
        orbit = cameraObject.AddComponent<ObjOrbitCamera>();
        Material template = Resources.Load<Material>("GlobalDeformation/ModelMaterial");
        Shader shader = Shader.Find("OBJ Viewer/DoubleSidedLit") ?? Shader.Find("Standard");
        modelMaterial = template != null ? new Material(template) : new Material(shader);
        modelMaterial.color = new Color(0.3f, 0.72f, 0.86f);
        cageMaterial = Unlit(new Color(0.23f, 0.54f, 0.67f));
        pointMaterial = Unlit(new Color(0.28f, 0.85f, 0.95f));
        selectedMaterial = Unlit(new Color(1f, 0.73f, 0.18f));
        GameObject lightObject = new GameObject("Deformation Key Light", typeof(Light));
        lightObject.transform.SetParent(transform, false);
        Light light = lightObject.GetComponent<Light>();
        light.type = LightType.Directional;
        light.intensity = 1.3f;
        lightObject.transform.rotation = Quaternion.Euler(35, -30, 0);
        RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
        RenderSettings.ambientLight = new Color(0.36f, 0.4f, 0.48f);
        ui = new DeformationUI(transform);
        BuildInterface();
        SetMode(GlobalDeformationMode.Taper);
    }

    private void Start() { LoadSample(); }

    public void LoadSample()
    {
        LoadModelFromPath(Path.Combine(Application.streamingAssetsPath, "DeformationSample.obj"));
    }

    public bool LoadModelFromPath(string path)
    {
        Mesh newMesh = null;
        try
        {
            path = path.Trim().Trim('"');
            if (!Path.IsPathRooted(path))
            {
                string root = path.StartsWith("Assets/", StringComparison.OrdinalIgnoreCase) ||
                    path.StartsWith("Assets\\", StringComparison.OrdinalIgnoreCase)
                    ? Directory.GetParent(Application.dataPath).FullName : Application.streamingAssetsPath;
                path = Path.Combine(root, path);
            }
            ObjMeshData data = ObjParser.ParseFile(path);
            foreach (Vector3 v in data.Positions)
                if (!Finite(v.x) || !Finite(v.y) || !Finite(v.z)) throw new FormatException("OBJ 包含无效顶点坐标。");
            ObjMeshBuildResult build = ObjParser.BuildUnityMeshResult(data);
            newMesh = build.Mesh;
            Bounds bounds = new Bounds(data.Positions[0], Vector3.zero);
            foreach (Vector3 v in data.Positions) bounds.Encapsulate(v);
            float longest = Mathf.Max(bounds.size.x, Mathf.Max(bounds.size.y, bounds.size.z));
            float scale = longest > 0.00001f ? 4f / longest : 1f;
            Vector3[] rest = new Vector3[data.Positions.Count];
            for (int i = 0; i < rest.Length; i++) rest[i] = (data.Positions[i] - bounds.center) * scale;
            Bounds normalized = new Bounds(Vector3.zero, bounds.size * scale);
            FfdLattice lattice = new FfdLattice(rest, normalized, resolution);

            // Parse and prepare first; a failed load keeps the displayed model intact.
            if (DisplayMesh != null) Destroy(DisplayMesh);
            if (modelObject != null) Destroy(modelObject);
            DisplayMesh = newMesh;
            newMesh = null;
            DisplayMesh.MarkDynamic();
            renderSourceMap = build.RenderVertexSourceIndices;
            RestVertices = rest;
            RestBounds = normalized;
            restNormals = DisplayMesh.normals;
            deformedSource = new Vector3[rest.Length];
            renderVertices = new Vector3[renderSourceMap.Length];
            Lattice = lattice;
            modelObject = new GameObject(data.ObjectName, typeof(MeshFilter), typeof(MeshRenderer));
            modelObject.transform.SetParent(transform, false);
            modelObject.GetComponent<MeshFilter>().sharedMesh = DisplayMesh;
            modelRenderer = modelObject.GetComponent<MeshRenderer>();
            modelRenderer.sharedMaterial = modelMaterial;
            ObjLoadedModel loaded = modelObject.AddComponent<ObjLoadedModel>();
            loaded.Initialize(data, DisplayMesh, renderSourceMap);
            pathInput.text = path;
            Amount = 0;
            amountSlider.SetValueWithoutNotify(0);
            RebuildCage();
            ApplyDeformation();
            FocusModel();
            statistics.text = Path.GetFileName(path) + "\n原始顶点 " + rest.Length + "  /  三角形 " + data.TriangleCount;
            SetStatus("已导入。选择变形模式后调整参数。\n所有变形都从原始模型重新计算。");
            return true;
        }
        catch (Exception exception)
        {
            if (newMesh != null) Destroy(newMesh);
            SetStatus("加载失败：" + exception.Message, true);
            return false;
        }
    }

    public void SetMode(GlobalDeformationMode mode)
    {
        dragging = false;
        Mode = mode;
        Amount = 0;
        amountSlider.minValue = mode == GlobalDeformationMode.Taper ? -0.95f : mode == GlobalDeformationMode.Twist ? -360 : -150;
        amountSlider.maxValue = mode == GlobalDeformationMode.Taper ? 1.5f : mode == GlobalDeformationMode.Twist ? 360 : 150;
        amountSlider.SetValueWithoutNotify(0);
        analyticPanel.SetActive(mode != GlobalDeformationMode.FFD);
        ffdPanel.SetActive(mode == GlobalDeformationMode.FFD);
        if (cageRoot != null) cageRoot.SetActive(mode == GlobalDeformationMode.FFD);
        for (int i = 0; i < modeButtons.Length; i++) Highlight(modeButtons[i], i == (int)mode);
        ApplyDeformation();
        SetStatus(mode == GlobalDeformationMode.FFD
            ? "左键选择并拖动控制点；也可输入 XYZ。\n拖动平面平行于屏幕，转动视角可改变编辑方向。"
            : "沿所选轴从包围框底端到顶端施加变形。\n切换模式会将强度归零，不叠加前一种变形。");
    }

    public void SetAxis(DeformationAxis axis)
    {
        Axis = axis;
        for (int i = 0; i < 3; i++) Highlight(axisButtons[i], i == (int)axis);
        ApplyDeformation();
    }

    public void SetAmount(float value)
    {
        Amount = Mathf.Clamp(value, amountSlider.minValue, amountSlider.maxValue);
        amountSlider.SetValueWithoutNotify(Amount);
        ApplyDeformation();
    }

    public void ResetDeformation()
    {
        Amount = 0;
        amountSlider.SetValueWithoutNotify(0);
        if (Lattice != null) Lattice.Reset();
        UpdateCage();
        ApplyDeformation();
        SetStatus("已恢复原始模型及规则控制网格。");
    }

    public void SetResolution(int n)
    {
        if (n < 2 || n > 6) throw new ArgumentOutOfRangeException(nameof(n), "n 必须在 2 到 6 之间。");
        resolution = n;
        nInput.text = n.ToString();
        if (RestVertices == null) return;
        Lattice = new FfdLattice(RestVertices, RestBounds, resolution);
        RebuildCage();
        ApplyDeformation();
        SetStatus("已由原始包围框重建 " + n + "×" + n + "×" + n + " 网格；此前控制点位移已复位。");
    }

    public void SelectPoint(int index)
    {
        if (Lattice == null || index < 0 || index >= Lattice.Points.Length) return;
        SelectedPoint = index;
        RefreshSelection();
    }

    public void SetControlPoint(int index, Vector3 position)
    {
        if (Lattice == null || index < 0 || index >= Lattice.Points.Length) throw new ArgumentOutOfRangeException(nameof(index));
        if (!Finite(position.x) || !Finite(position.y) || !Finite(position.z)) throw new ArgumentException("坐标必须是有限数值。");
        Lattice.Points[index] = position;
        UpdateCage();
        if (Mode == GlobalDeformationMode.FFD) ApplyDeformation();
    }

    public void FocusModel()
    {
        if (modelRenderer == null) return;
        Bounds bounds = modelRenderer.bounds;
        if (Mode == GlobalDeformationMode.FFD && cageRoot != null)
            bounds.Encapsulate(cageRoot.GetComponent<Renderer>().bounds);
        orbit.Focus(bounds);
    }

    private void ApplyDeformation()
    {
        amountLabel.text = Mode == GlobalDeformationMode.Taper ? "锥化系数  " + Amount.ToString("F2")
            : "总角度  " + Amount.ToString("F1") + "°";
        if (RestVertices == null) return;
        if (Mode == GlobalDeformationMode.FFD) Lattice.Evaluate(deformedSource);
        else
            for (int i = 0; i < RestVertices.Length; i++)
                deformedSource[i] = DeformationMath.Evaluate(RestVertices[i], RestBounds, Mode, Axis, Amount);
        for (int i = 0; i < renderVertices.Length; i++) renderVertices[i] = deformedSource[renderSourceMap[i]];
        DisplayMesh.vertices = renderVertices;
        DisplayMesh.RecalculateBounds();
        if (Mode != GlobalDeformationMode.FFD && Mathf.Abs(Amount) < 0.00001f) DisplayMesh.normals = restNormals;
        else DisplayMesh.RecalculateNormals();
        if (DisplayMesh.uv.Length == renderVertices.Length) DisplayMesh.RecalculateTangents();
    }

    private void RebuildCage()
    {
        if (cageRoot != null) Destroy(cageRoot);
        if (cageMesh != null) Destroy(cageMesh);
        handles.Clear();
        cageRoot = new GameObject("FFD Control Lattice");
        cageRoot.transform.SetParent(transform, false);
        cageMesh = new Mesh { name = "FFD lattice edges" };
        cageMesh.MarkDynamic();
        cageRoot.AddComponent<MeshFilter>().sharedMesh = cageMesh;
        cageRoot.AddComponent<MeshRenderer>().sharedMaterial = cageMaterial;
        int n = Lattice.Resolution;
        var edges = new List<int>();
        for (int i = 0; i < n; i++)
            for (int j = 0; j < n; j++)
                for (int k = 0; k < n; k++)
                {
                    int a = Lattice.Index(i, j, k);
                    if (i + 1 < n) { edges.Add(a); edges.Add(Lattice.Index(i + 1, j, k)); }
                    if (j + 1 < n) { edges.Add(a); edges.Add(Lattice.Index(i, j + 1, k)); }
                    if (k + 1 < n) { edges.Add(a); edges.Add(Lattice.Index(i, j, k + 1)); }
                    GameObject handle = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                    handle.name = "P[" + i + "," + j + "," + k + "]";
                    Destroy(handle.GetComponent<Collider>());
                    handle.transform.SetParent(cageRoot.transform, false);
                    handle.transform.localScale = Vector3.one * 0.065f;
                    handles.Add(handle.transform);
                }
        cageMesh.vertices = Lattice.Points;
        cageMesh.SetIndices(edges.ToArray(), MeshTopology.Lines, 0);
        cageRoot.SetActive(Mode == GlobalDeformationMode.FFD);
        SelectedPoint = Lattice.Index(n - 1, n - 1, n - 1);
        UpdateCage();
    }

    private void UpdateCage()
    {
        if (cageMesh == null || Lattice == null) return;
        cageMesh.vertices = Lattice.Points;
        cageMesh.RecalculateBounds();
        for (int i = 0; i < handles.Count; i++) handles[i].localPosition = Lattice.Points[i];
        RefreshSelection();
    }

    private void RefreshSelection()
    {
        if (SelectedPoint < 0 || Lattice == null) return;
        int n = Lattice.Resolution;
        int[] indices = { SelectedPoint / (n * n), (SelectedPoint / n) % n, SelectedPoint % n };
        Vector3 p = Lattice.Points[SelectedPoint];
        for (int i = 0; i < 3; i++)
        {
            indexInput[i].text = indices[i].ToString();
            coordinateInput[i].text = p[i].ToString("F4", CultureInfo.InvariantCulture);
        }
        selectedLabel.text = "P[" + indices[0] + ", " + indices[1] + ", " + indices[2] + "]  /  " + (n * n * n) + " 个控制点";
        for (int i = 0; i < handles.Count; i++)
            handles[i].GetComponent<Renderer>().sharedMaterial = i == SelectedPoint ? selectedMaterial : pointMaterial;
    }

    private void Update()
    {
        // Keep the model viewport separate from the controls at any window aspect ratio.
        float left = Mathf.Clamp(376f * ui.Canvas.scaleFactor / Mathf.Max(1, Screen.width), 0.1f, 0.75f);
        viewCamera.rect = new Rect(left, 0, 1f - left, 1);
        if (Mode != GlobalDeformationMode.FFD || Lattice == null) return;
        if (Input.GetMouseButtonUp(0)) dragging = false;
        if (Input.GetMouseButtonDown(0) &&
            (EventSystem.current == null || !EventSystem.current.IsPointerOverGameObject()) && viewCamera.pixelRect.Contains(Input.mousePosition))
        {
            int nearest = -1;
            float best = 16f * 16f, depth = float.MaxValue;
            for (int i = 0; i < handles.Count; i++)
            {
                Vector3 screen = viewCamera.WorldToScreenPoint(handles[i].position);
                if (screen.z <= 0) continue;
                float d = ((Vector2)screen - (Vector2)Input.mousePosition).sqrMagnitude;
                if (d < best - 1f || (d <= best && screen.z < depth)) { best = d; depth = screen.z; nearest = i; }
            }
            if (nearest >= 0)
            {
                SelectPoint(nearest);
                dragPlane = new Plane(viewCamera.transform.forward, handles[nearest].position);
                Ray ray = viewCamera.ScreenPointToRay(Input.mousePosition);
                float distance;
                if (dragPlane.Raycast(ray, out distance))
                {
                    dragOffset = handles[nearest].position - ray.GetPoint(distance);
                    dragging = true;
                }
            }
        }
        if (dragging && Input.GetMouseButton(0))
        {
            Ray ray = viewCamera.ScreenPointToRay(Input.mousePosition);
            float distance;
            if (dragPlane.Raycast(ray, out distance))
                SetControlPoint(SelectedPoint, transform.InverseTransformPoint(ray.GetPoint(distance) + dragOffset));
        }
    }

    private void BuildInterface()
    {
        Transform p = ui.Panel;
        ui.Label(p, "全局变形实验室", 16, 14, 312, 34, 25);
        ui.Label(p, "OBJ  /  GLOBAL DEFORMATION", 16, 50, 312, 22, 12);
        pathInput = ui.Input(p, "", 16, 88, 312);
        ui.Button(p, "选择 OBJ", 16, 126, 96, ChooseFile);
        ui.Button(p, "导入路径", 124, 126, 96, () => LoadModelFromPath(pathInput.text));
        ui.Button(p, "示例模型", 232, 126, 96, LoadSample);
        statistics = ui.Label(p, "", 16, 166, 312, 50, 13);
        ui.Label(p, "01  选择变形", 16, 228, 312, 28, 17);
        string[] titles = { "锥化 Taper", "扭曲 Twist", "弯曲 Bend", "FFD 自由变形" };
        for (int i = 0; i < 4; i++)
        {
            int mode = i;
            modeButtons[i] = ui.Button(p, titles[i], 16 + (i % 2) * 160, 268 + (i / 2) * 40, 152,
                () => SetMode((GlobalDeformationMode)mode));
        }
        analyticPanel = DeformationUI.Element("Analytic Controls", p, new Vector2(0, -354), new Vector2(344, 340));
        Transform a = analyticPanel.transform;
        ui.Label(a, "02  变形参数", 16, 0, 312, 28, 17);
        ui.Label(a, "模型局部轴", 16, 40);
        for (int i = 0; i < 3; i++)
        {
            int axis = i;
            axisButtons[i] = ui.Button(a, ((DeformationAxis)i).ToString(), 16 + i * 106, 72, 100,
                () => SetAxis((DeformationAxis)axis));
        }
        amountLabel = ui.Label(a, "强度", 16, 122);
        amountSlider = ui.Slider(a, 20, 158, 300, SetAmount);
        ui.Label(a, "锥化：底端不变，顶端按 1 + 系数 缩放。\n扭曲：底端不转，顶端达到设置角度。\n弯曲：以底端为基准，沿圆弧弯曲。", 16, 205, 312, 96, 14);
        Highlight(axisButtons[1], true);

        ffdPanel = DeformationUI.Element("FFD Controls", p, new Vector2(0, -354), new Vector2(344, 340));
        Transform f = ffdPanel.transform;
        ui.Label(f, "02  FFD 控制网格", 16, 0, 312, 28, 17);
        ui.Label(f, "每轴点数 n（2–6）", 16, 40, 170);
        nInput = ui.Input(f, "3", 187, 37, 44);
        ui.Button(f, "重建", 239, 36, 89, RebuildFromInput);
        selectedLabel = ui.Label(f, "", 16, 82);
        ui.Label(f, "索引 i / j / k（从 0 开始）", 16, 115, 312, 24, 13);
        for (int i = 0; i < 3; i++) indexInput[i] = ui.Input(f, "0", 16 + i * 60, 144, 52);
        ui.Button(f, "选择点", 204, 143, 124, SelectFromInput);
        ui.Label(f, "控制点局部坐标  X / Y / Z", 16, 185, 312, 24, 13);
        for (int i = 0; i < 3; i++) coordinateInput[i] = ui.Input(f, "0", 16 + i * 106, 214, 100);
        ui.Button(f, "应用坐标", 16, 253, 152, ApplyCoordinates);
        ui.Button(f, "复位此点", 176, 253, 152, () =>
        {
            if (SelectedPoint >= 0) SetControlPoint(SelectedPoint, Lattice.RestPoints[SelectedPoint]);
        });
        ui.Label(f, "青色为控制点，黄色为当前选中点。\n坐标对应居中、等比缩放后的模型空间。", 16, 295, 312, 44, 13);
        ui.Button(p, "复位全部", 16, 704, 152, ResetDeformation);
        ui.Button(p, "聚焦模型", 176, 704, 152, FocusModel);
        status = ui.Label(p, "", 16, 749, 312, 64, 13);
        ui.Label(p, "左键拖点 · 右键旋转 · 中键平移 · 滚轮缩放", 16, 825, 312, 26, 12);
    }

    private void RebuildFromInput()
    {
        int n;
        if (!int.TryParse(nInput.text, out n) || n < 2 || n > 6) { SetStatus("n 必须是 2–6 的整数。", true); return; }
        SetResolution(n);
    }

    private void SelectFromInput()
    {
        if (Lattice == null) return;
        int[] values = new int[3];
        for (int i = 0; i < 3; i++)
            if (!int.TryParse(indexInput[i].text, out values[i]) || values[i] < 0 || values[i] >= resolution)
            { SetStatus("索引必须在 0 到 n−1 之间。", true); return; }
        SelectPoint(Lattice.Index(values[0], values[1], values[2]));
    }

    private void ApplyCoordinates()
    {
        if (SelectedPoint < 0) return;
        Vector3 p = Vector3.zero;
        for (int i = 0; i < 3; i++)
        {
            float value;
            if (!float.TryParse(coordinateInput[i].text, NumberStyles.Float, CultureInfo.InvariantCulture, out value) || !Finite(value))
            { SetStatus("XYZ 请输入有效数字，小数点使用英文句点。", true); return; }
            p[i] = value;
        }
        SetControlPoint(SelectedPoint, p);
        SetStatus("控制点已更新，模型已重新计算。");
    }

    private void ChooseFile()
    {
#if UNITY_EDITOR
        string path = UnityEditor.EditorUtility.OpenFilePanel("选择 OBJ 模型", Application.dataPath, "obj");
        if (!string.IsNullOrEmpty(path)) LoadModelFromPath(path);
#else
        SetStatus("请输入 OBJ 的绝对路径，然后点击“导入路径”。\n相对路径以 StreamingAssets 为起点。");
        pathInput.Select();
        pathInput.ActivateInputField();
#endif
    }

    private static Material Unlit(Color color)
    {
        Material template = Resources.Load<Material>("GlobalDeformation/ControlMaterial");
        Material material = template != null ? new Material(template) : new Material(Shader.Find("Unlit/Color"));
        material.color = color;
        return material;
    }
    private static bool Finite(float value) { return !float.IsNaN(value) && !float.IsInfinity(value); }
    private static void Highlight(Button button, bool active)
    {
        button.GetComponent<Image>().color = active ? new Color(0.10f, 0.5f, 0.59f) : new Color(0.12f, 0.25f, 0.36f);
    }

    private void SetStatus(string text, bool error = false)
    {
        if (status == null) return;
        status.text = text;
        status.color = error ? new Color(1, 0.55f, 0.4f) : new Color(0.68f, 0.79f, 0.86f);
    }

    private void OnDestroy()
    {
        if (DisplayMesh != null) Destroy(DisplayMesh);
        if (cageMesh != null) Destroy(cageMesh);
        if (modelMaterial != null) Destroy(modelMaterial);
        if (cageMaterial != null) Destroy(cageMaterial);
        if (pointMaterial != null) Destroy(pointMaterial);
        if (selectedMaterial != null) Destroy(selectedMaterial);
    }
}
