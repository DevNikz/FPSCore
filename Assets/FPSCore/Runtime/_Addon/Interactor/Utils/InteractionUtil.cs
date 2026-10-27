using UnityEngine;

/// <summary>Where a carried object sits in view space.</summary>
[System.Serializable]
public class HoldPose
{
    public Vector3 offset;

    [Tooltip("Extra forward = object radius x this.")]
    [Range(0f, 1f)]
    public float radiusPush = 0.5f;

    [Tooltip("Extra drop = object radius x this.")]
    [Range(0f, 1f)]
    public float radiusDrop;

    public HoldPose() { }

    public HoldPose(Vector3 offset, float radiusPush, float radiusDrop)
    {
        this.offset = offset;
        this.radiusPush = radiusPush;
        this.radiusDrop = radiusDrop;
    }
}
