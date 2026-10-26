using UnityEngine;

/// <summary>
/// Put next to an Interactable that is not a PickupObject, so it still gets a pickup outline.
/// (A PickupObject does this itself, see IInteractableHooks.) Never use both on one object.
/// </summary>
[RequireComponent(typeof(Interactable))]
public class InteractableOutlineHook : MonoBehaviour, IInteractableHooks
{
    public void OnInteractableEnabled(Interactable interactable) { }

    public void OnInteractableDisabled(Interactable interactable) { }

    // Custom Hooks to Outline Renderer (Not included in FPS Core Package)
    // public void OnInteractableEnabled(Interactable interactable) =>
    //     PickupOutlineRegistry.Register(interactable);

    // public void OnInteractableDisabled(Interactable interactable) =>
    //     PickupOutlineRegistry.Unregister(interactable);
}
