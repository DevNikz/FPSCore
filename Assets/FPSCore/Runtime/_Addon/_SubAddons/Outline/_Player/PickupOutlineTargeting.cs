using UnityEngine;

/// <summary>
/// Outlines what the player is aiming at. Listens to Interactor.TargetChanged; the target's
/// components are resolved once per change, Update only re-checks cheap flags.
/// </summary>
[FPSAddon("Outline Renderer (Sub-Addon)")]
[DefaultExecutionOrder(100)] // after Interactor (50)
[RequireComponent(typeof(Interactor))]
[DisallowMultipleComponent]
public class PickupOutlineTargeting : MonoBehaviour
{
    Interactor _interactor;
    Interactable _target; // aimed at, from Interactor
    Carriable _targetCarriable; // cached on target change
    Carriable _current; // currently outlined as a pickup
    Interactable _currentMain; // currently outlined as an interactable

    void Awake()
    {
        if (!TryGetComponent(out _interactor))
        {
            Debug.LogError($"{name}: Interactor missing.", this);
            enabled = false;
        }
    }

    void OnEnable()
    {
        _interactor.TargetChanged += HandleTargetChanged;
        HandleTargetChanged(_interactor.Target);
    }

    void OnDisable()
    {
        _interactor.TargetChanged -= HandleTargetChanged;
        _target = null;
        _targetCarriable = null;
        SetInteractOutline(null); // never leave an outline stuck on
        SetPickupOutline(null);
    }

    void HandleTargetChanged(Interactable target)
    {
        _target = target;
        _targetCarriable = null;
        if (target != null)
            target.TryGetComponent(out _targetCarriable);
    }

    void Update()
    {
        Carriable next = null;
        if (
            _targetCarriable != null
            && !_interactor.IsHolding
            && !IsCarriableBusy(_targetCarriable)
        )
            next = _targetCarriable;
        SetPickupOutline(next);

        SetInteractOutline(next == null ? _target : null);
    }

    static bool IsCarriableBusy(Carriable c) => c.IsHeld || c.IsClaimed;

    void SetInteractOutline(Interactable next)
    {
        if (next == _currentMain)
            return;
        PickupOutlineRegistry.Unhighlight(_currentMain);
        _currentMain = next;
        PickupOutlineRegistry.Highlight(_currentMain);
    }

    void SetPickupOutline(Carriable next)
    {
        if (next == _current)
            return;
        PickupOutlineRegistry.Unhighlight(_current);
        _current = next;
        PickupOutlineRegistry.Highlight(_current);
    }
}
