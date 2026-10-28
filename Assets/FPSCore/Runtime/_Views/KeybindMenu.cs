using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Layouts;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

/// <summary>
/// Keybind menu for <see cref="InputRemapper"/>. Two ways to get the UI:
/// - Code: this script draws the whole menu (canvas, list, buttons).
/// - Scene: you build the UI in the scene and assign it below (rows come from a KeybindRow prefab).
/// Opening it frees the cursor and blocks gameplay input through InputManager.EnterUIMode.
/// </summary>
public class KeybindMenu : MonoBehaviour
{
    public enum BuildMode
    {
        Code,
        Scene,
    }

    [Header("Mode")]
    [Tooltip("Code = this script draws the menu. Scene = use the UI referenced below.")]
    [SerializeField]
    private BuildMode buildMode = BuildMode.Code;

    [Tooltip("Empty = found on the InputManager object.")]
    [SerializeField]
    private InputRemapper remapper;

    [Header("Open / Close")]
    [Tooltip("Key that toggles the menu. Empty = call Open() / Close() from your own code.")]
    [SerializeField]
    [InputControl(layout = "Button")]
    private string toggleKeyPath = "<Keyboard>/f1";

    [Header("Code UI (Code mode)")]
    [SerializeField]
    private string title = "Controls";

    [SerializeField]
    private Vector2 panelSize = new(850f, 850f);

    [Tooltip("Empty = TextMeshPro default font (needs TMP Essential Resources imported).")]
    [SerializeField]
    private TMP_FontAsset font;

    [Header("Row Layout (Code mode rows)")]
    [Tooltip("Space around the content of each built-in row (left, right, top, bottom).")]
    [SerializeField]
    private RectOffset rowPadding;

    [Tooltip("Space between the label and the buttons inside each built-in row.")]
    [SerializeField]
    private float rowSpacing = 15f;

    [Header("Scene UI (Scene mode)")]
    [Tooltip("Whole menu. Shown on Open, hidden on Close.")]
    [SerializeField]
    private GameObject panelRoot;

    [Tooltip("Parent of the rows (give it a Vertical Layout Group).")]
    [SerializeField]
    private RectTransform rowContainer;

    [SerializeField]
    private KeybindRow rowPrefab;

    [SerializeField]
    private Button resetAllButton;

    [SerializeField]
    private Button closeButton;

    [Tooltip("Optional full-screen blocker shown while waiting for a key.")]
    [SerializeField]
    private GameObject listeningOverlay;

    [Tooltip("Optional text inside the overlay.")]
    [SerializeField]
    private TMP_Text listeningText;

    [Tooltip("Optional line for messages (swapped keys, locked keys).")]
    [SerializeField]
    private TMP_Text statusText;

    [Tooltip(
        "On = turns off Raycast Target on every graphic under Panel Root that isn't a button, scroll area or the listening overlay."
    )]
    [SerializeField]
    private bool disableSceneRaycasts = false;

    private static readonly Color PANEL_COLOR = new(0.08f, 0.08f, 0.1f, 0.94f);
    private static readonly Color BUTTON_COLOR = new(0.22f, 0.24f, 0.3f, 1f);
    private static readonly Color OVERLAY_COLOR = new(0f, 0f, 0f, 0.75f);
    private static readonly Color LIST_COLOR = new(1f, 1f, 1f, 0.04f);

    private InputRemapper _remapper;
    private InputAction _toggle;
    private readonly List<KeybindRow> _rows = new();
    private GameObject _root; // canvas (new) or Panel (child canvas) in Code, panelRoot in Scene
    private RectTransform _list; // where rows go
    private bool _isOpen;

    public bool IsOpen => _isOpen;

    private void Awake()
    {
        if (!string.IsNullOrEmpty(toggleKeyPath))
            _toggle = new InputAction("ToggleKeybindMenu", InputActionType.Button, toggleKeyPath);
    }

    private void OnEnable()
    {
        _toggle?.Enable();
    }

    private void OnDisable()
    {
        _toggle?.Disable();
        Close(); // also frees the UI mode so the game isn't stuck
    }

    private void Start()
    {
        _remapper = remapper;
        if (_remapper == null)
        {
            var manager = InputManager.Instance;
            _remapper = manager != null ? manager.GetComponent<InputRemapper>() : null;
        }
        if (_remapper == null || !_remapper.IsReady)
        {
            Debug.LogWarning(
                "KeybindMenu: InputRemapper missing or not ready. Add it to the InputManager object and turn on 'Use Asset'.",
                this
            );
            enabled = false;
            return;
        }

        bool isBuilt = buildMode == BuildMode.Code ? BuildCodeUi() : BindSceneUi();
        if (!isBuilt)
        {
            enabled = false;
            return;
        }

        _remapper.Changed += RefreshRows;
        _root.SetActive(false);
    }

