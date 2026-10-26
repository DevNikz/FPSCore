using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Reads the player's input: move, look, jump, crouch, interact, sprint, zoom and cursor lock.
/// </summary>
[DefaultExecutionOrder(-100)]
public class InputManager : MonoBehaviour
{
    private static InputManager _instance;
    private static bool _quitting;

    //Creates only 1 instance of Input Manager and returns it the referenced instanced Input Manager.
    public static InputManager Instance
    {
        get
        {
            if (_instance == null && !_quitting)
                Create();
            return _instance;
        }
    }

    [Header("Input Source")]
    [Tooltip("Off = built-in code bindings. On = use the asset below.")]
    [SerializeField]
    private bool useAsset;

    [SerializeField]
    private InputActionAsset inputAsset;

    [Tooltip("Action map to read from the asset.")]
    [SerializeField]
    private string actionMapName = "Player";

    [Header("Cursor")]
    [SerializeField]
    private bool lockCursorOnStart = true;

    /// <summary>
    /// - Input Action Names (must match the referenced <see cref="inputAsset">)
    /// - Add more action names that suits your project key inputs
    /// </summary>
    private const string MOVE_NAME = "Move",
        LOOK_NAME = "Look",
        JUMP_NAME = "Jump",
        CROUCH_NAME = "Crouch",
        CROUCH_TOGGLE_NAME = "CrouchToggle",
        INTERACT_NAME = "Interact",
        SPRINT_NAME = "Sprint",
        ZOOM_NAME = "Zoom",
        UNLOCK_NAME = "UnlockCursor";

    /// <summary>
    /// - Input Action variables (must match the referenced Keynames above>)
    /// - Add more action map variables that suits your project key inputs
    /// </summary>
    private InputAction _move,
        _look,
        _jump,
        _crouch,
        _crouchToggle,
        _interact,
        _sprint,
        _zoom,
        _unlock;

    //_grab,
    //_slide,
    //...

    private float _lookScale = 1f;
    private int _relockFrame = -1; // frame the cursor was re-locked by a click
    private InputActionAsset _runtimeAsset; // clone of inputAsset
    private InputActionMap _map; // map read from _runtimeAsset (null = code bindings only)
    private readonly List<InputAction> _owned = new(); // code-built actions
    private bool _built;

    // Extra actions a game asked for. The specs are static so a replacement manager rebuilds them.
    private readonly struct ExtraSpec
    {
        public readonly bool IsVector2;
        public readonly string[] Paths;

        public ExtraSpec(bool isVector2, string[] paths)
        {
            IsVector2 = isVector2;
            Paths = paths;
        }
    }

    private static readonly Dictionary<string, ExtraSpec> _extraSpecs = new();
    private readonly Dictionary<string, InputAction> _extra = new();

    private bool _gameplayOn = true; // SetGameplayEnabled (menus, cutscenes)
    private readonly HashSet<Object> _uiOwners = new(); // open UIs
    private readonly List<Object> _deadOwners = new();

    public bool UsingAsset => _runtimeAsset != null;

    /// <summary>Action map the game reads (a clone of the asset). Null while on code bindings only.</summary>
    public InputActionMap Map => _map;

    /// <summary>Project input asset assigned in the inspector (the original, never rebound).</summary>
    public InputActionAsset InputAsset => inputAsset;

    /// <summary>Name of the action map read from the asset.</summary>
    public string ActionMapName => actionMapName;

    /// <summary>False while a UI is open or gameplay was switched off.</summary>
    public bool GameplayEnabled => _gameplayOn && !UIMode;

    /// <summary>True while any UI that needs the cursor is open.</summary>
    public bool UIMode => _uiOwners.Count > 0;
    public bool CursorLocked { get; private set; }

    /// <summary>
    /// True on the frame a click re-locked the cursor (or closed a UI). Click actions added with
    /// GetExtraAction should ignore that frame so the click does not also grab, scan or store.
    /// </summary>
    public bool WasRelockFrame => Time.frameCount == _relockFrame;

    // Gameplay reads. Neutral values when disabled/unlocked.
    public Vector2 Move => Active(_move) ? _move.ReadValue<Vector2>() : Vector2.zero;
    public Vector2 Look =>
        CursorLocked && Active(_look) ? _look.ReadValue<Vector2>() * _lookScale : Vector2.zero;

