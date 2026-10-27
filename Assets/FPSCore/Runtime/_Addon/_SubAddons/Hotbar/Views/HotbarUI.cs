using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Shows a <see cref="Hotbar"/>. Two ways to get the UI:
/// - Code: this script draws the whole bar (canvas, layout, slots).
/// - Scene: you build the container in the scene and assign it below (slots come from a HotbarSlotView prefab).
/// </summary>
public class HotbarUI : MonoBehaviour
{
    public enum BuildMode
    {
        Code,
        Scene,
    }

    [Header("Source")]
    [Tooltip("Hotbar to show. Empty = auto-found.")]
    [SerializeField]
    Hotbar hotbar;

    [Header("Mode")]
    [Tooltip("Code = this script draws the hotbar. Scene = use the UI referenced below.")]
    [SerializeField]
    BuildMode buildMode = BuildMode.Code;

    [Header("Code UI (Code mode)")]
    [Tooltip("Slot edge length (px).")]
    [SerializeField]
    float slotSize = 64f;

    [SerializeField]
    float border = 3f;

    [Tooltip("Gap between border and icon (px).")]
    [SerializeField]
    float iconPadding = 6f;

    [Tooltip("Gap between slots (px).")]
    [SerializeField]
    float spacing = 8f;

    [Tooltip("Distance from the bottom of the screen (px).")]
    [SerializeField]
    float bottomMargin = 24f;

    [Tooltip("Slot background. Alpha 0 = border only.")]
    [SerializeField]
    Color backColor = new Color(0f, 0f, 0f, 0.3f);

    [SerializeField]
    Color borderColor = new Color(1f, 1f, 1f, 0.5f);

    [Tooltip("Border color of the equipped slot.")]
    [SerializeField]
    Color selectedColor = new Color(1f, 0.85f, 0.2f, 1f);

    [SerializeField]
    Color labelColor = Color.white;

    [Tooltip("Empty = TextMeshPro default font (needs TMP Essential Resources imported).")]
    [SerializeField]
    TMP_FontAsset font;

    [Header("Scene UI (Scene mode)")]
    [Tooltip("Parent of the slots (give it a Horizontal Layout Group).")]
    [SerializeField]
    RectTransform slotContainer;

    [Tooltip("One of these is created per hotbar slot.")]
    [SerializeField]
    HotbarSlotView slotPrefab;

    class Slot
    {
        public HotbarSlotView view;
        public RectTransform rect,
            back,
            content;
        public Image backImg;
        public TMP_Text label,
            count;
    }

    Hotbar _bound;
    Slot[] _slots;
    RectTransform _container; // where slots go (code: own bar, scene: slotContainer)
    float _nextFind;

    void Awake()
    {
        if (buildMode == BuildMode.Scene)
        {
            if (slotContainer == null || slotPrefab == null)
            {
                Debug.LogWarning(
                    "HotbarUI: Scene mode needs Slot Container and Slot Prefab.",
                    this
                );
                enabled = false;
                return;
            }
            if (slotContainer.GetComponent<LayoutGroup>() == null)
                Debug.LogWarning("HotbarUI: Slot Container needs a Layout Group.", this);
            _container = slotContainer;
        }
        else
            _container = BuildCodeRoot();
    }

    void OnEnable()
    {
        TryBind();
    }

    void OnDisable()
    {
        Unbind();
        SetSlotsVisible(false);
    }

    void OnDestroy()
    {
        Unbind();
        DestroySlots();
    }

    void Update()
    {
        if (_bound != null)
            return;

        // Source missing/destroyed: hide and keep looking (once a second).
        SetSlotsVisible(false);
        if (Time.unscaledTime < _nextFind)
            return;
        _nextFind = Time.unscaledTime + 1f;
        TryBind();
    }

    void TryBind()
    {
        if (_bound != null || _container == null)
            return;
        if (hotbar == null)
            hotbar = FindFirstObjectByType<Hotbar>();
        if (hotbar == null)
            return;

        if (_slots == null || _slots.Length != hotbar.Size)
            Build(hotbar.Size);

        SetSlotsVisible(true);
        _bound = hotbar;
        _bound.Changed += Refresh;
        Refresh();
    }

    void Unbind()
    {
        if (_bound != null)
            _bound.Changed -= Refresh;
        _bound = null;
    }

    // Code mode: canvas > bar (bottom-center, sized by its layout group).
    RectTransform BuildCodeRoot()
    {
        var canvasGo = new GameObject(
            "Hotbar Canvas",
            typeof(RectTransform),
            typeof(Canvas),
            typeof(CanvasScaler)
        );
        canvasGo.transform.SetParent(transform, false);
        var canvas = canvasGo.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 50;
        var scaler = canvasGo.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 0.5f;

        var bar = NewRect("Bar", canvasGo.transform);
        bar.anchorMin = bar.anchorMax = bar.pivot = new Vector2(0.5f, 0f);
        bar.anchoredPosition = new Vector2(0f, bottomMargin);
        var g = bar.gameObject.AddComponent<HorizontalLayoutGroup>();
        g.spacing = spacing;
        g.childAlignment = TextAnchor.LowerCenter;
        g.childControlWidth = g.childControlHeight = true;
        g.childForceExpandWidth = g.childForceExpandHeight = false;
        var fit = bar.gameObject.AddComponent<ContentSizeFitter>();
        fit.horizontalFit = fit.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
        return bar;
    }

