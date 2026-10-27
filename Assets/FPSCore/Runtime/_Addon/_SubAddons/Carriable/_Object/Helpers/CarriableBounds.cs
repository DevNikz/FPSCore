using UnityEngine;

/// <summary>Bounds and size measuring for a Carriable, in the root's local space.</summary>
public static class CarriableBounds
{
    /// <summary>Corner 'index' (0..7) of the bounds.</summary>
    public static Vector3 GetBoundsCorner(Bounds bounds, int index)
    {
        Vector3 e = bounds.extents;
        var k = new Vector3(
            (index & 1) == 0 ? -e.x : e.x,
            (index & 2) == 0 ? -e.y : e.y,
            (index & 4) == 0 ? -e.z : e.z
        );
        return bounds.center + k;
    }

    // Visual bounds in local space from mesh bounds (exact), else renderer/collider bounds.
    public static bool MeasureLocalBounds(Transform root, out Bounds b)
    {
        b = default;
        bool has = false;
        foreach (var mf in root.GetComponentsInChildren<MeshFilter>())
        {
            var r = mf.GetComponent<Renderer>();
            if (mf.sharedMesh == null || r == null || !r.enabled)
                continue;
            Grow(root, mf.transform.localToWorldMatrix, mf.sharedMesh.bounds, ref b, ref has);
        }
        if (!has)
            foreach (var r in root.GetComponentsInChildren<Renderer>())
            {
                if (r == null || !r.enabled || r is ParticleSystemRenderer)
                    continue;
                Grow(root, Matrix4x4.identity, r.bounds, ref b, ref has);
            }
        if (!has)
            foreach (var c in root.GetComponentsInChildren<Collider>())
            {
                if (c == null || !c.enabled || c.isTrigger)
                    continue;
                Grow(root, Matrix4x4.identity, c.bounds, ref b, ref has);
            }
        return has;
    }

    // Visual center in local space (renderers, else colliders).
    public static Vector3 MeasureCenter(Transform root)
    {
        Bounds b = default;
        bool has = false;
        foreach (var r in root.GetComponentsInChildren<Renderer>())
        {
            if (r == null || !r.enabled || r is ParticleSystemRenderer)
                continue;
            Grow(root, Matrix4x4.identity, r.bounds, ref b, ref has);
        }
        if (!has)
            foreach (var c in root.GetComponentsInChildren<Collider>())
            {
                if (c == null || !c.enabled || c.isTrigger)
                    continue;
                Grow(root, Matrix4x4.identity, c.bounds, ref b, ref has);
            }
        return has ? b.center : Vector3.zero;
    }

    /// <summary>Largest dimension (m) of the enabled, non-trigger colliders together.</summary>
    public static float MeasureExtent(Collider[] cols)
    {
        if (cols == null)
            return 0f;
        Bounds b = default;
        bool has = false;
        foreach (var c in cols)
        {
            if (c == null || !c.enabled || c.isTrigger)
                continue;
            if (!has)
            {
                b = c.bounds;
                has = true;
            }
            else
                b.Encapsulate(c.bounds);
        }
        return has ? Mathf.Max(b.size.x, Mathf.Max(b.size.y, b.size.z)) : 0f;
    }

    static void Grow(Transform root, Matrix4x4 m, Bounds lb, ref Bounds b, ref bool has)
    {
        for (int i = 0; i < 8; i++)
        {
            Vector3 p = root.InverseTransformPoint(m.MultiplyPoint3x4(GetBoundsCorner(lb, i)));
            if (!has)
            {
                b = new Bounds(p, Vector3.zero);
                has = true;
            }
            else
                b.Encapsulate(p);
        }
    }
}