    /// <summary>Look sensitivity multiplier 0..1 (a grabber can lower it for heavy objects).</summary>
    public float LookScale
    {
        get => _lookScale;
        set => _lookScale = Mathf.Clamp01(value);
    }
    public bool JumpPressed => Active(_jump) && _jump.WasPressedThisFrame();
    public bool CrouchHeld => Active(_crouch) && _crouch.IsPressed();
    public bool CrouchTogglePressed => Active(_crouchToggle) && _crouchToggle.WasPressedThisFrame();
    public bool InteractPressed =>
        CursorLocked && Active(_interact) && _interact.WasPressedThisFrame();
    public bool InteractHeld => CursorLocked && Active(_interact) && _interact.IsPressed();
    public bool InteractReleased =>
        CursorLocked && Active(_interact) && _interact.WasReleasedThisFrame();

    /// <summary>Esc this frame while a UI is open. The UI should close itself and call ExitUIMode.</summary>
    public bool UICancelPressed => UIMode && _unlock != null && _unlock.WasPressedThisFrame();

    public bool SprintHeld => Active(_sprint) && _sprint.IsPressed();
    public bool ZoomHeld => CursorLocked && Active(_zoom) && _zoom.IsPressed();

    private bool Active(InputAction a) => GameplayEnabled && a != null;

    /// <summary>
    /// A button action the game adds (grab, hotbar...). Uses the asset action with this name if there
    /// is one, else a code action on defaultPaths. Enabled, and the same action on every call.
    /// Null if the name is empty. Read it with the same guards as the built-in reads
    /// (GameplayEnabled, CursorLocked, WasRelockFrame for clicks).
    /// </summary>
    public InputAction GetExtraAction(string actionName, params string[] defaultPaths) =>
        GetOrBuildExtra(actionName, false, defaultPaths);

    /// <summary>Same as GetExtraAction, but a Vector2 value action (scroll, sticks).</summary>
    public InputAction GetExtraVector2Action(string actionName, params string[] defaultPaths) =>
        GetOrBuildExtra(actionName, true, defaultPaths);

    private InputAction GetOrBuildExtra(string actionName, bool isVector2, string[] paths)
    {
        if (string.IsNullOrEmpty(actionName))
        {
            Debug.LogWarning("InputManager: an extra action needs a name.", this);
            return null;
        }
        if (_extra.TryGetValue(actionName, out var existing))
            return existing;
        _extraSpecs.TryAdd(actionName, new ExtraSpec(isVector2, paths));
        return _built ? BuildExtra(actionName, _extraSpecs[actionName]) : null;
    }

    private InputAction BuildExtra(string actionName, ExtraSpec spec)
    {
        var a = Pick(
            _map,
            actionName,
            () =>
                Own(
                    spec.IsVector2
                        ? Vector2Action(actionName, spec.Paths)
                        : Button(actionName, spec.Paths)
                )
        );
        a.Enable();
        _extra[actionName] = a;

        return a;
    }

    /// <summary>
    /// Display text of an action's binding for prompts ("E", "Left Button"...), reflecting rebinds.
    /// Interact is built in; any other value uses the extra action with the same name as the
    /// enum value (Grab, Scan...). Allocates: call on prompt change only, not per frame.
    /// </summary>
    public string GetLabel(PromptAction action) =>
        action == PromptAction.Interact ? BindingLabel(_interact) : GetLabel(action.ToString());

    /// <summary>Binding text of an extra action by name. Null if it was never added.</summary>
    public string GetLabel(string actionName) =>
        _extra.TryGetValue(actionName, out var a) ? BindingLabel(a) : null;

    private static string BindingLabel(InputAction a)
    {
        if (a == null)
            return null;
        var bindings = a.bindings;
        int pick = -1;
        for (int i = 0; i < bindings.Count; i++)
        {
            var b = bindings[i];
            string path = b.effectivePath;
            if (b.isComposite || b.isPartOfComposite || string.IsNullOrEmpty(path))
                continue;
            if (path.StartsWith("<Keyboard>") || path.StartsWith("<Mouse>"))
            {
                pick = i;
                break;
            }
            if (pick < 0)
                pick = i;
        }
        return pick >= 0 ? a.GetBindingDisplayString(pick) : null;
    }

