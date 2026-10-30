using UnityEngine;

/// <summary>
/// Lives with the Carrier addon. Lets Dragger claim a Carriable through the existing claim
/// handshake without Dragger knowing about Carriable. Don't put this next to Draggable.
/// </summary>
[CarriableAddon("Dragger Carriable Target")]
[RequireComponent(typeof(Carriable))]
public class CarriableDragTarget : MonoBehaviour, IDragTarget
{
    Carriable _source;
    ClaimProxy _proxy;

    Carriable Source
    {
        get
        {
            if (_source == null)
                _source = GetComponent<Carriable>();
            return _source;
        }
    }

    public Rigidbody Body => Source.Body;
    public Transform Root => transform;
    public float Extent => Source.Extent;
    public bool IsClaimed => Source.IsClaimed;

    public bool TryClaim(IDragClaimant claimant)
    {
        if (claimant == null || Source.IsHeld)
            return false;

        _proxy = new ClaimProxy(this, claimant);
        if (Source.TryClaim(_proxy))
            return true;

        _proxy = null;
        return false;
    }

    public void ReleaseClaim() => Source.ReleaseClaim();

    void NotifyClaimEnded(IDragClaimant claimant)
    {
        _proxy = null;
        claimant.OnClaimEnded(this);
    }

    // Carriable only knows ICarriableClaimant; this forwards its callback to the Dragger.
    sealed class ClaimProxy : ICarriableClaimant
    {
        readonly CarriableDragTarget _owner;
        readonly IDragClaimant _claimant;

        public ClaimProxy(CarriableDragTarget owner, IDragClaimant claimant)
        {
            _owner = owner;
            _claimant = claimant;
        }

        public CharacterController Body => throw new System.NotImplementedException();

        public void Clear(Carriable p) => _owner.NotifyClaimEnded(_claimant);
    }
}