    private void OnDestroy()
    {
        if (_remapper != null)
            _remapper.Changed -= RefreshRows;
        _toggle?.Dispose();
    }

    private void Update()
    {
        var manager = InputManager.Instance;
        // The key that just ended a rebind (Esc cancels) must not also close the menu.
        if (manager == null || _remapper.IsRebinding || _remapper.WasRebindFrame)
            return;

        if (_toggle != null && _toggle.WasPressedThisFrame())
        {
            if (_isOpen)
                Close();
            else if (!manager.UIMode)
                Open();
        }
        else if (_isOpen && manager.UICancelPressed)
            Close();
    }

    public void Open()
    {
        var manager = InputManager.Instance;
        if (_isOpen || _root == null || manager == null)
            return;

        _isOpen = true;
        _root.SetActive(true);
        manager.EnterUIMode(this);
        SetStatus(string.Empty);
        RefreshRows();
    }

    public void Close()
    {
        if (!_isOpen)
            return;

        _isOpen = false;
        if (_remapper != null)
            _remapper.CancelRebind();
        SetListening(false);
        if (_root != null)
            _root.SetActive(false);

        var manager = InputManager.Instance;
        if (manager != null)
            manager.ExitUIMode(this);
    }

    private void ResetAll()
    {
        _remapper.ResetAll();
        SetStatus("Reset to defaults.");
    }

    // Rebuilds every row from the remapper. Runs on open and on any binding change.
    private void RefreshRows()
    {
        if (!_isOpen || _list == null)
            return;

        foreach (var old in _rows)
        {
            if (old == null)
                continue;
            old.gameObject.SetActive(false); // so the layout ignores it until Destroy runs
            Destroy(old.gameObject);
        }
        _rows.Clear();

        foreach (var slot in _remapper.Slots)
        {
            var row = CreateRow();
            row.Setup(
                slot.Label,
                _remapper.GetDisplayString(slot),
                slot.IsLocked,
                () => BeginRebind(slot, row),
                () => _remapper.ResetSlot(slot)
            );
            _rows.Add(row);
        }
    }

    private void BeginRebind(KeybindSlot slot, KeybindRow row)
    {
        if (!_remapper.StartRebind(slot, OnRebindDone))
            return;

        foreach (var r in _rows)
            r.SetInteractable(false);
        row.ShowListening();
        SetListening(true, slot.Label);
        SetStatus(string.Empty);
    }

    private void OnRebindDone(InputRemapper.RebindResult result, string otherLabel)
    {
        SetListening(false);
        if (!_isOpen)
            return;

        SetStatus(
            result switch
            {
                InputRemapper.RebindResult.Swapped => $"Swapped with {otherLabel}.",
                InputRemapper.RebindResult.Rejected =>
                    $"{otherLabel} is locked, so that key can't be used.",
                _ => string.Empty,
            }
        );
        RefreshRows();
    }

    private void SetListening(bool isListening, string label = null)
    {
        if (listeningOverlay != null)
            listeningOverlay.SetActive(isListening);
        if (listeningText != null && isListening)
            listeningText.text = $"Press a key or mouse button for\n{label}\n\n(Esc to cancel)";
    }

    private void SetStatus(string message)
    {
        if (statusText != null)
            statusText.text = message;
    }

    private KeybindRow CreateRow()
    {
        if (buildMode == BuildMode.Scene)
            return Instantiate(rowPrefab, _list);

        var rt = NewUi(
            "Row",
            _list,
            typeof(HorizontalLayoutGroup),
            typeof(LayoutElement),
            typeof(KeybindRow)
        );
        rt.GetComponent<LayoutElement>().preferredHeight = 44f;
        var layout = rt.GetComponent<HorizontalLayoutGroup>();
        layout.padding = new RectOffset(
            rowPadding.left,
            rowPadding.right,
            rowPadding.top,
            rowPadding.bottom
        );
        layout.spacing = rowSpacing;
        layout.childControlWidth = layout.childControlHeight = true;
        layout.childForceExpandWidth = false;
        layout.childForceExpandHeight = true;

        var label = AddText(rt, string.Empty, 22f, TextAlignmentOptions.MidlineLeft, 0f);
        label.GetComponent<LayoutElement>().flexibleWidth = 1f;
        var bind = AddButton(rt, string.Empty, 190f);
        var reset = AddButton(rt, "Reset", 90f);

        var row = rt.GetComponent<KeybindRow>();
        row.Assign(label, bind, bind.GetComponentInChildren<TMP_Text>(), reset);
        return row;
    }

