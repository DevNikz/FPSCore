using System;
using System.Collections;
using UnityEngine;
using Random = UnityEngine.Random;

[InteractableAddon("Carriable (Addon)")]
[RequireComponent(typeof(Rigidbody), typeof(Interactable))]
[DisallowMultipleComponent]
public class Carriable : MonoBehaviour
{
    [Tooltip("Carry follow strength.")]
    [SerializeField]
    float followGain = 15f;

    [Tooltip("Max speed while carried (m/s).")]
    [SerializeField]
    float maxFollowSpeed = 20f;

    [Tooltip("Hold style. Auto = from size and mass.")]
    [SerializeField]
    HoldSize holdSize = HoldSize.Auto;

    [Tooltip("Turn-to-view strength (aligned hold).")]
    [SerializeField]
    float rotateGain = 12f;

    [Tooltip("Max turn speed while carried (rad/s).")]
    [SerializeField]
    float maxRotateSpeed = 30f;

    [Tooltip("Scales throw speed for this object.")]
    [SerializeField]
    float throwMultiplier = 1f;

    [Tooltip("Delay before player collision returns after release (s).")]
    [SerializeField]
    float collisionRestoreDelay = 0.3f;

    [Tooltip("Seconds after pickup before the break-distance drop applies.")]
    [SerializeField]
    float breakGrace = 1f;

    [Header("Hold Pose")]
    [Tooltip("On = use Hold Rotation instead of the grab-time rotation.")]
    [SerializeField]
    bool overrideHoldRotation;

    [Tooltip("Rotation relative to the view (deg). Zero = faces the view.")]
    [SerializeField]
    Vector3 holdRotation = Vector3.zero;

    [Tooltip("Use view yaw only (ignore pitch/roll) so the object stays level.")]
    [SerializeField]
    bool levelHold;

    [Tooltip("Shift the pivot so the visual center sits on the hold point.")]
    // [SerializeField]
    private readonly bool centerOnHold;

    [Tooltip("Extra offset from the hold point, in view space (m).")]
    [SerializeField]
    Vector3 holdOffset = Vector3.zero;

    [Header("Camera")]
    [Tooltip("On = push forward so no part of this object enters the camera near plane.")]
    [SerializeField]
    bool keepClearOfCamera;

    [Tooltip("Extra gap beyond the camera near plane (m).")]
    [SerializeField]
    float cameraClearance = 0.05f;

    [Header("View Lock")]
    [Tooltip(
        "On = while carried: physics off, parented to the camera so it is fixed in the view "
            + "(no sway, no lag; child canvases move with it). "
            + "Uses Hold Rotation, Hold Offset and Center On Hold. Ignores Level Hold."
    )]
    [SerializeField]
    bool lockToView;

    const float DropSpeedCap = 3f;

    Rigidbody _rb;
    Interactable _interactable;
    ICarriableInteractHandler[] _interactHandlers;
    CarriableViewLock _viewLock;
    CarriableCameraClearance _clearance;
    Collider[] _cols;
    CharacterController _ignoredCC;
    Coroutine _restore;
    bool _attached,
        _claimed,
        _savedGravity;
    RigidbodyInterpolation _savedInterp;
    float _savedAngDamp;
    Quaternion _relRot = Quaternion.identity; // rotation relative to view
    Vector3 _centerLocal; // visual center in local space
    float _carryTime;
    bool _arrived; // reached the hold point once
    internal int TickIndex = -1; // slot in the ticker, -1 = not ticking

    /// <summary>Raised when any Carriable is enabled. Addons (e.g. physics LOD) hook in here.</summary>
    public static event Action<Carriable> Enabled;

    /// <summary>Raised when any Carriable is disabled.</summary>
    public static event Action<Carriable> Disabled;

    /// <summary>Drives carried and claimed items. Null = a CarriableTicker is created on first use.</summary>
    public static ICarriableTicker Ticker { get; set; }

    /// <summary>
    /// Takes a Carriable back on Despawn (e.g. a pool). Return true if handled.
    /// Null or false = the item is destroyed.
    /// </summary>
    public static Func<Carriable, bool> DespawnHandler { get; set; }

    /// <summary>Raised after Throw releases the object (arg = launch velocity).</summary>
    public event System.Action<Carriable, Vector3> Thrown;

