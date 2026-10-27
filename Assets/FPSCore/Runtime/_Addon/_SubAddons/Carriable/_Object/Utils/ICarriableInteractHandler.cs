/// <summary>
/// Put on a Carriable's GameObject to take the interact press before it is picked up
/// (e.g. a product on a shelf). Found once in Carriable.Awake with GetComponents.
/// </summary>
public interface ICarriableInteractHandler
{
    /// <summary>
    /// Return true to consume the press, even if the action failed (e.g. shelf full).
    /// Return false to let the press fall through to the pickup.
    /// </summary>
    bool TryHandleInteract(Carriable carriable, Interactor who);
}
