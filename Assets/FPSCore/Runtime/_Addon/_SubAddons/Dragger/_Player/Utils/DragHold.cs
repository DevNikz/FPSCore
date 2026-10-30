using System;
using UnityEngine;

/// <summary>Where the dragged object is held, when it breaks free, and how it is released.</summary>
[Serializable]
public class DragHold
{
    [Tooltip("Closest carry distance from the view (m).")]
    [SerializeField]
    float minHoldDistance = 1f;

    [Tooltip("Farthest carry distance (m). Far objects get pulled in to this.")]
    [SerializeField]
    float maxHoldDistance = 2.2f;

    [Tooltip("Gap kept from walls while lifted (m).")]
    [SerializeField]
    float wallClearance = 0.3f;

    [Tooltip("Let go if the object lags this far behind (m).")]
    [SerializeField]
    float breakDistance = 2f;

    [Tooltip("Speed cap on release (m/s).")]
    [SerializeField]
    float releaseSpeedCap = 3f;

    /// <summary>Keeps the grab distance (view-space z) inside the carry range.</summary>
    public float ClampCarryDistance(float z) => Mathf.Clamp(z, minHoldDistance, maxHoldDistance);

    public bool IsBeyondBreak(Vector3 offsetToHoldPoint) =>
        offsetToHoldPoint.magnitude > breakDistance;

    public Vector3 ClampReleaseVelocity(Vector3 velocity) =>
        Vector3.ClampMagnitude(velocity, releaseSpeedCap);

    /// <summary>
    /// Carry point in front of the view, pulled in when a wall is ahead (lifted only).
    /// </summary>
    public Vector3 GetHoldPoint(
        IDragAim aim,
        Transform origin,
        Vector3 localPoint,
        bool isLifted,
        IDragTarget target
    )
    {
        Vector3 point = origin.TransformPoint(localPoint);
        if (!isLifted)
            return point;

        Vector3 from = origin.position;
        Vector3 dir = point - from;
        float d = dir.magnitude;
        if (d < 1e-4f)
            return point;

        dir /= d;
        float clear = wallClearance + target.Extent * 0.5f;
        if (aim.Cast(from, dir, d + clear, target.Root, out var wall))
            d = Mathf.Clamp(wall.distance - clear, Mathf.Min(minHoldDistance, d * 0.5f), d);
        return from + dir * d;
    }

    public void Validate()
    {
        minHoldDistance = Mathf.Max(0.1f, minHoldDistance);
        maxHoldDistance = Mathf.Max(minHoldDistance, maxHoldDistance);
        wallClearance = Mathf.Max(0f, wallClearance);
        breakDistance = Mathf.Max(0.1f, breakDistance);
        releaseSpeedCap = Mathf.Max(0f, releaseSpeedCap);
    }
}
