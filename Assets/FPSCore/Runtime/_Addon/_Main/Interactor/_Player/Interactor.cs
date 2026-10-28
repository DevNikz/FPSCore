using System;
using UnityEngine;

/// <summary>
/// Player-side aiming and interacting: view origin, ray cast, crosshair Target, interact press.
/// Carrying lives in the Carrier addon, which reports full hands through IHandsFull. While
/// carrying, Target is only an Interactable that allows Holding (Interactable.CanInteractIn).
/// </summary>
[FPSAddon("Interactor (Main | Add this first)")]
[RequireComponent(typeof(FirstPersonController))]
[DefaultExecutionOrder(50)]
[DisallowMultipleComponent]
public class Interactor : MonoBehaviour
{
    [Header("References")]
    [Tooltip("View origin (CameraTarget). Auto-found if empty.")]
    [SerializeField]
    private Transform origin;

    [Header("Interact")]
    [SerializeField]
    private float interactRange = 3f;

    [Tooltip("Layers the rays can hit.")]
    [SerializeField]
    private LayerMask rayMask = ~0;

    [Header("Debug")]
    [SerializeField]
    private bool drawGizmos = true;

    [Header("Debug (read-only)")]
    [SerializeField]
    private Interactable _targetDebug;

    private CharacterController _body;
    private Interactable _target;
    private IHandsFull _handsFull;
    private readonly RaycastHit[] _buf = new RaycastHit[16];

    public CharacterController Body => _body;
    public Transform Origin => origin;
    public float InteractRange => interactRange;

    /// <summary>Interactable under the crosshair that can be used in the current state.</summary>
    public Interactable Target
    {
        get => _target;
        private set
        {
            _targetDebug = value;
            if (_target == value)
                return;
            _target = value;
            TargetChanged?.Invoke(value);
        }
    }

    /// <summary>Raised when Target changes (including to null). Listeners (e.g. outlines) hook in here.</summary>
    public event Action<Interactable> TargetChanged;

    /// <summary>True while an addon (Carrier) reports full hands.</summary>
    public bool IsHolding => _handsFull != null && _handsFull.AreHandsFull;

    /// <summary>This frame's interact press went to Target. Carrier skips it.</summary>
    public bool DidConsumeInteract { get; private set; }

    private void Awake()
    {
        _body = GetComponent<CharacterController>(); // Cache Character Controller.
        if (origin == null)
            origin = transform.Find("CameraTarget");
        if (origin == null && Camera.main != null)
            origin = Camera.main.transform;
        if (origin == null)
        {
            Debug.LogError($"{name}: no view origin. Assign CameraTarget.", this);
            enabled = false;
        }
    }

    private void OnDisable()
    {
        Target = null;
        DidConsumeInteract = false;
    }

    private void Update()
    {
        DidConsumeInteract = false;
        var input = InputManager.Instance;
        if (input == null)
            return; // quitting

        UpdateTarget();
        if (input.InteractPressed && Target != null)
        {
            DidConsumeInteract = true; // Carrier skips this press
            Target.Interact(this);
        }
    }

    private void UpdateTarget()
    {
        Target = null;
        var held = IsHolding ? _handsFull.HeldObject : null;
        var ignore = held != null ? held.transform : null;
        if (!Cast(origin.position, origin.forward, interactRange, ignore, out var hit))
            return;
        var i = hit.collider.GetComponentInParent<Interactable>();
        var state = IsHolding ? PromptContext.Holding : PromptContext.EmptyHands;
        if (i != null && i.CanInteractIn(state, held))
            Target = i;
    }

    /// <summary>Called by an addon that can fill the hands (Carrier).</summary>
    public void RegisterHandsFull(IHandsFull source)
    {
        _handsFull = source;
    }

    /// <summary>Called by that addon when it is disabled.</summary>
    public void UnregisterHandsFull(IHandsFull source)
    {
        if (_handsFull == source)
            _handsFull = null;
    }

    /// <summary>Nearest hit, skipping self and 'ignore'.</summary>
    public bool Cast(Vector3 from, Vector3 dir, float dist, Transform ignore, out RaycastHit best)
    {
        best = default;
        if (origin == null)
            return false;

        int n = Physics.RaycastNonAlloc(
            from,
            dir,
            _buf,
            dist,
            rayMask,
            QueryTriggerInteraction.Ignore
        );
        float min = float.MaxValue;
        bool found = false;
        for (int i = 0; i < n; i++)
        {
            var c = _buf[i].collider;
            if (c == null)
                continue;
            var t = c.transform;
            if (t.IsChildOf(transform) || (ignore != null && t.IsChildOf(ignore)))
                continue;
            if (_buf[i].distance < min)
            {
                min = _buf[i].distance;
                best = _buf[i];
                found = true;
            }
        }
        return found;
    }

    private void OnDrawGizmos()
    {
        if (!drawGizmos || origin == null)
            return;

        Gizmos.color = Color.yellow; // interact ray
        Gizmos.DrawLine(origin.position, origin.position + origin.forward * interactRange);
    }

    private void OnValidate()
    {
        interactRange = Mathf.Max(0.1f, interactRange);
    }
}