    private bool BindSceneUi()
    {
        if (panelRoot == null || rowContainer == null || rowPrefab == null)
        {
            Debug.LogWarning(
                "KeybindMenu: Scene mode needs Panel Root, Row Container and Row Prefab.",
                this
            );
            return false;
        }

        _root = panelRoot;
        _list = rowContainer;
        if (resetAllButton != null)
            resetAllButton.onClick.AddListener(ResetAll);
        if (closeButton != null)
            closeButton.onClick.AddListener(Close);
        if (listeningOverlay != null)
            listeningOverlay.SetActive(false);
        if (disableSceneRaycasts)
            DisableNonInteractiveRaycasts(panelRoot.transform);
        return true;
    }

    // Uses a Canvas found under this object, or makes one. A found canvas keeps its own settings;
    // missing scaler / raycaster are added with the code defaults.
    private Canvas FindOrCreateCanvas(out bool isCreated)
    {
        var canvas = GetComponentInChildren<Canvas>(true);
        isCreated = canvas == null;
        if (isCreated)
        {
            var created = new GameObject("KeybindMenu Canvas", typeof(Canvas));
            created.transform.SetParent(transform, false);
            canvas = created.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 100;
        }
        else if (!canvas.gameObject.activeSelf)
            canvas.gameObject.SetActive(true); // Panel is what Open / Close toggles

        var canvasGo = canvas.gameObject;
        if (canvasGo.GetComponent<CanvasScaler>() == null)
        {
            var scaler = canvasGo.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 0.5f;
        }
        if (canvasGo.GetComponent<GraphicRaycaster>() == null)
            canvasGo.AddComponent<GraphicRaycaster>();
        return canvas;
    }

    // Code mode: canvas > panel (title, scroll list, status, buttons) + listening overlay on top.
    private bool BuildCodeUi()
    {
        EnsureEventSystem();

        var canvas = FindOrCreateCanvas(out bool isCanvasCreated);
        var canvasGo = canvas.gameObject;

        var panel = NewUi("Panel", canvasGo.transform, typeof(Image), typeof(VerticalLayoutGroup));
        panel.anchorMin = panel.anchorMax = panel.pivot = new Vector2(0.5f, 0.5f);
        panel.sizeDelta = panelSize;
        var panelImage = panel.GetComponent<Image>();
        panelImage.color = PANEL_COLOR;
        panelImage.raycastTarget = false; // background only
        var panelLayout = panel.GetComponent<VerticalLayoutGroup>();
        panelLayout.padding = new RectOffset(24, 24, 20, 20);
        panelLayout.spacing = 10f;
        panelLayout.childControlWidth = panelLayout.childControlHeight = true;
        panelLayout.childForceExpandWidth = true;
        panelLayout.childForceExpandHeight = false;

        AddText(panel, title, 34f, TextAlignmentOptions.Center, 48f);

        var scroll = NewUi("Scroll", panel, typeof(ScrollRect), typeof(LayoutElement));
        scroll.GetComponent<LayoutElement>().flexibleHeight = 1f;
        var viewport = NewUi("Viewport", scroll, typeof(Image), typeof(RectMask2D));
        Stretch(viewport);
        viewport.GetComponent<Image>().color = LIST_COLOR; // gives the wheel something to hit
        _list = NewUi("Content", viewport, typeof(VerticalLayoutGroup), typeof(ContentSizeFitter));
        _list.anchorMin = new Vector2(0f, 1f);
        _list.anchorMax = Vector2.one;
        _list.pivot = new Vector2(0.5f, 1f);
        _list.anchoredPosition = Vector2.zero;
        _list.sizeDelta = Vector2.zero;
        var listLayout = _list.GetComponent<VerticalLayoutGroup>();
        listLayout.spacing = 6f;
        listLayout.childControlWidth = listLayout.childControlHeight = true;
        listLayout.childForceExpandWidth = true;
        listLayout.childForceExpandHeight = false;
        _list.GetComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter
            .FitMode
            .PreferredSize;
        var scrollRect = scroll.GetComponent<ScrollRect>();
        scrollRect.viewport = viewport;
        scrollRect.content = _list;
        scrollRect.horizontal = false;
        scrollRect.movementType = ScrollRect.MovementType.Clamped;
        scrollRect.scrollSensitivity = 30f;

        statusText = AddText(panel, string.Empty, 20f, TextAlignmentOptions.Center, 28f);

        var bar = NewUi("Buttons", panel, typeof(HorizontalLayoutGroup), typeof(LayoutElement));
        bar.GetComponent<LayoutElement>().preferredHeight = 46f;
        var barLayout = bar.GetComponent<HorizontalLayoutGroup>();
        barLayout.spacing = 12f;
        barLayout.childControlWidth = barLayout.childControlHeight = true;
        barLayout.childForceExpandWidth = barLayout.childForceExpandHeight = true;
        AddButton(bar, "Reset All").onClick.AddListener(ResetAll);
        AddButton(bar, "Close").onClick.AddListener(Close);

        var overlay = NewUi("Listening Overlay", canvasGo.transform, typeof(Image));
        Stretch(overlay);
        overlay.GetComponent<Image>().color = OVERLAY_COLOR; // blocks clicks while listening
        listeningText = AddText(overlay, string.Empty, 30f, TextAlignmentOptions.Center, 0f);
        Stretch((RectTransform)listeningText.transform);
        listeningOverlay = overlay.gameObject;
        listeningOverlay.SetActive(false);

        // A new canvas is ours to hide; a found canvas may hold other UI, so only hide the Panel.
        _root = isCanvasCreated ? canvasGo : panel.gameObject;
        return true;
    }

