using System;
using UnityEngine;

/// <summary>How mass and size change the way the dragged object follows the hold point.</summary>
[Serializable]
public class DragFeel
{
    [Tooltip("Effective load (kg) = mass x (1 + this x object size in m).")]
    [SerializeField]
    float sizeWeight = 0.5f;

    [Tooltip("Effective load (kg) where the feel is halfway between light and heavy.")]
    [SerializeField]
    float loadRef = 10f;

    [Tooltip("Effective load (kg) above this can't be lifted, only dragged on the ground.")]
    [SerializeField]
    float maxLiftLoad = 40f;

    [Tooltip("Position follow strength.")]
    [SerializeField]
    float followGain = 10f;

    [Tooltip("Max speed, lightest objects (m/s).")]
    [SerializeField]
    float lightSpeed = 12f;

    [Tooltip("Max speed, very heavy objects (m/s).")]
    [SerializeField]
    float heavySpeed = 1f;

    [Tooltip("Speed-up rate, lightest objects (m/s^2).")]
    [SerializeField]
    float lightAccel = 150f;

    [Tooltip("Speed-up rate, very heavy objects (m/s^2). Low = slow to start and stop.")]
    [SerializeField]
    float heavyAccel = 6f;

    public float ComputeLoad(float mass, float extent) => mass * (1f + sizeWeight * extent);

    /// <summary>0 = light .. 1 = very heavy.</summary>
    public float GetHeaviness(float load) => load / (load + Mathf.Max(loadRef, 0.01f));

    public bool CanLift(float load) => load <= maxLiftLoad;

    /// <summary>
    /// One physics step of following the hold point. Lifted objects follow in 3D;
    /// grounded ones keep their own vertical speed so gravity handles it.
    /// </summary>
    public Vector3 StepVelocity(
        Vector3 current,
        Vector3 offsetToHoldPoint,
        bool isLifted,
        float heavy,
        float deltaTime
    )
    {
        float speed = Mathf.Lerp(lightSpeed, heavySpeed, heavy);
        float accel = Mathf.Lerp(lightAccel, heavyAccel, heavy);

        Vector3 want = Vector3.ClampMagnitude(offsetToHoldPoint * followGain, speed);
        if (!isLifted)
            want.y = current.y;
        return Vector3.MoveTowards(current, want, accel * deltaTime);
    }

    public void Validate()
    {
        sizeWeight = Mathf.Max(0f, sizeWeight);
        loadRef = Mathf.Max(0.01f, loadRef);
        maxLiftLoad = Mathf.Max(0f, maxLiftLoad);
        followGain = Mathf.Max(0f, followGain);
        lightSpeed = Mathf.Max(0f, lightSpeed);
        heavySpeed = Mathf.Clamp(heavySpeed, 0f, lightSpeed);
        lightAccel = Mathf.Max(0f, lightAccel);
        heavyAccel = Mathf.Clamp(heavyAccel, 0f, lightAccel);
    }
}
