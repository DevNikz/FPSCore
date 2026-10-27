using UnityEngine;

/// <summary>
/// Keeps a carried Carriable out of the view camera's near plane. Measured on pickup,
/// then asked how far forward the object must move.
/// </summary>
public sealed class CarriableCameraClearance
{
    private readonly Transform _owner;
    private Camera _cam; // view camera, only while carried with Keep Clear Of Camera
    private Bounds _localBounds; // visual bounds in local space (for the camera clearance test)
    private bool _hasBounds;

    public CarriableCameraClearance(Transform owner)
    {
        _owner = owner;
    }

    public Camera Camera => _cam;
    public Vector3 LocalCenter => _localBounds.center;

    /// <summary>Camera and bounds are both known, so the near-plane test can run.</summary>
    public bool IsReady => _cam != null && _hasBounds;

    public void Clear()
    {
        _cam = null;
        _hasBounds = false;
    }

    public void Measure(Carrier who)
    {
        _cam = FindViewCamera(who);
        _hasBounds = CarriableBounds.MeasureLocalBounds(_owner, out _localBounds);
    }

    // How far forward (m) the object must move so no corner of its bounds is nearer than
    // near plane + clearance when placed at pos/rot. 0 = already clear.
    public float NearDeficit(Vector3 pos, Quaternion rot, float clearance)
    {
        Transform cam = _cam.transform;
        Vector3 fwd = cam.forward;
        Vector3 sc = _owner.lossyScale;
        float minZ = float.MaxValue;
        for (int i = 0; i < 8; i++)
        {
            Vector3 corner = CarriableBounds.GetBoundsCorner(_localBounds, i);
            Vector3 world = pos + rot * Vector3.Scale(corner, sc);
            minZ = Mathf.Min(minZ, Vector3.Dot(world - cam.position, fwd));
        }
        return Mathf.Max(0f, _cam.nearClipPlane + clearance - minZ);
    }

    // Same test as NearDeficit, in camera space (z = distance in front of the camera).
    public float NearDeficitLocal(Vector3 pos, Quaternion rot, float clearance)
    {
        Vector3 sc = _owner.lossyScale;
        float minZ = float.MaxValue;
        for (int i = 0; i < 8; i++)
        {
            Vector3 corner = CarriableBounds.GetBoundsCorner(_localBounds, i);
            minZ = Mathf.Min(minZ, (pos + rot * Vector3.Scale(corner, sc)).z);
        }
        return Mathf.Max(0f, _cam.nearClipPlane + clearance - minZ);
    }

    // Camera for the near-plane test: the view origin's camera, else the main camera.
    static Camera FindViewCamera(Carrier who)
    {
        Transform o = who.Origin;
        Camera c = o != null ? o.GetComponentInParent<Camera>() : null;
        if (c == null && o != null)
            c = o.GetComponentInChildren<Camera>();
        return c != null ? c : Camera.main;
    }
}
