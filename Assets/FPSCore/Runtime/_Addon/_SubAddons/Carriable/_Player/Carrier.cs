using UnityEngine;

/// <summary>
/// Carry, throw and swing-throw for Carriables. Aims through the Interactor on the same object.
/// </summary>
[FPSAddon("Carrier")]
[RequireComponent(typeof(Interactor))]
[DefaultExecutionOrder(55)]
[DisallowMultipleComponent]
public class Carrier : MonoBehaviour, IHandsFull
{
    [Header("Carry")]
    [SerializeField]
    private float holdDistance = 1.8f;

    [SerializeField]
    private float minHoldDistance = 0.8f;

    [Tooltip("Gap kept from walls.")]
    [SerializeField]
    private float holdClearance = 0.3f;

    [Tooltip("Drop if the object lags this far behind the hold point.")]
    [SerializeField]
    private float breakDistance = 1.5f;

    [Tooltip("Heaviest pickup (kg).")]
    [SerializeField]
    private float maxMass = 20f;

    [Header("Hold Style")]
    [Tooltip("Medium objects: Side = like small, Center = close center, Classic = out front.")]
    [SerializeField]
    private MediumHold mediumHold = MediumHold.Center;

    [Tooltip("Carried object follows view rotation.")]
    [SerializeField]
    private bool alignToView = true;

    [Tooltip("Object size (m) at/above this = medium.")]
    [SerializeField]
    private float mediumExtent = 0.45f;

    [Tooltip("Object size (m) at/above this = large.")]
    [SerializeField]
    private float largeExtent = 0.9f;

    [Tooltip("Mass (kg) at/above this = medium.")]
    [SerializeField]
    private float mediumMass = 3f;

    [Tooltip("Mass (kg) at/above this = large (two hands).")]
    [SerializeField]
    private float largeMass = 10f;

    [Tooltip("Small: bottom-right, like Minecraft.")]
    [SerializeField]
    private HoldPose smallPose = new HoldPose(new Vector3(0.32f, -0.28f, 0.55f), 0.5f, 0f);

    [Tooltip("Medium (Center): close, in front.")]
    [SerializeField]
    private HoldPose centerPose = new HoldPose(new Vector3(0f, -0.1f, 0.45f), 1f, 0f);

    [Tooltip("Large: low and centered, two hands.")]
    [SerializeField]
    private HoldPose largePose = new HoldPose(new Vector3(0f, -0.5f, 0.5f), 1f, 0.5f);

    [Header("Throw (hold E)")]
    [Tooltip("Press shorter than this = drop.")]
    [SerializeField]
    private float tapTime = 0.2f;

    [Tooltip("Seconds past tapTime to reach full power.")]
    [SerializeField]
    private float chargeTime = 1f;

    [SerializeField]
    private float minThrowSpeed = 4f;

    [SerializeField]
    private float maxThrowSpeed = 15f;

    [Header("Throw (camera swing)")]
    [SerializeField]
    private bool swingThrowEnabled = false;

    [Tooltip("Swing throw for small objects (held at the side). Off = hold-E only.")]
    [SerializeField]
    private bool swingSmall;

    [SerializeField]
    private bool swingMedium = false;

    [SerializeField]
    private bool swingLarge = false;

    [Tooltip("View turn speed that starts a swing (deg/s).")]
    [SerializeField]
    private float swingThreshold = 450f;

    [Tooltip("Ignore swings this long after pickup (s).")]
    [SerializeField]
    private float swingGrace = 0.25f;

    [Tooltip("Throw speed = hold-point speed x this.")]
    [SerializeField]
    private float swingThrowScale = 1f;

    [SerializeField]
    private float maxSwingSpeed = 18f;

    [Tooltip("Swing peak must exceed this (m/s) to throw.")]
    [SerializeField]
    private float minSwingSpeed = 3f;

    [Tooltip("Throw when speed falls below peak x this.")]
    [SerializeField, Range(0.3f, 0.99f)]
    private float swingReleaseRatio = 0.85f;

    [Tooltip("Force a throw after this long in a swing (s).")]
    [SerializeField]
    private float swingMaxTime = 0.3f;

    [Tooltip("Player velocity added to the throw (0..1).")]
    [SerializeField, Range(0f, 1f)]
    float inheritVelocity = 1f;

    [Tooltip("Smoothing of swing speed (s).")]
    [SerializeField]
    private float swingSmoothing = 0.04f;

    [Header("Debug")]
    [SerializeField]
    private bool drawGizmos = true;

