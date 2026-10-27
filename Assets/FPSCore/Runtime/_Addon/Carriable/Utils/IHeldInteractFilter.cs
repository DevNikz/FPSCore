using UnityEngine;

/// <summary>
/// Put on an Interactable's GameObject to limit which held items it can be used with
/// (e.g. a terminal that only works while carrying a scanner). With none on the object,
/// any held item is accepted. Only asked when the player is holding something.
/// Found once in Interactable.Awake with GetComponents.
/// </summary>
public interface IHeldInteractFilter
{
    bool CanInteractWhileHeld(GameObject held);
}
