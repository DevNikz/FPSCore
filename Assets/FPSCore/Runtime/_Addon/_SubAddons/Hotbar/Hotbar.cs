using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Hotbar for a Carrier. Each slot is a stack of Carriables with the same HotbarItemDefinition.
/// Carriable never references this: pickups with full hands arrive through ICarrierStorage,
/// everything else through the Carrier's Grabbed/Released events.
/// </summary>
[FPSAddon("Hotbar")]
[RequireComponent(typeof(Carrier))]
[DisallowMultipleComponent]
public class Hotbar : MonoBehaviour, ICarrierStorage
{
    [Tooltip("Slot count (keys 1..N). Applied at start; change before Play.")]
    [SerializeField, Range(1, IInput.HOTBAR_KEYS)]
    int size = 5;

    // One slot. The last item is the one in the hands while the slot is equipped.
    class SlotStack
    {
        public HotbarItemDefinition Def;
        public readonly List<Carriable> Items = new List<Carriable>();
        public Carriable Top => Items.Count > 0 ? Items[Items.Count - 1] : null;
    }

    Carrier _carrier;
    SlotStack[] _slots; // null entry = free slot
    int _current;
    bool _busy; // ignore Carrier events caused by our own stow/equip
    bool _equipPending; // equip the next item of the current stack on the next Update

    /// <summary>Raised when any slot, count or the equipped slot changes.</summary>
    public event Action Changed;

    public int Size => _slots != null ? _slots.Length : size;
    public int Current => _current;

    public HotbarItemDefinition GetDefinition(int i) =>
        IsValid(i) && _slots[i] != null ? _slots[i].Def : null;

    public int GetCount(int i) => IsValid(i) && _slots[i] != null ? _slots[i].Items.Count : 0;

    public bool IsFree(int i) => GetCount(i) == 0;

    void Awake()
    {
        _carrier = GetComponent<Carrier>();
        _slots = new SlotStack[Mathf.Clamp(size, 1, IInput.HOTBAR_KEYS)]; // all free
        if (_carrier == null)
        {
            Debug.LogError($"{name}: Carrier missing.", this);
            enabled = false;
        }
    }

    void OnEnable()
    {
        if (_carrier == null)
            return;
        _carrier.Grabbed += HandleGrabbed;
        _carrier.Released += HandleReleased;
        _carrier.RegisterStorage(this);
    }

    void OnDisable()
    {
        if (_carrier == null)
            return;
        _carrier.Grabbed -= HandleGrabbed;
        _carrier.Released -= HandleReleased;
        _carrier.UnregisterStorage(this);
    }

    void Update()
    {
        if (Purge())
            Changed?.Invoke();
        TryEquipPending();

        int i = IInput.HotbarPressedIndex;
        if (i >= 0)
            Select(i);
    }

    /// <summary>Equip slot i. False if invalid, nothing to equip, or a non-hotbar item is held.</summary>
    public bool Select(int i)
    {
        if (!IsValid(i) || _carrier == null || !_carrier.isActiveAndEnabled)
            return false;

        var held = _carrier.Held;
        if (i == _current)
        {
            // Only re-equips a stowed stack (hands empty); otherwise it is already equipped.
            if (held != null || IsFree(i))
                return false;
            EquipGuarded(i);
            return true;
        }

        if (held != null && IndexOf(held) < 0) // a non-hotbar item is in the hands
            return false;

        _busy = true;
        try
        {
            StowTop(_current);
            _current = i;
            Equip(i);
        }
        finally
        {
            _busy = false;
        }
        _equipPending = false;
        Changed?.Invoke();
        return true;
    }

    /// <summary>Carrier hook: hands are full, so stack the item or put it in a free slot.</summary>
    public bool TryStore(Carrier from, Carriable p)
    {
        if (_busy || from != _carrier || p == null || p.IsHeld)
            return false;

        var def = DefinitionOf(p);
        if (def == null) // not a hotbar item
            return false;

        var stack = FindStackWithRoom(def);
        if (stack == null)
        {
            int free = FindFreeSlot();
            if (free < 0)
                return false;
            stack = _slots[free] = new SlotStack { Def = def };
        }

        stack.Items.Insert(0, p); // below the item in the hands
        p.Stow();
        Changed?.Invoke();
        return true;
    }

    bool IsValid(int i) => _slots != null && i >= 0 && i < _slots.Length;

