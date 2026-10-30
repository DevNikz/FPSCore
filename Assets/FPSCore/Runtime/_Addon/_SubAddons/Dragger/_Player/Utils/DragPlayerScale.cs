using System;
using UnityEngine;

/// <summary>How much dragging slows the player's movement and mouse look.</summary>
[Serializable]
public class DragPlayerScale
{
    [Tooltip("Player move speed multiplier at max load. Read from MoveSpeedScale.")]
    [SerializeField, Range(0.1f, 1f)]
    float heavyMoveScale = 0.6f;

    [Tooltip("Mouse look multiplier at max load. Lower = heavier feel.")]
    [SerializeField, Range(0.05f, 1f)]
    float heavyLookScale = 0.35f;

    [Tooltip("How fast look sensitivity blends in and out (1/s).")]
    [SerializeField]
    float lookBlendSpeed = 8f;

    float _look = 1f; // current look multiplier

    /// <summary>Multiply your player move speed by this (1 when not dragging).</summary>
    public float GetMoveScale(bool isDragging, float heavy) =>
        isDragging ? Mathf.Lerp(1f, heavyMoveScale, heavy) : 1f;

    /// <summary>Blends the look multiplier toward its goal so grab/release doesn't pop.</summary>
    public float TickLook(bool isDragging, float heavy, float unscaledDeltaTime)
    {
        float goal = isDragging ? Mathf.Lerp(1f, heavyLookScale, heavy) : 1f;
        _look = Mathf.Lerp(_look, goal, 1f - Mathf.Exp(-lookBlendSpeed * unscaledDeltaTime));
        return _look;
    }

    public void ResetLook() => _look = 1f;

    public void Validate() => lookBlendSpeed = Mathf.Max(0.1f, lookBlendSpeed);
}
