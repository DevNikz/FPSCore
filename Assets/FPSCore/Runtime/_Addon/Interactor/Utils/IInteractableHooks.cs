/// <summary>
/// Put on the same object as an Interactable to react when it turns on or off (outlines, registries).
/// Optional: an Interactable with none works on its own. Only the first one found is called.
/// Also called while the object is being destroyed, so don't assume it is fully alive.
/// </summary>
public interface IInteractableHooks
{
    void OnInteractableEnabled(Interactable interactable);

    void OnInteractableDisabled(Interactable interactable);
}
