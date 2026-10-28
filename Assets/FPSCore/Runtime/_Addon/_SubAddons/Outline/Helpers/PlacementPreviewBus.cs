using System;
using UnityEngine;

public enum PreviewValidity
{
    Valid,
    Invalid,
}

/// <summary>
/// Placement previews (e.g. a furniture ghost). Placers call Draw every frame while previewing;
/// renderers (e.g. the outline sub-addon) listen to PreviewRequested. Nothing here knows who listens.
/// </summary>
public static class PlacementPreviewBus
{
    /// <summary>Raised per Draw call (mesh, localToWorld, subMeshCount, validity).</summary>
    public static event Action<Mesh, Matrix4x4, int, PreviewValidity> PreviewRequested;

    /// <summary>Call every frame the preview should show.</summary>
    public static void Draw(
        Mesh mesh,
        Matrix4x4 localToWorld,
        int subMeshCount,
        PreviewValidity validity
    ) => PreviewRequested?.Invoke(mesh, localToWorld, subMeshCount, validity);

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStatics() => PreviewRequested = null;
}
