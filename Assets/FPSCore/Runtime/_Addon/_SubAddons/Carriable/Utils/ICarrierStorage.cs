/// <summary>
/// Optional storage behind a <see cref="Carrier"/> (e.g. a hotbar). Asked to take an object the
/// Carrier cannot hold because its hands are full. Register with Carrier.RegisterStorage.
/// </summary>
public interface ICarrierStorage
{
    /// <summary>Store the object (and hide it). False = rejected, it stays in the world.</summary>
    bool TryStore(Carrier from, Carriable p);
}
