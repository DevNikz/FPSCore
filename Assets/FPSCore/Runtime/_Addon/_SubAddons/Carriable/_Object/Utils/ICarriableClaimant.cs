using UnityEngine;

/// <summary>
/// Drives a Carriable without carrying it (e.g. Grabber dragging). Carriable.TryClaim sets up
/// the physics, and Carriable calls Clear when the claim ends.
/// </summary>
public interface ICarriableClaimant
{
    /// <summary>Player body the item ignores collisions with while claimed.</summary>
    CharacterController Body { get; }

    /// <summary>Called by Carriable when the claim ends.</summary>
    void Clear(Carriable carriable);
}
