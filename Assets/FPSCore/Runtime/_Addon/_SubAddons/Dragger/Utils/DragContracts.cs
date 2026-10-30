using System;
using UnityEngine;

/// <summary>
/// Shared drag contracts. Lives in Base (outside _Addon) so the Dragger addon, the aim/target
/// adapters and the prompt bridge can all compile on their own, even when the others are missing.
/// Nothing in here references an addon type.
/// </summary>
/// <summary>Tells Dragger where the player is aiming and whether the hands are free.</summary>
public interface IDragAim
{
    /// <summary>View transform (camera) the drag point is held relative to.</summary>
    Transform Origin { get; }

    /// <summary>Player body, if any. Exposed so other systems can ignore it.</summary>
    CharacterController Body { get; }

    /// <summary>True while the player's hands are full (e.g. carrying something).</summary>
    bool IsHandsBusy { get; }

    /// <summary>Object under the crosshair, or null.</summary>
    GameObject GetAimedObject();

    /// <summary>Obstacle probe used to pull the held object in front of walls.</summary>
    bool Cast(Vector3 from, Vector3 dir, float distance, Transform ignore, out RaycastHit hit);
}

/// <summary>Something Dragger can pull around. One claimant at a time.</summary>
public interface IDragTarget
{
    Rigidbody Body { get; }

    /// <summary>Transform used to ignore the object's own colliders when probing for walls.</summary>
    Transform Root { get; }

    /// <summary>Object size in metres. Valid after a successful <see cref="TryClaim"/>.</summary>
    float Extent { get; }

    bool IsClaimed { get; }

    bool TryClaim(IDragClaimant claimant);

    /// <summary>Ends the claim and notifies the claimant through <see cref="IDragClaimant.OnClaimEnded"/>.</summary>
    void ReleaseClaim();
}

/// <summary>Implemented by Dragger. Called when a claim ends from the target's side.</summary>
public interface IDragClaimant
{
    void OnClaimEnded(IDragTarget target);
}

/// <summary>
/// Static drag events so other systems (prompts, audio, UI) can react without referencing Dragger.
/// The GameObject passed to <see cref="Ended"/> can be null if the dragged object was destroyed.
/// </summary>
public static class DragEvents
{
    public static event Action<GameObject> Started;
    public static event Action<GameObject> Ended;

    public static void RaiseStarted(GameObject dragged) => Started?.Invoke(dragged);

    public static void RaiseEnded(GameObject dragged) => Ended?.Invoke(dragged);

    // Keeps subscribers from leaking across play sessions when domain reload is disabled.
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetEvents()
    {
        Started = null;
        Ended = null;
    }
}