    // Turns off Raycast Target on graphics that only draw (panels, backgrounds, icons).
    private void DisableNonInteractiveRaycasts(Transform root)
    {
        foreach (var graphic in root.GetComponentsInChildren<Graphic>(true))
        {
            if (graphic.raycastTarget && !IsRaycastNeeded(graphic))
                graphic.raycastTarget = false;
        }
    }

    private bool IsRaycastNeeded(Graphic graphic)
    {
        if (listeningOverlay != null && graphic.gameObject == listeningOverlay)
            return true;
        if (graphic.GetComponent<Selectable>() != null)
            return true;

        var selectable = graphic.GetComponentInParent<Selectable>(true);
        if (selectable != null && selectable.targetGraphic == graphic)
            return true;

        var scroll = graphic.GetComponentInParent<ScrollRect>(true);
        return scroll != null
            && (
                scroll.viewport == graphic.rectTransform
                || scroll.GetComponent<Graphic>() == graphic
            );
    }

    // Menu clicks need an EventSystem with the Input System UI module.
    private static void EnsureEventSystem()
    {
        if (FindFirstObjectByType<EventSystem>() != null)
            return;
        var go = new GameObject("EventSystem", typeof(EventSystem));
        go.AddComponent<InputSystemUIInputModule>().AssignDefaultActions();
    }

    private static RectTransform NewUi(string name, Transform parent, params Type[] components)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        foreach (var type in components)
            go.AddComponent(type);
        return (RectTransform)go.transform;
    }

    private static void Stretch(RectTransform rt)
    {
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = rt.offsetMax = Vector2.zero;
    }

    private TMP_Text AddText(
        Transform parent,
        string text,
        float size,
        TextAlignmentOptions align,
        float height
    )
    {
        var rt = NewUi("Text", parent, typeof(TextMeshProUGUI), typeof(LayoutElement));
        var tmp = rt.GetComponent<TextMeshProUGUI>();
        if (font != null)
            tmp.font = font;
        tmp.text = text;
        tmp.fontSize = size;
        tmp.alignment = align;
        tmp.color = Color.white;
        tmp.raycastTarget = false;
        if (height > 0f)
            rt.GetComponent<LayoutElement>().preferredHeight = height;
        return tmp;
    }

    private Button AddButton(Transform parent, string label, float width = 0f)
    {
        var rt = NewUi(
            label + " Button",
            parent,
            typeof(Image),
            typeof(Button),
            typeof(LayoutElement)
        );
        var image = rt.GetComponent<Image>();
        image.color = BUTTON_COLOR;
        var button = rt.GetComponent<Button>();
        button.targetGraphic = image;
        if (width > 0f)
            rt.GetComponent<LayoutElement>().preferredWidth = width;

        var text = AddText(rt, label, 20f, TextAlignmentOptions.Center, 0f);
        Stretch((RectTransform)text.transform);
        return button;
    }
}
