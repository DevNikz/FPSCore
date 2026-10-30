using UnityEngine;

/// <summary>
/// Makes any Rigidbody draggable by <see cref="Dragger"/>. Handles the claim and restores gravity
/// on release. Don't put this next to CarriableDragTarget (both implement IDragTarget).
/// Note: the aim provider decides what can be targeted (e.g. InteractorDragAim needs an Interactable).
/// </summary>
[InteractableAddon("Draggable (Addon)")]
[RequireComponent(typeof(Rigidbody))]
[DisallowMultipleComponent]
public class Draggable : MonoBehaviour, IDragTarget
{
    Rigidbody _body;
    IDragClaimant _claimant;
    bool _hadGravity;
    float _extent;

    public Rigidbody Body
    {
        get
        {
            if (_body == null)
                _body = GetComponent<Rigidbody>();
            return _body;
        }
    }

    public Transform Root => transform;
    public float Extent => _extent;
    public bool IsClaimed => _claimant != null;

    public bool TryClaim(IDragClaimant claimant)
    {
        if (claimant == null || IsClaimed || Body == null)
            return false;

        _claimant = claimant;
        _hadGravity = Body.useGravity; // Dragger changes it while dragging
        _extent = ComputeExtent();
        return true;
    }

    public void ReleaseClaim()
    {
        if (!IsClaimed)
            return;

        var claimant = _claimant;
        _claimant = null;
        if (Body != null)
            Body.useGravity = _hadGravity;
        claimant.OnClaimEnded(this);
    }

    void OnDisable() => ReleaseClaim();

    // Longest side of all colliders combined (m).
    float ComputeExtent()
    {
        var colliders = GetComponentsInChildren<Collider>();
        if (colliders.Length == 0)
            return 0f;

        Bounds bounds = colliders[0].bounds;
        for (int i = 1; i < colliders.Length; i++)
            bounds.Encapsulate(colliders[i].bounds);

        Vector3 size = bounds.size;
        return Mathf.Max(size.x, Mathf.Max(size.y, size.z));
    }
}