    // Reset statics for "Enter Play Mode without Domain Reload".
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics()
    {
        _instance = null;
        _quitting = false;
        _extraSpecs.Clear();
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void Bootstrap()
    {
        _ = Instance;
    }

    // Uses Resources/InputManager prefab if present (set your asset there), else a bare object.
    private static void Create()
    {
        var prefab = Resources.Load<InputManager>("InputManager");
        if (prefab != null)
            Instantiate(prefab);
        else
            new GameObject("[InputManager]").AddComponent<InputManager>();
    }

    private void Awake()
    {
        if (_instance != null && _instance != this)
        {
            // Configured asset-based manager replaces a bare auto-created one.
            if (useAsset && inputAsset != null && !_instance.UsingAsset)
            {
                _instance.enabled = false;
                Destroy(_instance.gameObject);
            }
            else
            {
                Destroy(gameObject);
                return;
            }
        }
        _instance = this;
        transform.SetParent(null); // DontDestroyOnLoad needs a root object
        DontDestroyOnLoad(gameObject);

        BuildActions();
        SetCursorLocked(lockCursorOnStart);
    }

    /// <summary>
    /// - Build Actions if Input Action Mappings doesn't exist in <see cref="inputAsset">.
    /// - For Example, if <see cref="_interact"> is missing in Input Actions Asset,
    /// - this function will automatically build a default input action mapping.
    /// </summary>
    private void BuildActions()
    {
        InputActionMap map = null;
        if (useAsset && inputAsset != null)
        {
            _runtimeAsset = Instantiate(inputAsset); // never touch the project asset
            map = _runtimeAsset.FindActionMap(actionMapName, false);
            if (map == null)
                Debug.LogWarning(
                    $"InputManager: map '{actionMapName}' not found. Using defaults.",
                    this
                );
        }
        _map = map;

        _move = Pick(map, MOVE_NAME, () => MakeMove());
        _look = Pick(
            map,
            LOOK_NAME,
            () =>
                Own(
                    new InputAction(
                        LOOK_NAME,
                        InputActionType.Value,
                        "<Mouse>/delta",
                        expectedControlType: "Vector2"
                    )
                )
        ); // raw px/frame: no deltaTime
        _jump = Pick(
            map,
            JUMP_NAME,
            () => Own(Button(JUMP_NAME, "<Keyboard>/space", "<Gamepad>/buttonSouth"))
        );
        _crouch = Pick(
            map,
            CROUCH_NAME,
            () => Own(Button(CROUCH_NAME, "<Keyboard>/leftCtrl", "<Gamepad>/buttonEast"))
        );
        _crouchToggle = Pick(
            map,
            CROUCH_TOGGLE_NAME,
            () => Own(Button(CROUCH_TOGGLE_NAME, "<Keyboard>/c"))
        );
        _interact = Pick(
            map,
            INTERACT_NAME,
            () => Own(Button(INTERACT_NAME, "<Keyboard>/e", "<Gamepad>/buttonWest"))
        );
        _sprint = Pick(
            map,
            SPRINT_NAME,
            () => Own(Button(SPRINT_NAME, "<Keyboard>/leftShift", "<Gamepad>/leftStickPress"))
        );
        _zoom = Pick(
            map,
            ZOOM_NAME,
            () => Own(Button(ZOOM_NAME, "<Mouse>/rightButton", "<Gamepad>/leftTrigger"))
        );
        _unlock = Pick(map, UNLOCK_NAME, () => Own(Button(UNLOCK_NAME, "<Keyboard>/escape")));
        //Add more action mappings here if you add additional input actions in your input asset.
        //...

        // Own actions are enabled here; asset actions are enabled individually (same effect).
        _move.Enable();
        _look.Enable();
        _jump.Enable();
        _crouch.Enable();
        _crouchToggle.Enable();
        _interact.Enable();
        _sprint.Enable();
        _zoom.Enable();
        _unlock.Enable();
        //Enable your input mapping here.
        //...

        // Actions a game asked for before this manager existed (or before it was replaced).
        foreach (var spec in _extraSpecs)
            BuildExtra(spec.Key, spec.Value);
        _built = true;
    }

    // Asset action if present, else code fallback.
    private InputAction Pick(
        InputActionMap map,
        string actionName,
        System.Func<InputAction> fallback
    )
    {
        var a = map?.FindAction(actionName, false);
        if (a == null && map != null)
            Debug.LogWarning(
                $"InputManager: action '{actionName}' missing in asset. Using default.",
                this
            );
        return a ?? fallback();
    }

    private InputAction Own(InputAction a)
    {
        _owned.Add(a);
        return a;
    }

    private static InputAction Button(string n, params string[] paths)
    {
        var a = new InputAction(n, InputActionType.Button);
        foreach (var p in paths)
            a.AddBinding(p);
        return a;
    }

    private static InputAction Vector2Action(string n, params string[] paths)
    {
        var a = new InputAction(n, InputActionType.Value, expectedControlType: "Vector2");
        foreach (var p in paths)
            a.AddBinding(p);
        return a;
    }

    private InputAction MakeMove()
    {
        var a = Own(
            new InputAction(MOVE_NAME, InputActionType.Value, expectedControlType: "Vector2")
        );
        a.AddCompositeBinding("2DVector")
            .With("Up", "<Keyboard>/w")
            .With("Down", "<Keyboard>/s")
            .With("Left", "<Keyboard>/a")
            .With("Right", "<Keyboard>/d");
        a.AddBinding("<Gamepad>/leftStick");

        //Add custom bindings here through code.
        return a;
    }

    private void Update()
    {
        if (!_built)
            return;
        PurgeUIOwners();
        if (CursorLocked && _unlock.WasPressedThisFrame())
            SetCursorLocked(false);
        else if (
            !CursorLocked
            && GameplayEnabled
            && Mouse.current != null
            && Mouse.current.leftButton.wasPressedThisFrame
        )
        {
            _relockFrame = Time.frameCount;
            SetCursorLocked(true); // click to re-lock
        }
    }

    public void SetCursorLocked(bool locked)
    {
        CursorLocked = locked;
        Cursor.lockState = locked ? CursorLockMode.Locked : CursorLockMode.None;
        Cursor.visible = !locked;
    }

    /// <summary>Call from menus/cutscenes to block gameplay input.</summary>
    public void SetGameplayEnabled(bool enabled)
    {
        _gameplayOn = enabled;
        SetCursorLocked(GameplayEnabled);
    }

    /// <summary>
    /// Open a UI that needs the cursor: frees it and blocks look, move, interact and the rest
    /// (extra actions too, when read with GameplayEnabled) until every owner has exited.
    /// Pair with ExitUIMode (do it in OnDisable too).
    /// </summary>
    public void EnterUIMode(Object owner)
    {
        if (owner == null)
        {
            Debug.LogWarning("InputManager: EnterUIMode needs an owner.", this);
            return;
        }
        if (_uiOwners.Add(owner))
            SetCursorLocked(GameplayEnabled);
    }

    /// <summary>Close a UI opened with EnterUIMode. Safe to call twice.</summary>
    public void ExitUIMode(Object owner)
    {
        if (owner == null || !_uiOwners.Remove(owner))
            return;
        _relockFrame = Time.frameCount; // the click that closed the UI must not also grab/scan/store
        SetCursorLocked(GameplayEnabled);
    }

    // A UI destroyed while open (scene change, etc.) must not leave the game stuck.
    private void PurgeUIOwners()
    {
        if (_uiOwners.Count == 0)
            return;
        _deadOwners.Clear();
        foreach (var o in _uiOwners)
            if (o == null)
                _deadOwners.Add(o);
        if (_deadOwners.Count == 0)
            return;
        foreach (var o in _deadOwners)
            _uiOwners.Remove(o);
        _relockFrame = Time.frameCount;
        SetCursorLocked(GameplayEnabled);
    }

    private void OnApplicationQuit()
    {
        _quitting = true;
    }

    private void OnDestroy()
    {
        if (_built)
        {
            foreach (var a in _owned)
                a.Dispose();
            _owned.Clear();
            _extra.Clear();
            if (_runtimeAsset != null)
            {
                _runtimeAsset.Disable();
                Destroy(_runtimeAsset);
            }
            _built = false;
        }
        if (_instance == this)
            _instance = null;
    }
}
