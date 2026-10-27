using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Default ICarriableTicker, created on the first carry or claim.
/// Set Carriable.Ticker before that (e.g. in a BeforeSceneLoad hook) to use another.
/// </summary>
public sealed class CarriableTicker : MonoBehaviour, ICarriableTicker
{
    private readonly List<Carriable> _active = new();

    internal static CarriableTicker Create()
    {
        var go = new GameObject("CarriableTicker");
        DontDestroyOnLoad(go);
        return go.AddComponent<CarriableTicker>();
    }

    public void Add(Carriable carriable)
    {
        if ((object)carriable == null || carriable.TickIndex >= 0)
            return;
        carriable.TickIndex = _active.Count;
        _active.Add(carriable);
    }

    public void Remove(Carriable carriable)
    {
        if ((object)carriable == null || carriable.TickIndex < 0)
            return;
        int i = carriable.TickIndex;
        carriable.TickIndex = -1;
        if (i < _active.Count)
            RemoveAtSwap(i);
    }

    void RemoveAtSwap(int i)
    {
        int last = _active.Count - 1;
        if (i != last)
        {
            var moved = _active[last];
            _active[i] = moved;
            moved.TickIndex = i;
        }
        _active.RemoveAt(last);
    }

    void OnDestroy()
    {
        if (ReferenceEquals(Carriable.Ticker, this))
            Carriable.Ticker = null;
    }

    // Backwards: a carriable that drops itself mid-tick swaps in one that already ran.
    void FixedUpdate()
    {
        for (int i = _active.Count - 1; i >= 0; i--)
        {
            if (i >= _active.Count)
                continue;
            var c = _active[i];
            if (c == null)
                RemoveAtSwap(i);
            else
                c.TickFixed();
        }
    }

    void LateUpdate()
    {
        for (int i = _active.Count - 1; i >= 0; i--)
        {
            if (i >= _active.Count)
                continue;
            var c = _active[i];
            if (c == null)
                RemoveAtSwap(i);
            else
                c.TickLate();
        }
    }
}