    void Build(int count)
    {
        DestroySlots();
        _slots = new Slot[count];
        for (int i = 0; i < count; i++)
            _slots[i] = buildMode == BuildMode.Code ? NewCodeSlot(i) : NewSceneSlot(i);
        Layout();
    }

    void DestroySlots()
    {
        if (_slots == null)
            return;
        foreach (var s in _slots)
        {
            if (s == null || s.rect == null)
                continue;
            s.rect.gameObject.SetActive(false); // so the layout ignores it until Destroy runs
            Destroy(s.rect.gameObject);
        }
        _slots = null;
    }

    void SetSlotsVisible(bool on)
    {
        if (_slots == null)
            return;
        foreach (var s in _slots)
            if (s != null && s.rect != null && s.rect.gameObject.activeSelf != on)
                s.rect.gameObject.SetActive(on);
    }

    Slot NewSceneSlot(int i)
    {
        var view = Instantiate(slotPrefab, _container);
        view.name = "Slot" + (i + 1);
        return new Slot { view = view, rect = (RectTransform)view.transform };
    }

    Slot NewCodeSlot(int i)
    {
        var s = new Slot { rect = NewRect("Slot" + (i + 1), _container) };
        var borderImg = NewImage(s.rect);

        s.back = NewRect("Back", s.rect);
        s.backImg = NewImage(s.back);

        s.content = NewRect("Content", s.back);
        Stretch(s.content);
        var icon = NewImage(s.content);
        icon.preserveAspect = true;
        icon.enabled = false;

        s.label = NewText("Label", s.content, TextAlignmentOptions.Center);
        s.label.enableAutoSizing = true;
        s.label.fontSizeMin = 8f;
        s.label.fontSizeMax = 16f;
        s.label.overflowMode = TextOverflowModes.Truncate;
        s.label.enabled = false;

        s.count = NewText("Count", s.content, TextAlignmentOptions.BottomRight);
        s.count.fontSize = 18f;
        s.count.enabled = false;

        s.view = s.rect.gameObject.AddComponent<HotbarSlotView>();
        s.view.Assign(borderImg, icon, s.label, s.count, borderColor, selectedColor);
        return s;
    }

    // Code mode only: sizes and colors (also the live tweak path).
    void Layout()
    {
        if (buildMode != BuildMode.Code || _slots == null)
            return;

        foreach (var s in _slots)
        {
            s.rect.sizeDelta = new Vector2(slotSize, slotSize); // used when the group doesn't control child size
            var le = s.rect.GetComponent<LayoutElement>();
            if (le == null)
                le = s.rect.gameObject.AddComponent<LayoutElement>();
            le.preferredWidth = le.minWidth = slotSize;
            le.preferredHeight = le.minHeight = slotSize;
            le.flexibleWidth = le.flexibleHeight = 0f;

            Stretch(s.back);
            s.back.offsetMin = new Vector2(border, border);
            s.back.offsetMax = new Vector2(-border, -border);
            s.backImg.color = backColor;

            s.content.offsetMin = new Vector2(iconPadding, iconPadding);
            s.content.offsetMax = new Vector2(-iconPadding, -iconPadding);
            s.label.color = labelColor;
            s.count.color = labelColor;
            s.view.SetColors(borderColor, selectedColor);
        }

        if (_container != null && _container.TryGetComponent<HorizontalLayoutGroup>(out var g))
            g.spacing = spacing;
    }

    void Refresh()
    {
        if (_slots == null || _bound == null)
            return;

        for (int i = 0; i < _slots.Length; i++)
        {
            var def = _bound.GetDefinition(i);
            _slots[i]
                .view.Set(
                    i == _bound.Current,
                    def != null ? def.Icon : null,
                    def != null ? def.DisplayName : string.Empty,
                    _bound.GetCount(i)
                );
        }
    }

    TMP_Text NewText(string n, Transform parent, TextAlignmentOptions align)
    {
        var rt = NewRect(n, parent);
        Stretch(rt);
        var tmp = rt.gameObject.AddComponent<TextMeshProUGUI>();
        if (font != null)
            tmp.font = font;
        tmp.alignment = align;
        tmp.raycastTarget = false;
        return tmp;
    }

    static RectTransform NewRect(string n, Transform parent)
    {
        var go = new GameObject(n, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        return (RectTransform)go.transform;
    }

    static Image NewImage(RectTransform rt)
    {
        var img = rt.gameObject.AddComponent<Image>();
        img.raycastTarget = false;
        return img;
    }

    static void Stretch(RectTransform rt)
    {
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = rt.offsetMax = Vector2.zero;
    }

    void OnValidate()
    {
        slotSize = Mathf.Max(8f, slotSize);
        border = Mathf.Clamp(border, 0f, slotSize * 0.4f);
        iconPadding = Mathf.Clamp(iconPadding, 0f, slotSize * 0.4f);
        spacing = Mathf.Max(0f, spacing);
        if (Application.isPlaying)
        {
            Layout(); // live tweak in Inspector
            Refresh();
        }
    }
}
