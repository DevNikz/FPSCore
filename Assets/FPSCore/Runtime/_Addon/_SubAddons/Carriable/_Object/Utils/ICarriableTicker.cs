/// <summary>
/// Drives carried and claimed Carriables each fixed and late update by calling their
/// TickFixed and TickLate. Set Carriable.Ticker to replace the default PickupTicker.
/// </summary>
public interface ICarriableTicker
{
    void Add(Carriable carriable);
    void Remove(Carriable carriable);
}