    public Carrier Holder { get; private set; }
    public bool IsHeld => _attached;

    /// <summary>Non-carry owner (e.g. Grabber dragging), null when not claimed.</summary>
    public ICarriableClaimant Claimant { get; private set; }
    public bool IsClaimed => _claimed;
    public Rigidbody Body => _rb;
    public HoldSize Size => holdSize;

    /// <summary>Largest collider dimension (m), measured on pickup/claim.</summary>
    public float Extent { get; private set; }

    public event Action Grabbed;
    public event Action Dropped;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStatics()
    {
        Enabled = null;
        Disabled = null;
        Ticker = null;
        DespawnHandler = null;
    }

    void Awake()
    {
        _rb = GetComponent<Rigidbody>();
        _interactable = GetComponent<Interactable>();
        _interactHandlers = GetComponents<ICarriableInteractHandler>();
        _viewLock = new CarriableViewLock(this, _rb);
        _clearance = new CarriableCameraClearance(transform);
    }

    void OnEnable()
    {
        if (_rb == null || _interactable == null)
        {
            Debug.LogError($"{name}: Rigidbody/Interactable missing.", this);
            enabled = false;
            return;
        }
        _interactable.OnInteractEvent += HandleInteract;
        _viewLock.RestorePendingParent();
        Enabled?.Invoke(this);
    }

    void OnDisable()
    {
        if (_interactable != null)
            _interactable.OnInteractEvent -= HandleInteract;
        Detach();
        ReleaseClaim();
        SetIgnore(false); // never leave collisions off
        RemoveFromTicker();
        Disabled?.Invoke(this);
    }

    protected virtual void HandleInteract(Interactor who)
    {
        for (int i = 0; i < _interactHandlers.Length; i++)
            if (_interactHandlers[i].TryHandleInteract(this, who))
                return;

        if (_attached || who == null)
            return;
        if (!who.TryGetComponent<Carrier>(out var carrier))
        {
            Debug.LogWarning($"{name}: Carrier missing on {who.name}.", this);
            return;
        }
        carrier.TryGrab(this);
    }

    /// <summary>Called by Carrier. False if it can't be carried.</summary>
    public bool Attach(Carrier who)
    {
        if (_attached || who == null || _rb == null || _rb.isKinematic)
            return false;

        ReleaseClaim(); // picking up ends a claim

        Holder = who;
        _attached = true;
        Grabbed?.Invoke();
        _relRot = overrideHoldRotation
            ? Quaternion.Euler(holdRotation)
            : Quaternion.Inverse(ViewBasis(who)) * _rb.rotation;
        _centerLocal = centerOnHold ? CarriableBounds.MeasureCenter(transform) : Vector3.zero;
        _clearance.Clear();
        if (keepClearOfCamera || lockToView)
        {
            _clearance.Measure(who);
            if (!_clearance.IsReady)
                Debug.LogWarning($"{name}: view camera or bounds missing (clearance off).", this);
            else if (lockToView && centerOnHold)
                _centerLocal = _clearance.LocalCenter; // exact, independent of grab rotation
        }
        _carryTime = 0f;
        _arrived = false;
        Engage(who.Body);
        _rb.useGravity = false;
        if (lockToView)
            BeginViewLock();
        UpdateTicking();
        return true;
    }

    /// <summary>Called by an ICarriableClaimant (e.g. Grabber). False if it can't be claimed.</summary>
    public bool TryClaim(ICarriableClaimant who)
    {
        if (_attached || _claimed || who == null || _rb == null || _rb.isKinematic)
            return false;

        Claimant = who;
        _claimed = true;
        Engage(who.Body); // the claimant sets gravity
        UpdateTicking();
        return true;
    }

    /// <summary>Stop being claimed. Safe to call anytime.</summary>
    public void ReleaseClaim()
    {
        if (!_claimed)
            return;
        _claimed = false;
        UpdateTicking();
        var c = Claimant;
        Claimant = null;

        Disengage();
        if (!IsDestroyed(c))
            c.Clear(this);
    }

    /// <summary>Release gently.</summary>
    public void Drop()
    {
        if (!_attached)
            return;
        Detach();
        Dropped?.Invoke();
        _rb.linearVelocity = Vector3.ClampMagnitude(_rb.linearVelocity, DropSpeedCap);
    }