    static HotbarItemDefinition DefinitionOf(Carriable p) =>
        p.TryGetComponent<HotbarItem>(out var item) ? item.Definition : null;

    // Slot holding p, or -1.
    int IndexOf(Carriable p)
    {
        for (int i = 0; i < _slots.Length; i++)
            if (_slots[i] != null && _slots[i].Items.Contains(p))
                return i;
        return -1;
    }

    // Same-definition stack that is not full. The current slot is tried first.
    SlotStack FindStackWithRoom(HotbarItemDefinition def)
    {
        for (int n = 0; n < _slots.Length; n++)
        {
            var s = _slots[(_current + n) % _slots.Length];
            if (s != null && s.Def == def && s.Items.Count < def.MaxStack)
                return s;
        }
        return null;
    }

    // First free slot after the current one (wraps). The current slot is skipped: stored
    // items must never sit in the equipped slot while the hands hold something else.
    int FindFreeSlot()
    {
        for (int n = 1; n < _slots.Length; n++)
        {
            int i = (_current + n) % _slots.Length;
            if (_slots[i] == null)
                return i;
        }
        return -1;
    }

    // Removes p from its stack and frees the slot if it was the last item. Returns the slot, or -1.
    int RemoveFromStack(Carriable p)
    {
        int i = IndexOf(p);
        if (i < 0)
            return -1;
        _slots[i].Items.Remove(p);
        if (_slots[i].Items.Count == 0)
            _slots[i] = null;
        return i;
    }

    // Hide the held item but keep it in its stack.
    void StowTop(int i)
    {
        var p = _slots[i]?.Top;
        if (p != null)
            p.Stow();
    }

    // Bring the stack's top item into the hands. Callers set _busy.
    void Equip(int i)
    {
        var p = _slots[i]?.Top;
        if (p == null)
            return;

        p.transform.position = _carrier.GetHoldPoint(p);
        p.gameObject.SetActive(true);
        if (_carrier.TryGrab(p))
            return;

        RemoveFromStack(p); // rejected: leave it in the world
        if (!IsFree(i))
            _equipPending = true; // try the next one
    }

    void EquipGuarded(int i)
    {
        _busy = true;
        try
        {
            Equip(i);
        }
        finally
        {
            _busy = false;
        }
        Changed?.Invoke();
    }

    // After a drop/throw: the next item of the stack comes into the hands.
    void TryEquipPending()
    {
        if (!_equipPending)
            return;
        _equipPending = false;
        if (_carrier == null || !_carrier.isActiveAndEnabled)
            return;
        if (_carrier.Held != null || IsFree(_current))
            return;
        EquipGuarded(_current);
    }

    void HandleGrabbed(Carriable p)
    {
        if (_busy || p == null)
            return;
        var def = DefinitionOf(p);
        if (def == null) // not a hotbar item (currency etc.)
            return;

        var stack = _slots[_current];
        if (stack == null)
            stack = _slots[_current] = new SlotStack { Def = def };
        else if (stack.Def != def || stack.Items.Count >= def.MaxStack)
        {
            p.Drop(); // slot taken (should not happen): refuse the pickup
            return;
        }

        stack.Items.Add(p); // on top: it is the one in the hands
        Changed?.Invoke();
    }

    void HandleReleased(Carriable p)
    {
        if (_busy || p == null)
            return;
        int i = RemoveFromStack(p); // dropped/thrown = out of the stack
        if (i < 0)
            return;
        if (i == _current && !IsFree(i))
            _equipPending = true;
        Changed?.Invoke();
    }

    // Drops destroyed items, and the current slot's top item if it is out in the world
    // (released while we were not listening). A stowed (inactive) top item stays.
    bool Purge()
    {
        bool changed = false;
        for (int i = 0; i < _slots.Length; i++)
        {
            var s = _slots[i];
            if (s == null)
                continue;

            // Unity null: reference set but object destroyed.
            if (s.Items.RemoveAll(p => p == null) > 0)
                changed = true;

            var top = s.Top;
            if (i == _current && top != null && top.gameObject.activeSelf && !top.IsHeld)
            {
                s.Items.Remove(top);
                changed = true;
                if (s.Items.Count > 0)
                    _equipPending = true;
            }

            if (s.Items.Count == 0)
                _slots[i] = null;
        }
        return changed;
    }
}