    private Interactor _interactor;
    private ICarrierStorage _storage;
    private Carriable _held;
    private bool _charging;
    private float _holdTimer,
        _carryTime;

    // Active hold (null pose = classic).
    private HoldPose _pose;
    private float _radius;
    private HoldSize _size;

    // Swing tracking.
    private bool _swingInit,
        _swinging;
    private Vector3 _prevOffset,
        _holdVel,
        _peakVel;
    private float _swingSpeed,
        _swingTimer;

    /// <summary>Raised after an object is picked up.</summary>
    public event System.Action<Carriable> Grabbed;

    /// <summary>Raised when a held object is dropped, thrown or stowed.</summary>
    public event System.Action<Carriable> Released;

    public float BreakDistance => breakDistance;
    public bool AreHandsFull => _held != null;
    public bool IsCharging => _held != null && _charging;

    /// <summary>Charging past the tap window (a real throw, not a drop).</summary>
    public bool IsThrowCharging => IsCharging && _holdTimer >= tapTime;
    public Carriable Held => _held;
    public GameObject HeldObject => _held != null ? _held.gameObject : null;
    public HoldSize HeldSize => _held != null ? _size : HoldSize.Auto;
    public bool AlignToView => alignToView;

    /// <summary>Swing throw allowed for the current object size.</summary>
    public bool SwingAllowed =>
        swingThrowEnabled
        && (
            _size == HoldSize.Small ? swingSmall
            : _size == HoldSize.Large ? swingLarge
            : swingMedium
        );

    public Quaternion ViewRotation => Origin != null ? Origin.rotation : Quaternion.identity;

    /// <summary>Hold-E throw charge 0..1.</summary>
    public float Charge01 =>
        _held != null && _charging
            ? Mathf.Clamp01((_holdTimer - tapTime) / Mathf.Max(chargeTime, 0.01f))
            : 0f;

    /// <summary>Camera-swing power 0..1.</summary>
    public float Swing01 =>
        SwingAllowed && _held != null && maxSwingSpeed > 0f
            ? Mathf.Clamp01(
                (_swinging ? _peakVel : _holdVel).magnitude * swingThrowScale / maxSwingSpeed
            )
            : 0f;

    /// <summary>Power for the HUD meter 0..1.</summary>
    public float Power01 => _held == null ? 0f : (_charging ? Charge01 : Swing01);

    /// <summary>View origin (the Interactor's), null if there is none.</summary>
    public Transform Origin => _interactor != null ? _interactor.Origin : null;

    /// <summary>Player body the carried object ignores collisions with.</summary>
    public CharacterController Body => _interactor != null ? _interactor.Body : null;

    void Awake()
    {
        _interactor = GetComponent<Interactor>();
        if (_interactor == null)
        {
            Debug.LogError($"{name}: Interactor missing.", this);
            enabled = false;
        }
    }

    void OnEnable()
    {
        if (_interactor != null)
            _interactor.RegisterHandsFull(this);
    }

    void OnDisable()
    {
        if (_held != null)
            _held.Drop();
        _held = null;
        _pose = null;
        _charging = false;
        ResetSwing();
        if (_interactor != null)
            _interactor.UnregisterHandsFull(this);
    }

    void Update()
    {
        var input = InputManager.Instance;
        if (input == null || Origin == null)
            return; // quitting, or no view

        float dt = Time.deltaTime;

        if (_held == null) // not carrying (also covers destroyed object)
        {
            _held = null;
            _charging = false;
            ResetSwing();
            return;
        }

        ConfigureHold(_held); // live-tweakable
        _carryTime += dt;
        TrackSwing(dt);

        if (input.InteractPressed)
        {
            if (_interactor.DidConsumeInteract) // the Interactor used it on an Interactable
                return;
            if (TryGiveHeldToReceiver())
                return;
            if (_held.TryGetComponent<IHeldInteract>(out var hi) && hi.OnHeldInteract(_interactor))
                return;
            _charging = true;
            _holdTimer = 0f;
            _swinging = false; // charge overrides swing
        }

        if (_charging)
        {
            if (input.InteractReleased)
            {
                ReleaseHeld();
                return;
            }
            if (!input.InteractHeld) // input lost (unlock/menu): cancel charge
            {
                _charging = false;
                return;
            }
            _holdTimer += dt;
            return;
        }

        UpdateSwingThrow(dt);
    }