    /// <summary>Release with a launch velocity (m/s).</summary>
    public void Throw(Vector3 velocity)
    {
        if (!_attached)
            return;
        Detach();
        Dropped?.Invoke();
        _rb.linearVelocity = velocity * throwMultiplier;
        _rb.angularVelocity = Random.insideUnitSphere * 2f;
        Thrown?.Invoke(this, _rb.linearVelocity);
    }

    /// <summary>Release and hide (e.g. a hotbar). Collisions restore in OnDisable.</summary>
    public void Stow()
    {
        Detach();
        if (_rb != null)
        {
            _rb.linearVelocity = Vector3.zero;
            _rb.angularVelocity = Vector3.zero;
        }
        gameObject.SetActive(false);
    }

    /// <summary>Remove this object: handed to DespawnHandler (e.g. a pool), else destroyed.</summary>
    public void Despawn()
    {
        if (DespawnHandler != null && DespawnHandler(this))
            return;
        gameObject.SetActive(false);
        Destroy(gameObject);
    }

    /// <summary>Pool hook, runs just before activation (on first use, before Awake).</summary>
    public virtual void OnPoolSpawn() { }

    /// <summary>Pool hook, runs just after deactivation. Drops listeners so the next use starts clean.</summary>
    public virtual void OnPoolRelease()
    {
        Grabbed = null;
        Dropped = null;
        Thrown = null;
    }

    internal void TickFixed()
    {
        if (_claimed && IsDestroyed(Claimant)) // claimant destroyed
            ReleaseClaim();
        if (!_attached)
            return;
        if (Holder == null) // holder destroyed
        {
            Detach();
            return;
        }
        if (_viewLock.IsLocked) // pose is written every frame by ApplyViewLock
            return;

        Vector3 point = Holder.GetHoldPoint(this);
        float dist = (point - _rb.position).magnitude;
        _carryTime += Time.fixedDeltaTime;
        if (dist <= Holder.BreakDistance)
            _arrived = true;
        // Objects grabbed from range fly in first; only then can they "break".
        if (dist > Holder.BreakDistance && (_arrived || _carryTime > breakGrace)) // stuck/lagging
        {
            Drop();
            return;
        }
        Vector3 want =
            point
            + ViewBasis(Holder) * holdOffset
            - _rb.rotation * Vector3.Scale(_centerLocal, transform.lossyScale);
        if (_clearance.IsReady)
        {
            Vector3 fwd = _clearance.Camera.transform.forward;
            want += fwd * _clearance.NearDeficit(want, _rb.rotation, cameraClearance); // aim clear of the camera
            float now = _clearance.NearDeficit(_rb.position, _rb.rotation, cameraClearance);
            if (now > 0f) // already inside: hard push, don't wait for the follow to catch up
                _rb.position += fwd * now;
        }
        _rb.linearVelocity = Vector3.ClampMagnitude(
            (want - _rb.position) * followGain,
            maxFollowSpeed
        );
        if (Holder.AlignToView || overrideHoldRotation)
            DriveRotation();
    }

    // Only the LOCAL pose is written (constant relative to the camera), so update order
    // doesn't matter: the hierarchy does the rest at render time.
    internal void TickLate()
    {
        if (_viewLock.IsLocked)
            ApplyViewLock();
    }

    // Keep the grab-time orientation relative to the view.
    void DriveRotation()
    {
        Quaternion target = ViewBasis(Holder) * _relRot;
        (target * Quaternion.Inverse(_rb.rotation)).ToAngleAxis(out float ang, out Vector3 axis);
        if (ang > 180f)
            ang -= 360f;
        if (
            float.IsNaN(axis.x)
            || float.IsInfinity(axis.x)
            || axis.sqrMagnitude < 1e-6f
            || Mathf.Abs(ang) < 0.05f
        )
        {
            _rb.angularVelocity = Vector3.zero;
            return;
        }
        Vector3 w = axis.normalized * (ang * Mathf.Deg2Rad * rotateGain);
        _rb.angularVelocity = Vector3.ClampMagnitude(w, maxRotateSpeed);
    }

    // View rotation, optionally yaw-only.
    Quaternion ViewBasis(Carrier h)
    {
        Quaternion v = h.ViewRotation;
        return levelHold ? Quaternion.Euler(0f, v.eulerAngles.y, 0f) : v;
    }

