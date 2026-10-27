/// <summary>
/// Put on anything the player can aim at while carrying (shelf, crate, counter).
/// Carrier calls it when interact is pressed with the crosshair over this object
/// (found with GetComponentInParent), after the Interactor has offered the press to an Interactable that allows Holding,
/// before IHeldInteract and throw charging.
/// </summary>
public interface IHeldTargetReceiver
{
    /// <summary>
    /// Return true to consume the press, even if the action failed (e.g. shelf full).
    /// Return false to let the press fall through. Check isActiveAndEnabled yourself.
    /// </summary>
    bool OnReceiveHeld(Carrier carrier, Carriable held);
}