    // View-rotation speed + hold-point velocity (rotation only).
    void TrackSwing(float dt)
    {
        var origin = Origin;
        if (dt < 1e-5f || origin == null)
            return;

        Vector3 fwd = origin.forward;
        Vector3 offset = fwd * holdDistance;
        if (!_swingInit)
        {
            _prevOffset = offset;
            _swingInit = true;
            return;
        }

        float k = 1f - Mathf.Exp(-dt / Mathf.Max(swingSmoothing, 0.001f));
        float ang = Vector3.Angle(_prevOffset, offset) / dt;
        _swingSpeed = Mathf.Lerp(_swingSpeed, ang, k);
        _holdVel = Vector3.Lerp(_holdVel, (offset - _prevOffset) / dt, k);
        _prevOffset = offset;
    }

    // Start on fast turn, release at the peak.
    void UpdateSwingThrow(float dt)
    {
        if (!SwingAllowed || _held == null || _carryTime < swingGrace)
        {
            _swinging = false; // e.g. size toggled mid-swing
            return;
        }

        if (!_swinging)
        {
            if (_swingSpeed < swingThreshold)
                return;
            _swinging = true;
            _swingTimer = 0f;
            _peakVel = _holdVel;
            return;
        }

        _swingTimer += dt;
        float m = _holdVel.magnitude;
        float peak = _peakVel.magnitude;
        if (m > peak)
        {
            _peakVel = _holdVel;
            peak = m;
        }

        if (m < peak * swingReleaseRatio || _swingTimer >= swingMaxTime)
            FireSwing();
    }

    void FireSwing()
    {
        var p = _held;
        var body = _interactor.Body;
        Vector3 v = Vector3.ClampMagnitude(_peakVel * swingThrowScale, maxSwingSpeed);
        bool ok = p != null && v.magnitude >= minSwingSpeed;
        if (ok && body != null)
            v += body.velocity * inheritVelocity;

        Vector3 send = v;
        ResetSwing();
        if (ok)
            p.Throw(send);
    }

    void ResetSwing()
    {
        _swinging = false;
        _swingInit = false;
        _swingSpeed = 0f;
        _swingTimer = 0f;
        _holdVel = Vector3.zero;
        _peakVel = Vector3.zero;
    }

    // Nearest hit along the view ray, ignoring the held object.
    bool TryCastFromView(out RaycastHit hit)
    {
        var origin = Origin;
        return _interactor.Cast(
            origin.position,
            origin.forward,
            _interactor.InteractRange,
            _held.transform,
            out hit
        );
    }

    // Whatever is under the crosshair (e.g. a shelf) may take the held object.
    bool TryGiveHeldToReceiver()
    {
        if (!TryCastFromView(out var hit))
            return false;

        var receiver = hit.collider.GetComponentInParent<IHeldTargetReceiver>();
        return receiver != null && receiver.OnReceiveHeld(this, _held);
    }

    void ReleaseHeld()
    {
        var p = _held;
        float charge = Charge01;
        bool tap = _holdTimer < tapTime;
        _charging = false;
        if (p == null)
            return;

        if (tap)
            p.Drop();
        else
            p.Throw(Origin.forward * Mathf.Lerp(minThrowSpeed, maxThrowSpeed, charge));
    }

    /// <summary>Called by Carriable. False if rejected.</summary>
    public bool TryGrab(Carriable p)
    {
        if (p == null || p == _held || !isActiveAndEnabled)
            return false;
        if (p.Body == null || p.Body.mass > maxMass)
            return false;
        if (_held != null) // hands full: a storage (e.g. a hotbar) may take it
            return _storage != null && _storage.TryStore(this, p);
        if (!p.Attach(this))
            return false;
        _held = p;
        ConfigureHold(p);
        _charging = false;
        _carryTime = 0f;
        ResetSwing();
        Grabbed?.Invoke(p);
        return true;
    }

    /// <summary>Optional storage for pickups while the hands are full (e.g. a hotbar). One at a time.</summary>
    public void RegisterStorage(ICarrierStorage storage)
    {
        if (_storage != null && _storage != storage)
            Debug.LogWarning($"{name}: carrier storage replaced.", this);
        _storage = storage;
    }

    public void UnregisterStorage(ICarrierStorage storage)
    {
        if (_storage == storage)
            _storage = null;
    }

    /// <summary>Called by Carriable when it lets go.</summary>
    public void Clear(Carriable p)
    {
        if (_held != p)
            return;
        _held = null;
        _pose = null;
        _charging = false;
        ResetSwing();
        Released?.Invoke(p);
    }