    void BeginViewLock()
    {
        Transform view =
            _clearance.Camera != null
                ? _clearance.Camera.transform
                : (Holder != null ? Holder.Origin : null);
        _viewLock.Begin(view);
    }

    void ApplyViewLock()
    {
        var h = Holder;
        if (h == null || h.Origin == null || _viewLock.View == null)
            return;

        // Hold point as an offset from the origin (ignores walls).
        Vector3 hold = h.Origin.InverseTransformPoint(h.GetViewHoldPoint(this));
        Quaternion rot = Quaternion.Euler(holdRotation);
        Vector3 pos = hold + holdOffset - rot * Vector3.Scale(_centerLocal, transform.lossyScale);
        if (_clearance.IsReady)
            pos.z += _clearance.NearDeficitLocal(pos, rot, cameraClearance);
        transform.localPosition = pos;
        transform.localRotation = rot;
    }

    // Carried or claimed items are driven by the ticker; idle ones have no callbacks.
    void UpdateTicking()
    {
        if (_attached || _claimed)
            (Ticker ??= CarriableTicker.Create()).Add(this);
        else
            RemoveFromTicker();
    }

    // Never creates a ticker (it clears Ticker when destroyed, e.g. while quitting).
    void RemoveFromTicker()
    {
        if (Ticker != null)
            Ticker.Remove(this);
        else
            TickIndex = -1;
    }

    void Detach()
    {
        if (!_attached)
            return;
        _attached = false;
        UpdateTicking();
        var h = Holder;
        Holder = null;

        Disengage();
        if (h != null)
            h.Clear(this);
    }

    // Shared setup for carry and claim.
    void Engage(CharacterController cc)
    {
        if (_restore != null) // cancel pending collision restore
        {
            StopCoroutine(_restore);
            _restore = null;
        }

        _cols = GetComponentsInChildren<Collider>();
        _ignoredCC = cc;
        Extent = CarriableBounds.MeasureExtent(_cols);

        _savedGravity = _rb.useGravity;
        _savedInterp = _rb.interpolation;
        _savedAngDamp = _rb.angularDamping;
        _rb.interpolation = RigidbodyInterpolation.Interpolate;
        _rb.angularDamping = 10f;
        _rb.WakeUp();

        SetIgnore(true);
    }

    void Disengage()
    {
        _viewLock.End();
        _rb.useGravity = _savedGravity;
        _rb.interpolation = _savedInterp;
        _rb.angularDamping = _savedAngDamp;

        if (isActiveAndEnabled)
            _restore = StartCoroutine(RestoreCollision());
        else
            SetIgnore(false);
    }

    IEnumerator RestoreCollision()
    {
        yield return new WaitForSeconds(collisionRestoreDelay);
        SetIgnore(false);
        _restore = null;
    }

    internal void SetIgnore(bool on)
    {
        if (_ignoredCC == null || _cols == null)
            return;
        foreach (var c in _cols)
            if (c != null)
                Physics.IgnoreCollision(c, _ignoredCC, on);
    }

    internal Collider[] GetOwnColliders()
    {
        var own = new System.Collections.Generic.List<Collider>();
        foreach (var c in GetComponentsInChildren<Collider>())
            if (c.GetComponentInParent<Carriable>() == this)
                own.Add(c);
        return own.ToArray();
    }

    // Null, or a destroyed Unity object behind the interface (interface null checks miss it).
    static bool IsDestroyed(ICarriableClaimant claimant) =>
        claimant == null || (claimant is UnityEngine.Object o && o == null);

    protected virtual void OnValidate()
    {
        followGain = Mathf.Max(0f, followGain);
        maxFollowSpeed = Mathf.Max(0f, maxFollowSpeed);
        throwMultiplier = Mathf.Max(0f, throwMultiplier);
        rotateGain = Mathf.Max(0f, rotateGain);
        maxRotateSpeed = Mathf.Max(0f, maxRotateSpeed);
        collisionRestoreDelay = Mathf.Max(0f, collisionRestoreDelay);
        breakGrace = Mathf.Max(0f, breakGrace);
        cameraClearance = Mathf.Max(0f, cameraClearance);
    }
}
