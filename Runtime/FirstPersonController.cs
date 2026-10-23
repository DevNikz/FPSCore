using UnityEngine;
#if FPS_CINEMACHINE
using Unity.Cinemachine;
#endif

/// <summary>
/// FPS Controller Base
/// - The Main FPS Controller for the player.
/// - It handles all the player movement including the ff:
///     - Move
///     - Look
///     - Crouch
///     - Jump
///     - Sprint (Toggable Limited Stamina)
///     - View bobbing (Toggable)
///     - Zoom In
/// </summary>
[RequireComponent(typeof(CharacterController))]
public class FirstPersonController : MonoBehaviour
{
    [Header("References")]
    [Tooltip("Empty child at eye level. The CinemachineCamera is a child of this.")]
    [SerializeField]
    private Transform cameraTarget;

    [Header("Look")]
    [Tooltip("Degrees per mouse pixel.")]
    [SerializeField]
    private float sensitivity = 0.1f;

    [SerializeField, Range(10f, 89f)]
    private float pitchLimit = 85f;

    [Tooltip("Head turn limit relative to body. Past this, the body rotates.")]
    [SerializeField, Range(30f, 180f)]
    private float maxHeadYaw = 90f;

    [Tooltip("Body auto-aligns to view direction while moving (deg/s). 0 = off.")]
    [SerializeField]
    private float bodyAlignSpeed = 360f;

    [SerializeField]
    private bool invertY;

    [Header("Movement")]
    [SerializeField]
    private float walkSpeed = 4f;

    [SerializeField]
    private float crouchSpeed = 2f;

    [Tooltip("Ground acceleration (m/s²).")]
    [SerializeField]
    private float acceleration = 30f;

    [Tooltip("Acceleration multiplier while airborne.")]
    [SerializeField, Range(0f, 1f)]
    private float airControl = 0.3f;

    [Header("Sprint (Shift)")]
    [SerializeField]
    private float sprintSpeed = 6.5f;

    [Tooltip("On = stamina-limited. Off = unlimited sprint.")]
    [SerializeField]
    private bool limitedStamina = true;

    [SerializeField]
    private float maxStamina = 100f;

    [Tooltip("Stamina spent per second while sprinting.")]
    [SerializeField]
    private float staminaDrainRate = 20f;

    [Tooltip("Stamina regained per second when not sprinting.")]
    [SerializeField]
    private float staminaRegenRate = 15f;

    [Tooltip("Seconds after sprinting before regen starts.")]
    [SerializeField]
    private float regenDelay = 1f;

    [Tooltip("After running out, stamina fraction needed to sprint again.")]
    [SerializeField, Range(0f, 1f)]
    private float exhaustRecovery = 0.25f;

    [Tooltip("Bob amplitude multiplier while sprinting.")]
    [SerializeField]
    private float sprintBobMultiplier = 1.3f;

    [Tooltip("Runtime display (overwritten each frame when limited).")]
    [SerializeField]
    private float stamina;

    [Header("Jump")]
    [SerializeField]
    private float jumpHeight = 1.2f;

    [SerializeField]
    private float gravity = 20f;

    [Header("Crouch")]
    [SerializeField]
    private float crouchHeight = 1f;

    [Tooltip("Eye height above feet.")]
    [SerializeField]
    private float standEyeHeight = 1.7f;

    [SerializeField]
    private float crouchEyeHeight = 0.9f;

    [SerializeField]
    private float crouchTime = 0.2f;

    [Tooltip("Sprinting while moving cancels toggled crouch.")]
    [SerializeField]
    private bool sprintCancelsToggle = true;

    [SerializeField]
    private LayerMask obstructionMask = ~0;

    [Header("Zoom")]
    [SerializeField]
    private bool zoomEnabled = true;

    [Tooltip("Auto-found under CameraTarget if empty.")]
    [SerializeField]
    private CinemachineCamera vcam;

    [Tooltip("Vertical FOV when fully zoomed (lower = closer).")]
    [SerializeField, Range(5f, 90f)]
    private float zoomFov = 30f;

    [SerializeField]
    private float zoomInTime = 0.25f;