    // Pick pose from object size/mass (or its override).
    void ConfigureHold(Carriable p)
    {
        if (p == null)
            return;
        _radius = Mathf.Max(0f, p.Extent) * 0.5f;
        float mass = p.Body != null ? p.Body.mass : 0f;

        _size = p.Size;
        if (_size == HoldSize.Auto)
            _size =
                (p.Extent >= largeExtent || mass >= largeMass) ? HoldSize.Large
                : (p.Extent >= mediumExtent || mass >= mediumMass) ? HoldSize.Medium
                : HoldSize.Small;

        if (_size == HoldSize.Small)
            _pose = smallPose;
        else if (_size == HoldSize.Large)
            _pose = largePose;
        else if (mediumHold == MediumHold.Side)
            _pose = smallPose;
        else if (mediumHold == MediumHold.Center)
            _pose = centerPose;
        else
            _pose = null; // classic
    }

    /// <summary>Carry target before the wall pull-in. View-locked items use this so walls never move them.</summary>
    public Vector3 GetViewHoldPoint(Carriable p) => HoldTarget(p, out _);

    // Wall-independent carry target and the clearance to keep from walls.
    Vector3 HoldTarget(Carriable p, out float clear)
    {
        var origin = Origin;
        clear = holdClearance;
        if (origin == null)
            return p != null ? p.transform.position : transform.position;

        if (p != null && p == _held && _pose != null) // close pose (view space)
        {
            Vector3 l = _pose.offset;
            l.z += _radius * _pose.radiusPush;
            l.y -= _radius * _pose.radiusDrop;
            clear += _radius * 0.5f;
            return origin.TransformPoint(l);
        }
        return origin.position + origin.forward * holdDistance; // classic: out front
    }

    /// <summary>Carry target, pulled in when a wall is ahead.</summary>
    public Vector3 GetHoldPoint(Carriable p)
    {
        var origin = Origin;
        if (origin == null)
            return p != null ? p.transform.position : transform.position;

        Vector3 from = origin.position;
        Vector3 target = HoldTarget(p, out float clear);

        Vector3 to = target - from;
        float dist = to.magnitude;
        if (dist < 1e-4f)
            return target;

        Vector3 dir = to / dist;
        if (_interactor.Cast(from, dir, dist + clear, p != null ? p.transform : null, out var hit))
            dist = Mathf.Clamp(hit.distance - clear, Mathf.Min(minHoldDistance, dist * 0.5f), dist);
        return from + dir * dist;
    }

    void OnDrawGizmos()
    {
        var interactor = _interactor != null ? _interactor : GetComponent<Interactor>();
        var origin = interactor != null ? interactor.Origin : null;
        if (!drawGizmos || origin == null)
            return;

        Vector3 hp = Application.isPlaying
            ? GetHoldPoint(_held)
            : origin.position + origin.forward * holdDistance;
        Gizmos.color = _held != null ? Color.green : Color.white; // hold point
        Gizmos.DrawWireSphere(hp, 0.1f);

        if (_held != null)
        {
            Gizmos.color = Color.magenta; // swing velocity
            Gizmos.DrawRay(hp, (_swinging ? _peakVel : _holdVel) * 0.1f);
        }
    }

    void OnValidate()
    {
        minHoldDistance = Mathf.Max(0.1f, minHoldDistance);
        holdDistance = Mathf.Max(minHoldDistance, holdDistance);
        holdClearance = Mathf.Max(0f, holdClearance);
        breakDistance = Mathf.Max(0.1f, breakDistance);
        maxMass = Mathf.Max(0f, maxMass);
        tapTime = Mathf.Max(0f, tapTime);
        chargeTime = Mathf.Max(0.01f, chargeTime);
        mediumExtent = Mathf.Max(0f, mediumExtent);
        largeExtent = Mathf.Max(mediumExtent, largeExtent);
        mediumMass = Mathf.Max(0f, mediumMass);
        largeMass = Mathf.Max(mediumMass, largeMass);
        maxThrowSpeed = Mathf.Max(minThrowSpeed, maxThrowSpeed);
        swingThreshold = Mathf.Max(1f, swingThreshold);
        swingGrace = Mathf.Max(0f, swingGrace);
        swingThrowScale = Mathf.Max(0f, swingThrowScale);
        maxSwingSpeed = Mathf.Max(0.1f, maxSwingSpeed);
        minSwingSpeed = Mathf.Clamp(minSwingSpeed, 0f, maxSwingSpeed);
        swingMaxTime = Mathf.Max(0.02f, swingMaxTime);
        swingSmoothing = Mathf.Max(0.001f, swingSmoothing);
    }
}
