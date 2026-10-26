using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Static Class Input Helper
/// - This is where Input Mapping Encapsulation live such as <see cref="GrabPressed">
/// </summary>
public static class IInput
{
    /// <summary>Hotbar keys 1..9.</summary>
    public const int HOTBAR_KEYS = 9;

    private const string HOTBAR_PREFIX = "Hotbar";

    private static InputManager _owner; // the manager the actions below came from
    private static InputAction _grab;

    //Add more here
    //_rotate,
    //_store,
    //...

    // Shop Keeper Input Actions
    // _scan;
    // _boxStore,
    // _boxNext,
    // _boxPrev,
    // _boxCancel,
    // _boxRotate,
    // _priceAdjust;
    //...
    static readonly InputAction[] _hotbar = new InputAction[HOTBAR_KEYS];

    // Reset statics for "Enter Play Mode without Domain Reload".
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStatics() => _owner = null;

    // Registers early so prompt labels (InputManager.GetLabel) exist before the first read.
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    static void Bootstrap() => Resolve();

    // Gets the actions again when the InputManager was replaced (a configured one beats a bare one).
    static bool Resolve()
    {
        var im = InputManager.Instance;
        if (im == null)
            return false;
        if (im == _owner)
            return true;

        // Left click is shared by Grab, Scan and BoxStore: only one of them is active per held item.
        _grab = im.GetExtraAction("Grab", "<Mouse>/leftButton", "<Gamepad>/rightTrigger");
        // Add your custom Resolve Action Mappings here
        // ...

        // Shop Keeper Resolve Action Mappings
        // _scan = im.GetExtraAction("Scan", "<Mouse>/leftButton", "<Gamepad>/rightTrigger");
        // _boxStore = im.GetExtraAction("BoxStore", "<Mouse>/leftButton", "<Gamepad>/rightTrigger");
        // _boxNext = im.GetExtraAction("BoxCycleNext", "<Keyboard>/q", "<Gamepad>/rightShoulder");
        // _boxPrev = im.GetExtraAction("BoxCyclePrev", "<Keyboard>/z", "<Gamepad>/leftShoulder");
        // _boxCancel = im.GetExtraAction("BoxCancel", "<Keyboard>/x", "<Gamepad>/buttonNorth");
        // _boxRotate = im.GetExtraVector2Action("BoxRotate", "<Mouse>/scroll");
        // _priceAdjust = im.GetExtraVector2Action("PriceAdjust", "<Mouse>/scroll");
        // ...

        for (int i = 0; i < _hotbar.Length; i++)
            _hotbar[i] = im.GetExtraAction(HOTBAR_PREFIX + (i + 1), "<Keyboard>/" + (i + 1));

        _owner = im;
        return true;
    }

    static bool CanRead => Resolve() && _owner.CursorLocked && _owner.GameplayEnabled;

    // Every read checks CanRead first: it also re-gets the actions if the InputManager was replaced.

    /// <summary>Hotbar slot (0-based) pressed this frame, else -1.</summary>
    public static int HotbarPressedIndex
    {
        get
        {
            if (!CanRead)
                return -1;
            for (int i = 0; i < _hotbar.Length; i++)
                if (_hotbar[i].WasPressedThisFrame())
                    return i;
            return -1;
        }
    }

    // A click that only re-locks the cursor must not grab, scan or store.
    public static bool GrabPressed =>
        CanRead && _grab.WasPressedThisFrame() && !_owner.WasRelockFrame;

    public static bool GrabHeld => CanRead && _grab.IsPressed();

    // Shop Keeper Encapsulators Functions
    // Scanner shares left click with Grab (the grabber is idle while something is held).
    // public static bool ScanPressed =>
    //     CanRead && _scan.WasPressedThisFrame() && !_owner.WasRelockFrame;

    // // Store the aimed item in the held box: left click, same guard as Grab.
    // public static bool BoxStorePressed =>
    //     CanRead && _boxStore.WasPressedThisFrame() && !_owner.WasRelockFrame;

    // public static bool BoxCycleNextPressed => CanRead && _boxNext.WasPressedThisFrame();

    // public static bool BoxCyclePrevPressed => CanRead && _boxPrev.WasPressedThisFrame();

    // public static bool BoxCancelPressed => CanRead && _boxCancel.WasPressedThisFrame();

    // /// <summary>Scroll Y this frame (0 when unavailable).</summary>
    // public static float BoxRotate => CanRead ? _boxRotate.ReadValue<Vector2>().y : 0f;

    // /// <summary>Scanner price change this frame: +1 or -1 per scroll step, else 0.</summary>
    // public static int PriceAdjustDelta
    // {
    //     get
    //     {
    //         if (!CanRead)
    //             return 0;
    //         float y = _priceAdjust.ReadValue<Vector2>().y;
    //         return y > 0f ? 1 : (y < 0f ? -1 : 0);
    //     }
    // }
}
