using UnityEngine;

/// <summary>
/// View lock for a carried Carriable. Physics fully off, and the object is parented to the
/// view camera, so Unity composes it with the final camera pose (no lag, no sway) and children
/// such as a world-space canvas move with it. The Carriable writes the local pose each frame.
/// </summary>
public sealed class CarriableViewLock
{
    private readonly Carriable _owner;
    private readonly Rigidbody _rb;
    private bool _isLocked,
        _lockKin,
        _lockDetect;
    private Collider[] _lockCols;
    private bool[] _lockColOn;
    private Transform _lockView, // camera the object is parented to while locked
        _lockSavedParent; // parent to return to on release
    private bool _pendingUnparent; // release happened while the parent could not be changed

    public CarriableViewLock(Carriable owner, Rigidbody rb)
    {
        _owner = owner;
        _rb = rb;
    }

    public bool IsLocked => _isLocked;

    /// <summary>Transform the object is parented to while locked.</summary>
    public Transform View => _lockView;

    public void Begin(Transform view)
    {
        if (view == null)
        {
            Debug.LogWarning($"{_owner.name}: no view to lock to. Carrying with physics.", _owner);
            return;
        }

        _lockKin = _rb.isKinematic; // false here: Attach rejects kinematic bodies
        _lockDetect = _rb.detectCollisions;
        _rb.linearVelocity = Vector3.zero;
        _rb.angularVelocity = Vector3.zero;
        _rb.isKinematic = true;
        _rb.detectCollisions = false;
        _rb.interpolation = RigidbodyInterpolation.None; // no physics smoothing on a locked pose

        _lockCols = _owner.GetOwnColliders();
        _lockColOn = new bool[_lockCols.Length];
        for (int i = 0; i < _lockCols.Length; i++)
        {
            var c = _lockCols[i];
            _lockColOn[i] = c != null && c.enabled;
            if (c != null)
                c.enabled = false; // also hides it from raycasts
        }

        _lockSavedParent = _owner.transform.parent;
        _lockView = view;
        _owner.transform.SetParent(view, true);
        _isLocked = true;
    }

    public void End()
    {
        if (!_isLocked)
            return;
        _isLocked = false;

        if (_rb != null)
        {
            _rb.isKinematic = _lockKin;
            _rb.detectCollisions = _lockDetect;
        }
        for (int i = 0; i < _lockCols.Length; i++)
            if (_lockCols[i] != null)
                _lockCols[i].enabled = _lockColOn[i];
        _owner.SetIgnore(true); // re-enabled colliders start unpaired with the player; Disengage clears it
        _lockCols = null;
        _lockColOn = null;
        RestoreParent();
    }

    /// <summary>Finishes a re-parent that had to wait (call from the owner's OnEnable).</summary>
    public void RestorePendingParent()
    {
        if (_pendingUnparent)
            RestoreParent();
    }

    // Back to the original parent, keeping the world pose. Unity refuses to re-parent while this
    // object or the camera is being (de)activated, so that case waits for OnEnable.
    void RestoreParent()
    {
        var tr = _owner.transform;
        if (_lockView == null || tr.parent != _lockView)
        {
            _pendingUnparent = false;
            _lockView = null;
            return;
        }
        if (!_owner.isActiveAndEnabled || !_lockView.gameObject.activeInHierarchy)
        {
            _pendingUnparent = true;
            return;
        }
        tr.SetParent(_lockSavedParent, true);
        _pendingUnparent = false;
        _lockView = null;
    }
}