    [SerializeField]
    private float zoomOutTime = 0.2f;

    [Tooltip("Easing over 0..1. X = progress, Y = zoom amount.")]
    [SerializeField]
    private AnimationCurve zoomCurve = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);

    [Tooltip("Look sensitivity multiplier at full zoom.")]
    [SerializeField, Range(0.1f, 1f)]
    private float zoomSensitivity = 0.5f;

    [Header("View Bob")]
    [SerializeField]
    private bool bobEnabled = true;

    [Tooltip("Cycles/sec at walk speed.")]
    [SerializeField]
    private float bobFrequency = 1.8f;

    [SerializeField]
    private float bobAmplitudeY = 0.05f;

    [SerializeField]
    private float bobAmplitudeX = 0.03f;

    [Tooltip("Amplitude multiplier while crouched.")]
    [SerializeField, Range(0f, 1f)]
    private float crouchBobMultiplier = 0.6f;

    [Tooltip("Fade in/out speed of the bob.")]
    [SerializeField]
    private float bobBlendSpeed = 8f;

    private CharacterController _cc;
    private float _standHeight,
        _feetOffset; // feet Y relative to transform
    private float _pitch,
        _headYaw; // headYaw is world-space
    private float _crouchT; // 0 stand .. 1 crouched
    private bool _wantsCrouch,
        _crouchToggled; // C-key latch
    private Vector3 _horizVel;
    private float _vertVel;
    private bool _grounded;
    private float _bobPhase,
        _bobWeight;
    private float _baseFov,
        _zoomT,
        _zoomEased; // _zoomT linear 0..1, _zoomEased after curve
    private bool _zoomReady;
    private bool _sprinting,
        _exhausted;
    private float _regenTimer,
        _sprintBlend;
    private readonly Collider[] _hits = new Collider[8];

    public bool IsGrounded => _grounded;
    public bool IsCrouching => _wantsCrouch;
    public bool IsCrouchToggled => _crouchToggled;
    public bool IsSprinting => _sprinting;
    public bool IsExhausted => _exhausted;
    public bool LimitedStamina => limitedStamina;
    public float Stamina => stamina;
    public float StaminaNormalized => maxStamina > 0f ? stamina / maxStamina : 1f;

    private void Awake()
    {
        _cc = GetComponent<CharacterController>();
        if (cameraTarget == null)
            cameraTarget = transform.Find("CameraTarget");
        if (cameraTarget == null)
        {
            Debug.LogError($"{name}: CameraTarget not assigned.", this);
            enabled = false;
            return;
        }
        _standHeight = _cc.height;
        _feetOffset = _cc.center.y - _cc.height * 0.5f;
        _headYaw = transform.eulerAngles.y;
        stamina = maxStamina;

        if (vcam == null)
            vcam = cameraTarget.GetComponentInChildren<CinemachineCamera>(true);
        _zoomReady = vcam != null;
        if (_zoomReady)
            _baseFov = vcam.Lens.FieldOfView;
        else
            Debug.LogWarning($"{name}: no CinemachineCamera found. Zoom disabled.", this);
    }

    private void OnDisable()
    {
        // Restore FOV so the camera isn't left zoomed.
        _zoomT = _zoomEased = 0f;
        _crouchToggled = false;
        if (_zoomReady && vcam != null)
            vcam.Lens.FieldOfView = _baseFov;
    }

    private void Update()
    {
        var input = InputManager.Instance;
        if (input == null)
            return; // quitting

        float dt = Time.deltaTime;
        Vector2 move = input.Move;

        UpdateZoom(input.ZoomHeld, dt);
        UpdateLook(input.Look, move.sqrMagnitude > 0.01f, dt);
        UpdateCrouch(
            input.CrouchHeld,
            input.CrouchTogglePressed,
            input.SprintHeld && move.sqrMagnitude > 0.01f,
            dt
        );
        UpdateSprint(input.SprintHeld, move, dt);
        UpdateMovement(move, input.JumpPressed, dt);
        UpdateCamera(dt);
    }

    private void UpdateZoom(bool held, float dt)
    {
        if (!_zoomReady || vcam == null)
            return;

        bool zoomIn = zoomEnabled && held;
        float time = zoomIn ? zoomInTime : zoomOutTime;
        _zoomT = Mathf.MoveTowards(_zoomT, zoomIn ? 1f : 0f, dt / Mathf.Max(time, 0.01f));

        // Null/empty curve falls back to smoothstep.
        _zoomEased =
            (zoomCurve != null && zoomCurve.length > 0)
                ? Mathf.Clamp01(zoomCurve.Evaluate(_zoomT))
                : Mathf.SmoothStep(0f, 1f, _zoomT);

        float target = Mathf.Min(zoomFov, _baseFov); // never zoom "out"
        float fov = Mathf.Lerp(_baseFov, target, _zoomEased);
        if (!Mathf.Approximately(vcam.Lens.FieldOfView, fov))
            vcam.Lens.FieldOfView = fov;
    }

    private void UpdateLook(Vector2 delta, bool moving, float dt)
    {
        delta *= sensitivity * Mathf.Lerp(1f, zoomSensitivity, _zoomEased); // slower aim when zoomed
        _pitch = Mathf.Clamp(_pitch + (invertY ? delta.y : -delta.y), -pitchLimit, pitchLimit);
        _headYaw = Mathf.Repeat(_headYaw + delta.x, 360f);

        float bodyYaw = transform.eulerAngles.y;
        float rel = Mathf.DeltaAngle(bodyYaw, _headYaw);

        if (Mathf.Abs(rel) > maxHeadYaw) // head past limit: drag body
            bodyYaw = _headYaw - Mathf.Sign(rel) * maxHeadYaw;
        else if (moving && bodyAlignSpeed > 0f) // walking: body follows view
            bodyYaw = Mathf.MoveTowardsAngle(bodyYaw, _headYaw, bodyAlignSpeed * dt);

        transform.rotation = Quaternion.Euler(0f, bodyYaw, 0f);
        cameraTarget.localRotation = Quaternion.Euler(
            _pitch,
            Mathf.DeltaAngle(bodyYaw, _headYaw),
            0f
        );
    }

    private void UpdateCrouch(bool held, bool togglePressed, bool sprintIntent, float dt)
    {
        if (togglePressed)
            _crouchToggled = !_crouchToggled;
        else if (_crouchToggled && sprintCancelsToggle && sprintIntent && !held && CanStand())
            _crouchToggled = false; // sprint exits crouch

        if (held || _crouchToggled)
            _wantsCrouch = true;
        else if (_wantsCrouch && CanStand())
            _wantsCrouch = false;

        _crouchT = Mathf.MoveTowards(
            _crouchT,
            _wantsCrouch ? 1f : 0f,
            dt / Mathf.Max(crouchTime, 0.01f)
        );
        float s = Mathf.SmoothStep(0f, 1f, _crouchT);

        float h = Mathf.Lerp(_standHeight, crouchHeight, s);
        _cc.height = h;
        _cc.center = new Vector3(0f, _feetOffset + h * 0.5f, 0f); // keep feet planted
    }

    private bool CanStand()
    {
        float r = _cc.radius * 0.95f;
        Vector3 p = transform.position + Vector3.up * (_feetOffset + _standHeight - _cc.radius);
        int n = Physics.OverlapSphereNonAlloc(
            p,
            r,
            _hits,
            obstructionMask,
            QueryTriggerInteraction.Ignore
        );
        for (int i = 0; i < n; i++)
            if (_hits[i] != null && _hits[i] != _cc)
                return false; // ignore self
        return true;
    }

    private void UpdateSprint(bool held, Vector2 move, float dt)
    {
        maxStamina = Mathf.Max(0f, maxStamina);
        stamina = Mathf.Clamp(stamina, 0f, maxStamina);

        // Crouch has priority. Needs movement input.
        bool wants = held && move.sqrMagnitude > 0.01f && !_wantsCrouch;

        if (!limitedStamina)
        {
            _exhausted = false;
            stamina = maxStamina;
            _sprinting = wants;
        }
        else
        {
            if (_exhausted && stamina >= maxStamina * exhaustRecovery)
                _exhausted = false;
            _sprinting = wants && !_exhausted && stamina > 0f;

            if (_sprinting)
            {
                stamina -= staminaDrainRate * dt;
                _regenTimer = regenDelay;
                if (stamina <= 0f)
                {
                    stamina = 0f;
                    _exhausted = true;
                    _sprinting = false;
                }
            }
            else
            {
                _regenTimer -= dt;
                if (_regenTimer <= 0f)
                    stamina = Mathf.Min(maxStamina, stamina + staminaRegenRate * dt);
            }
        }
        _sprintBlend = Mathf.MoveTowards(_sprintBlend, _sprinting ? 1f : 0f, bobBlendSpeed * dt);
    }

    private void UpdateMovement(Vector2 move, bool jump, float dt)
    {
        Vector3 wish = Quaternion.Euler(0f, _headYaw, 0f) * new Vector3(move.x, 0f, move.y);
        if (wish.sqrMagnitude > 1f)
            wish.Normalize();

        float s = Mathf.SmoothStep(0f, 1f, _crouchT);
        float maxSpeed = _sprinting ? sprintSpeed : Mathf.Lerp(walkSpeed, crouchSpeed, s);
        float accel = _grounded ? acceleration : acceleration * airControl;
        _horizVel = Vector3.MoveTowards(_horizVel, wish * maxSpeed, accel * dt);

        if (_grounded && _vertVel < 0f)
            _vertVel = -2f; // stick to ground
        if (_grounded && jump)
            _vertVel = Mathf.Sqrt(2f * gravity * jumpHeight);
        _vertVel -= gravity * dt;

        var flags = _cc.Move((_horizVel + Vector3.up * _vertVel) * dt);
        _grounded = _cc.isGrounded;
        if ((flags & CollisionFlags.Above) != 0 && _vertVel > 0f)
            _vertVel = 0f;
    }

    private void UpdateCamera(float dt)
    {
        float s = Mathf.SmoothStep(0f, 1f, _crouchT);
        float eye = Mathf.Lerp(standEyeHeight, crouchEyeHeight, s);
        Vector3 basePos = new Vector3(0f, _feetOffset + eye, 0f);

        float speed = new Vector2(_horizVel.x, _horizVel.z).magnitude;
        float maxSpeed = _sprinting ? sprintSpeed : Mathf.Lerp(walkSpeed, crouchSpeed, s);

        bool active = bobEnabled && _grounded && speed > 0.1f;
        _bobWeight = Mathf.Lerp(_bobWeight, active ? 1f : 0f, 1f - Mathf.Exp(-bobBlendSpeed * dt));

        if (active)
        {
            _bobPhase += 2f * Mathf.PI * bobFrequency * (speed / Mathf.Max(walkSpeed, 0.01f)) * dt;
            _bobPhase = Mathf.Repeat(_bobPhase, 2f * Mathf.PI);
        }

        float amp =
            _bobWeight
            * Mathf.Clamp01(speed / Mathf.Max(maxSpeed, 0.01f))
            * Mathf.Lerp(1f, crouchBobMultiplier, s)
            * Mathf.Lerp(1f, sprintBobMultiplier, _sprintBlend);
        Vector3 bob =
            new Vector3(
                Mathf.Cos(_bobPhase) * bobAmplitudeX,
                Mathf.Sin(_bobPhase * 2f) * bobAmplitudeY,
                0f
            ) * amp;

        cameraTarget.localPosition = basePos + bob;
    }

    private void OnValidate()
    {
        sprintSpeed = Mathf.Max(sprintSpeed, walkSpeed);
        maxStamina = Mathf.Max(0f, maxStamina);
        staminaDrainRate = Mathf.Max(0f, staminaDrainRate);
        staminaRegenRate = Mathf.Max(0f, staminaRegenRate);
        regenDelay = Mathf.Max(0f, regenDelay);
        crouchHeight = Mathf.Max(0.5f, crouchHeight);
        crouchEyeHeight = Mathf.Min(crouchEyeHeight, standEyeHeight);
        jumpHeight = Mathf.Max(0f, jumpHeight);
        gravity = Mathf.Max(0.1f, gravity);
    }
}
