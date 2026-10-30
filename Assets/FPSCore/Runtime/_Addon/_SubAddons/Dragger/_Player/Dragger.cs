using UnityEngine;

/// <summary>
/// Pulls a claimed Rigidbody toward a point in front of the view (lifted) or along the ground.
/// Standalone: it only needs an <see cref="IDragAim"/> on the same object (e.g. InteractorDragAim)
/// and targets that implement <see cref="IDragTarget"/> (Draggable, CarriableDragTarget).
/// Tuning lives in three helpers: <see cref="DragHold"/>, <see cref="DragFeel"/>, <see cref="DragPlayerScale"/>.
/// Other systems listen to <see cref="DragEvents"/>.
/// </summary>
[FPSAddon("Dragger")]
[DefaultExecutionOrder(60)]
public class Dragger : MonoBehaviour, IDragClaimant
{
    [Header("Hold")]
    [SerializeField]
    DragHold hold = new DragHold();

    [Header("Feel (mass and size)")]
    [SerializeField]
    DragFeel feel = new DragFeel();

    [Header("Player")]
    [SerializeField]
    DragPlayerScale playerScale = new DragPlayerScale();

    IDragAim _aim;
    IDragTarget _target;
    Component _targetComponent; // Unity-null check catches a destroyed target
    Vector3 _localPoint; // object center in view space at grab (z = carry distance)
    float _load; // effective load (kg)
    float _heavy; // 0 = light .. 1 = very heavy
    bool _isLifted; // false = drag on the ground

    bool HasTarget => _targetComponent != null;

    public bool IsDragging => HasTarget;
    public IDragTarget Dragged => HasTarget ? _target : null;
    public CharacterController Body => _aim != null ? _aim.Body : null;

    /// <summary>Multiply your player move speed by this (1 when not dragging).</summary>
    public float MoveSpeedScale => playerScale.GetMoveScale(IsDragging, _heavy);

    void Awake()
    {
        _aim = GetComponent<IDragAim>();
        if (_aim == null)
        {
            Debug.LogError(
                $"{name}: IDragAim missing. Add an aim provider (e.g. InteractorDragAim).",
                this
            );
            enabled = false;
        }
    }

    void OnDisable()
    {
        End();
        playerScale.ResetLook();
        var input = InputManager.Instance;
        if (input != null)
            input.LookScale = 1f; // never leave look dulled
    }

    void Update()
    {
        var input = InputManager.Instance;
        if (input == null)
            return; // quitting

        // Heavier object = slower mouse look (blended so grab/release doesn't pop).
        input.LookScale = playerScale.TickLook(IsDragging, _heavy, Time.unscaledDeltaTime);

        if (_target != null)
        {
            // Let go on release, when hands get full, or if the drag was ended elsewhere.
            if (!HasTarget || !IInput.GrabHeld || _aim.IsHandsBusy || !_target.IsClaimed)
                End();
            return;
        }

        if (IInput.GrabPressed && !_aim.IsHandsBusy)
            Begin();
    }

    void FixedUpdate()
    {
        if (!HasTarget)
            return; // none, or destroyed

        var rb = _target.Body;
        var origin = _aim != null ? _aim.Origin : null;
        if (rb == null || origin == null)
        {
            End();
            return;
        }

        _heavy = feel.GetHeaviness(_load);

        Vector3 holdPoint = hold.GetHoldPoint(_aim, origin, _localPoint, _isLifted, _target);
        Vector3 to = holdPoint - rb.position;
        if (!_isLifted)
            to.y = 0f; // ground drag: horizontal only
        if (hold.IsBeyondBreak(to))
        {
            End();
            return;
        }

        rb.linearVelocity = feel.StepVelocity(
            rb.linearVelocity,
            to,
            _isLifted,
            _heavy,
            Time.fixedDeltaTime
        );
    }

    void Begin()
    {
        var origin = _aim.Origin;
        var aimed = _aim.GetAimedObject();
        if (origin == null || aimed == null)
            return;

        if (!aimed.TryGetComponent(out IDragTarget target))
            return;

        var component = target as Component;
        if (component == null || target.Body == null || target.IsClaimed)
            return;
        if (!target.TryClaim(this))
            return;

        _target = target;
        _targetComponent = component;

        Vector3 local = origin.InverseTransformPoint(target.Body.position);
        local.z = hold.ClampCarryDistance(local.z);
        _localPoint = local;

        _load = feel.ComputeLoad(target.Body.mass, target.Extent); // Extent is set by TryClaim
        _heavy = feel.GetHeaviness(_load);
        _isLifted = feel.CanLift(_load);
        target.Body.useGravity = !_isLifted; // the target restores it on release

        DragEvents.RaiseStarted(component.gameObject);
    }

    void End()
    {
        var target = _target;
        var component = _targetComponent;
        ClearTarget();
        if (target == null)
            return;

        if (component != null) // skip release work if it was destroyed
        {
            target.ReleaseClaim();
            var rb = target.Body;
            if (rb != null)
                rb.linearVelocity = hold.ClampReleaseVelocity(rb.linearVelocity);
        }

        DragEvents.RaiseEnded(component != null ? component.gameObject : null);
    }

    /// <summary>Called by the target when its claim ends from its side (e.g. picked up elsewhere).</summary>
    public void OnClaimEnded(IDragTarget target)
    {
        if (!ReferenceEquals(_target, target))
            return;

        var component = _targetComponent;
        ClearTarget();
        DragEvents.RaiseEnded(component != null ? component.gameObject : null);
    }

    void ClearTarget()
    {
        _target = null;
        _targetComponent = null;
    }

    void OnValidate()
    {
        hold.Validate();
        feel.Validate();
        playerScale.Validate();
    }
}
