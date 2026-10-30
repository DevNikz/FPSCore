using UnityEngine;

/// <summary>
/// Lives with the Interactor addon. Lets Dragger use the Interactor's aim, hands-busy state and
/// obstacle probe without Dragger knowing about Interactor.
/// </summary>
[InteractorAddon("Dragger Interactor Link")]
[RequireComponent(typeof(Interactor))]
public class InteractorDragAim : MonoBehaviour, IDragAim
{
    [Tooltip(
        "Reach (m) of the fallback probe used when no Interactable is under the crosshair, so a plain Draggable can still be grabbed."
    )]
    [SerializeField]
    float fallbackReach = 3f;

    Interactor _source;

    Interactor Source
    {
        get
        {
            if (_source == null)
                _source = GetComponent<Interactor>();
            return _source;
        }
    }

    public Transform Origin => Source.Origin;
    public CharacterController Body => Source.Body;
    public bool IsHandsBusy => Source.IsHolding;

    public GameObject GetAimedObject()
    {
        var target = Source.Target; // Interactable under the crosshair
        if (target != null)
            return target.gameObject;

        // No Interactable aimed at: probe straight ahead so plain Draggables work too.
        var origin = Source.Origin;
        if (origin == null)
            return null;
        if (!Source.Cast(origin.position, origin.forward, fallbackReach, transform, out var hit))
            return null;
        return hit.rigidbody != null ? hit.rigidbody.gameObject : hit.collider.gameObject;
    }

    void OnValidate() => fallbackReach = Mathf.Max(0.1f, fallbackReach);

    public bool Cast(
        Vector3 from,
        Vector3 dir,
        float distance,
        Transform ignore,
        out RaycastHit hit
    ) => Source.Cast(from, dir, distance, ignore, out hit);
}
