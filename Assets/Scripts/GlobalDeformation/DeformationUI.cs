using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

/// <summary>Small UGUI factory, shared only by the deformation demo.</summary>
public sealed class DeformationUI
{
    public readonly Canvas Canvas;
    public readonly Transform Panel;
    private readonly Font font;
    private static readonly Color Ink = new Color(0.84f, 0.9f, 0.97f);

    public DeformationUI(Transform parent)
    {
        font = Font.CreateDynamicFontFromOSFont(new[] { "Microsoft YaHei", "Noto Sans CJK SC", "Arial" }, 16);
        GameObject root = new GameObject("Deformation UI", typeof(RectTransform), typeof(Canvas),
            typeof(CanvasScaler), typeof(GraphicRaycaster));
        root.transform.SetParent(parent, false);
        Canvas = root.GetComponent<Canvas>();
        Canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        CanvasScaler scaler = root.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1440, 900);
        scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
        scaler.matchWidthOrHeight = 1f;
        Panel = Box("Controls", root.transform, new Vector2(16, -16), new Vector2(344, 868),
            new Color(0.045f, 0.065f, 0.10f, 0.98f)).transform;
    }

    public GameObject Box(string name, Transform parent, Vector2 position, Vector2 size, Color color)
    {
        GameObject obj = Element(name, parent, position, size);
        obj.AddComponent<Image>().color = color;
        return obj;
    }

    public Text Label(Transform parent, string text, float x, float y, float width = 312, float height = 24, int fontSize = 15)
    {
        GameObject obj = Element(text, parent, new Vector2(x, -y), new Vector2(width, height));
        Text label = obj.AddComponent<Text>();
        label.font = font;
        label.text = text;
        label.fontSize = fontSize;
        label.color = Ink;
        label.raycastTarget = false;
        label.alignment = TextAnchor.MiddleLeft;
        label.horizontalOverflow = HorizontalWrapMode.Wrap;
        label.verticalOverflow = VerticalWrapMode.Truncate;
        return label;
    }

    public Button Button(Transform parent, string title, float x, float y, float width, UnityAction action)
    {
        GameObject obj = Box(title, parent, new Vector2(x, -y), new Vector2(width, 32),
            new Color(0.12f, 0.25f, 0.36f));
        Button button = obj.AddComponent<Button>();
        button.onClick.AddListener(action);
        Text text = Label(obj.transform, title, 0, 0, width, 32, 14);
        text.alignment = TextAnchor.MiddleCenter;
        return button;
    }

    public InputField Input(Transform parent, string value, float x, float y, float width)
    {
        GameObject obj = Box("Input", parent, new Vector2(x, -y), new Vector2(width, 30),
            new Color(0.09f, 0.12f, 0.18f));
        InputField input = obj.AddComponent<InputField>();
        Text text = Label(obj.transform, "", 7, 0, width - 14, 30, 13);
        text.supportRichText = false;
        input.textComponent = text;
        input.text = value;
        return input;
    }

    public Slider Slider(Transform parent, float x, float y, float width, UnityAction<float> action)
    {
        GameObject obj = Element("Amount Slider", parent, new Vector2(x, -y), new Vector2(width, 26));
        Slider slider = obj.AddComponent<Slider>();
        Box("Track", obj.transform, new Vector2(0, -10), new Vector2(width, 6), new Color(0.15f, 0.22f, 0.3f));
        GameObject handleArea = Element("Handle Area", obj.transform, Vector2.zero, new Vector2(width, 26));
        GameObject handle = Box("Handle", handleArea.transform, Vector2.zero, new Vector2(16, 26),
            new Color(0.25f, 0.78f, 0.9f));
        slider.handleRect = handle.GetComponent<RectTransform>();
        slider.handleRect.sizeDelta = new Vector2(16, 0);
        slider.handleRect.pivot = new Vector2(0.5f, 0.5f);
        slider.targetGraphic = handle.GetComponent<Image>();
        slider.onValueChanged.AddListener(action);
        return slider;
    }

    public static GameObject Element(string name, Transform parent, Vector2 position, Vector2 size)
    {
        GameObject obj = new GameObject(name, typeof(RectTransform));
        obj.transform.SetParent(parent, false);
        RectTransform rect = obj.GetComponent<RectTransform>();
        rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0, 1);
        rect.anchoredPosition = position;
        rect.sizeDelta = size;
        return obj;
    }
}
