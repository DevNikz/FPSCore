using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Shows prompt lines ("[E] text", one per line) pushed over <see cref="PromptBus"/>.
/// It never references Interactable, Interactor, Grabber or Carrier: sources publish, this draws.
/// Each line is filtered by player state (PromptEntry.when) and its key comes from InputManager
/// (follows rebinds). Two ways to get the UI:
/// - Code: this script draws the whole prompt (canvas, panel, lines).
/// - Scene: you build the container in the scene and assign it below (lines come from a PromptLineView prefab).
/// </summary>
public class PromptUI : MonoBehaviour
{
    public enum BuildMode
    {
        Code,
        Scene,
    }

    [Header("Mode")]
    [Tooltip("Code = this script draws the prompt. Scene = use the UI referenced below.")]
    [SerializeField]
    BuildMode buildMode = BuildMode.Code;

    [Tooltip("Fallback key name, used only if InputManager has no binding to show for a line.")]
    [SerializeField]
    string keyLabel = "E";

    [Header("Code UI (Code mode)")]
    [Tooltip("Distance below the screen center (px), i.e. just under the crosshair.")]
    [SerializeField]
    float offsetBelowCenter = 60f;

    [SerializeField]
    float fontSize = 28f;

    [Tooltip("Gap between lines (px).")]
    [SerializeField]
    float spacing = 4f;

    [Tooltip("Gap between panel edge and text (px).")]
    [SerializeField]
    float padding = 12f;

    [Tooltip("Panel background. Alpha 0 = text only.")]
    [SerializeField]
    Color backColor = new Color(0f, 0f, 0f, 0.35f);

    [SerializeField]
    Color textColor = Color.white;

    [Tooltip("Empty = TextMeshPro default font (needs TMP Essential Resources imported).")]
    [SerializeField]
    TMP_FontAsset font;

    [Header("Scene UI (Scene mode)")]
    [Tooltip("Parent of the lines (give it a Vertical Layout Group).")]
    [SerializeField]
    RectTransform lineContainer;

    [Tooltip("One of these is created per visible prompt line.")]
    [SerializeField]
    PromptLineView linePrefab;

    [Tooltip("Optional. Shown only while there is a prompt.")]
    [SerializeField]
    GameObject panel;

    RectTransform _container; // where lines go (code: own panel, scene: lineContainer)
    GameObject _panelGo;
    readonly List<PromptLineView> _lines = new();

    void Awake()
    {
        if (buildMode == BuildMode.Scene)
        {
            if (lineContainer == null || linePrefab == null)
            {
                Debug.LogWarning(
                    "PromptUI: Scene mode needs Line Container and Line Prefab.",
                    this
                );
                enabled = false;
                return;
            }
            if (lineContainer.GetComponent<LayoutGroup>() == null)
                Debug.LogWarning("PromptUI: Line Container needs a Layout Group.", this);
            _container = lineContainer;
            _panelGo = panel;
        }
        else
            BuildCodeRoot();
    }

    void OnEnable()
    {
        PromptBus.Changed += OnPromptsChanged;
        OnPromptsChanged(PromptBus.CurrentLines, PromptBus.CurrentContext); // catch up
    }

    void OnDisable()
    {
        PromptBus.Changed -= OnPromptsChanged;
        HideAll();
    }

    void OnDestroy() => DestroyLines();

    void OnPromptsChanged(IReadOnlyList<PromptEntry> lines, PromptContext ctx)
    {
        if (_container == null)
            return;

        var input = InputManager.Instance;
        int shown = 0;
        for (int i = 0; i < lines.Count; i++)
        {
            var e = lines[i];
            if (!IsLineVisibleIn(e, ctx))
                continue;

            string key = input != null ? input.GetLabel(e.action) : null;
            if (string.IsNullOrEmpty(key))
                key = keyLabel;

            var view = GetLine(shown++);
            view.gameObject.SetActive(true);
            view.Set(key, e.text);
        }

        for (int i = shown; i < _lines.Count; i++)
            _lines[i].gameObject.SetActive(false);
        if (_panelGo != null)
            _panelGo.SetActive(shown > 0);
    }

    static bool IsLineVisibleIn(PromptEntry e, PromptContext ctx)
    {
        if (string.IsNullOrEmpty(e.text))
            return false;
        var when = e.when == 0 ? PromptContext.Any : e.when; // unset = any state
        return (when & ctx) != 0;
    }

    void HideAll()
    {
        foreach (var l in _lines)
            if (l != null)
                l.gameObject.SetActive(false);
        if (_panelGo != null)
            _panelGo.SetActive(false);
    }

    PromptLineView GetLine(int index)
    {
        while (_lines.Count <= index)
            _lines.Add(buildMode == BuildMode.Code ? NewCodeLine() : NewSceneLine());
        return _lines[index];
    }

    PromptLineView NewSceneLine()
    {
        var view = Instantiate(linePrefab, _container);
        view.name = "Line" + (_lines.Count + 1);
        return view;
    }

    PromptLineView NewCodeLine()
    {
        var rt = NewRect("Line" + (_lines.Count + 1), _container);
        var tmp = rt.gameObject.AddComponent<TextMeshProUGUI>();
        if (font != null)
            tmp.font = font;
        tmp.fontSize = fontSize;
        tmp.color = textColor;
        tmp.alignment = TextAlignmentOptions.Left;
        tmp.textWrappingMode = TextWrappingModes.NoWrap;
        tmp.raycastTarget = false;

        var view = rt.gameObject.AddComponent<PromptLineView>();
        view.Assign(tmp);
        return view;
    }

    void DestroyLines()
    {
        foreach (var l in _lines)
        {
            if (l == null)
                continue;
            l.gameObject.SetActive(false); // so the layout ignores it until Destroy runs
            Destroy(l.gameObject);
        }
        _lines.Clear();
    }

    // Code mode: canvas > panel (just below screen center, sized by its layout group).
    void BuildCodeRoot()
    {
        var canvasGo = new GameObject(
            "Prompt Canvas",
            typeof(RectTransform),
            typeof(Canvas),
            typeof(CanvasScaler)
        );
        canvasGo.transform.SetParent(transform, false);
        var canvas = canvasGo.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 40;
        var scaler = canvasGo.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 0.5f;

        var bar = NewRect("Panel", canvasGo.transform);
        bar.anchorMin = bar.anchorMax = new Vector2(0.5f, 0.5f);
        bar.pivot = new Vector2(0.5f, 1f);
        bar.anchoredPosition = new Vector2(0f, -offsetBelowCenter);

        var img = bar.gameObject.AddComponent<Image>();
        img.color = backColor;
        img.raycastTarget = false;

        var g = bar.gameObject.AddComponent<VerticalLayoutGroup>();
        g.spacing = spacing;
        g.padding = new RectOffset((int)padding, (int)padding, (int)padding, (int)padding);
        g.childAlignment = TextAnchor.UpperCenter;
        g.childControlWidth = g.childControlHeight = true;
        g.childForceExpandWidth = g.childForceExpandHeight = false;
        var fit = bar.gameObject.AddComponent<ContentSizeFitter>();
        fit.horizontalFit = fit.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        _container = bar;
        _panelGo = bar.gameObject;
        _panelGo.SetActive(false); // hidden until there is a prompt
    }

    static RectTransform NewRect(string n, Transform parent)
    {
        var go = new GameObject(n, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        return (RectTransform)go.transform;
    }

    void OnValidate()
    {
        if (string.IsNullOrEmpty(keyLabel))
            keyLabel = "E";
        fontSize = Mathf.Max(8f, fontSize);
        spacing = Mathf.Max(0f, spacing);
        padding = Mathf.Max(0f, padding);
    }
}
