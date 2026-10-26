using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Layouts;

/// <summary>
/// One rebindable keyboard/mouse binding of an action (a plain button, or one part of a composite
/// such as Move/Up). Built by <see cref="InputRemapper"/>.
/// </summary>
public readonly struct KeybindSlot
{
    public readonly InputAction Action;
    public readonly int BindingIndex;
    public readonly int SlotIndex; // position among the action's keyboard/mouse slots
    public readonly string Label;
    public readonly string BindingId; // stable across asset reordering
    public readonly bool IsLocked;
    public readonly bool IsHidden;

    public KeybindSlot(
        InputAction action,
        int bindingIndex,
        int slotIndex,
        string label,
        bool isLocked,
        bool isHidden
    )
    {
        Action = action;
        BindingIndex = bindingIndex;
        SlotIndex = slotIndex;
        Label = label;
        BindingId = action.bindings[bindingIndex].id.ToString();
        IsLocked = isLocked;
        IsHidden = isHidden;
    }
}

/// <summary>
/// Remaps keyboard/mouse bindings of the project's input asset (through <see cref="InputManager"/>).
/// Put it on the same object as InputManager (the Resources/InputManager prefab).
/// Binding order of precedence: asset  &lt;  inspector defaults (below)  &lt;  player rebinds (saved file).
/// </summary>
[RequireComponent(typeof(InputManager))]
[DefaultExecutionOrder(-90)] // after InputManager (-100) has built its actions
public class InputRemapper : MonoBehaviour
{
    public enum RebindResult
    {
        Completed,
        Swapped,
        Rejected,
        Cancelled,
    }

    [Serializable]
    public class SlotDefault
    {
        [Tooltip(
            "Which keyboard/mouse binding of the action (0 = first). Composite parts count too."
        )]
        public int slotIndex;

        [Tooltip("Designer default for this slot. Player rebinds are saved on top of it.")]
        [InputControl(layout = "Button")]
        public string path;
    }

    [Serializable]
    public class ActionEntry
    {
        public string actionName;

        [Tooltip("Menu label. Empty = action name.")]
        public string displayName;

        [Tooltip("Not shown in the keybind menu.")]
        public bool hidden;

        [Tooltip("Shown, but the player can't rebind it and other actions can't swap with it.")]
        public bool locked;

        public List<SlotDefault> defaults = new();
    }

    [Header("Designer Defaults")]
    [Tooltip(
        "Right-click this component > Populate From Asset to list every action. "
            + "Changes made here (also in Play Mode) show up in the keybind menu."
    )]
    [SerializeField]
    private List<ActionEntry> entries = new();

    [Header("Saving")]
    [Tooltip("Player rebinds, saved in Application.persistentDataPath.")]
    [SerializeField]
    private string saveFileName = "keybinds.json";

    private const string CANCEL_PATH = "<Keyboard>/escape";
    private const string HIDDEN_BY_DEFAULT_NAME = "UnlockCursor"; // matches InputManager.UNLOCK_NAME

    [Serializable]
    private class SaveItem
    {
        public string id;
        public string path;
    }

    [Serializable]
    private class SaveData
    {
        public List<SaveItem> items = new();
    }

    private InputManager _manager;
    private InputActionRebindingExtensions.RebindingOperation _operation;
    private readonly List<KeybindSlot> _allSlots = new();
    private readonly List<KeybindSlot> _visibleSlots = new();
    private readonly Dictionary<string, string> _playerOverrides = new(); // binding id -> path
    private bool _ready;
    private bool _dirty; // inspector changed in Play Mode
    private bool _wasEnabled; // the action's state before a rebind started
    private string _oldPath;
    private int _lastRebindFrame = -1;

    /// <summary>True when InputManager is on an asset and the remapper is running.</summary>
    public bool IsReady => _ready;

    public bool IsRebinding => _operation != null;

    /// <summary>
    /// True on the frame a rebind ended. UIs should ignore Esc / toggle keys that frame, because the
    /// key that ended the rebind (Esc cancels) must not also close the menu.
    /// </summary>
    public bool WasRebindFrame => Time.frameCount == _lastRebindFrame;

    /// <summary>Slots the keybind menu shows (hidden ones left out).</summary>
    public IReadOnlyList<KeybindSlot> Slots => _visibleSlots;

    /// <summary>Raised after any binding changed (rebind, reset, inspector edit). Refresh prompts here.</summary>
    public event Action Changed;

    private void Awake()
    {
        _manager = GetComponent<InputManager>();
        if (InputManager.Instance != _manager)
            return; // duplicate manager that is about to be destroyed

        if (_manager.Map == null)
        {
            Debug.LogWarning(
                "InputRemapper: InputManager has no action map. Turn on 'Use Asset' and assign the Input Actions. Remapping disabled.",
                this
            );
            enabled = false;
            return;
        }

        BuildSlots();
        LoadOverrides();
        RebuildOverrides();
        _ready = true;
    }

    private void OnValidate()
    {
        if (Application.isPlaying && _ready)
            _dirty = true;
    }

    private void Update()
    {
        if (!_dirty || IsRebinding)
            return;
        _dirty = false;
        BuildSlots();
        RebuildOverrides();
    }

    private void OnDestroy()
    {
        DisposeOperation();
    }

    /// <summary>Binding text for a slot ("E", "Left Button"...). Allocates: call on UI refresh only.</summary>
    public string GetDisplayString(KeybindSlot slot) =>
        slot.Action.GetBindingDisplayString(slot.BindingIndex);

    /// <summary>
    /// Waits for the next key or mouse button and binds it to the slot. Esc cancels.
    /// onDone gets the result and, for Swapped/Rejected, the label of the other slot.
    /// False if a rebind is already running or the slot is locked.
    /// </summary>
    public bool StartRebind(KeybindSlot slot, Action<RebindResult, string> onDone = null)
    {
        if (!_ready || IsRebinding || slot.IsLocked)
            return false;

        var action = slot.Action;
        _oldPath = action.bindings[slot.BindingIndex].effectivePath;
        _wasEnabled = action.enabled;
        action.Disable(); // an action can't be rebound while enabled

        _operation = action
            .PerformInteractiveRebinding(slot.BindingIndex)
            .WithExpectedControlType("Button")
            .WithControlsHavingToMatchPath("<Keyboard>")
            .WithControlsHavingToMatchPath("<Mouse>")
            .WithControlsExcluding("<Mouse>/position")
            .WithControlsExcluding("<Mouse>/delta")
            .WithControlsExcluding("<Mouse>/scroll")
            .WithCancelingThrough(CANCEL_PATH)
            .OnMatchWaitForAnother(0.1f)
            .OnComplete(_ => FinishRebind(slot, true, onDone))
            .OnCancel(_ => FinishRebind(slot, false, onDone))
            .Start();
        return true;
    }

    /// <summary>Stops a running rebind (onDone gets Cancelled). Safe to call when idle.</summary>
    public void CancelRebind()
    {
        _operation?.Cancel();
    }

    /// <summary>Back to the inspector default (or the asset's binding) for one slot.</summary>
    public void ResetSlot(KeybindSlot slot)
    {
        if (!_ready || IsRebinding || slot.IsLocked)
            return;
        if (!_playerOverrides.Remove(slot.BindingId))
            return;
        SaveOverrides();
        RebuildOverrides();
    }

    /// <summary>Drops every player rebind.</summary>
    public void ResetAll()
    {
        if (!_ready || IsRebinding)
            return;
        _playerOverrides.Clear();
        SaveOverrides();
        RebuildOverrides();
    }

    private void FinishRebind(KeybindSlot slot, bool completed, Action<RebindResult, string> onDone)
    {
        DisposeOperation();
        if (_wasEnabled)
            slot.Action.Enable();
        _lastRebindFrame = Time.frameCount;

        string otherLabel = null;
        var result = completed ? CommitRebind(slot, out otherLabel) : RebindResult.Cancelled;
        onDone?.Invoke(result, otherLabel);
    }

    // The rebind operation already wrote the new path to the binding. Resolve conflicts and save.
    private RebindResult CommitRebind(KeybindSlot slot, out string otherLabel)
    {
        otherLabel = null;
        string newPath = slot.Action.bindings[slot.BindingIndex].effectivePath;
        if (string.Equals(newPath, _oldPath, StringComparison.OrdinalIgnoreCase))
            return RebindResult.Completed;

        var result = RebindResult.Completed;
        if (TryFindConflict(slot, newPath, out var other))
        {
            otherLabel = other.Label;
            if (other.IsLocked || other.IsHidden)
            {
                slot.Action.ApplyBindingOverride(slot.BindingIndex, _oldPath); // undo
                return RebindResult.Rejected;
            }
            other.Action.ApplyBindingOverride(other.BindingIndex, _oldPath); // swap
            _playerOverrides[other.BindingId] = _oldPath;
            result = RebindResult.Swapped;
        }

        _playerOverrides[slot.BindingId] = newPath;
        SaveOverrides();
        Changed?.Invoke();
        return result;
    }

    private bool TryFindConflict(KeybindSlot slot, string path, out KeybindSlot other)
    {
        foreach (var s in _allSlots)
        {
            if (s.BindingId == slot.BindingId)
                continue;
            string effective = s.Action.bindings[s.BindingIndex].effectivePath;
            if (!string.Equals(effective, path, StringComparison.OrdinalIgnoreCase))
                continue;
            other = s;
            return true;
        }
        other = default;
        return false;
    }

    /// <summary>Clears every override, then applies inspector defaults, then player rebinds.</summary>
    private void RebuildOverrides()
    {
        _manager.Map.RemoveAllBindingOverrides();

        foreach (var entry in entries)
        {
            if (entry == null || entry.defaults == null || string.IsNullOrEmpty(entry.actionName))
                continue;
            foreach (var d in entry.defaults)
            {
                if (d == null || string.IsNullOrEmpty(d.path))
                    continue;
                if (TryFindSlot(entry.actionName, d.slotIndex, out var slot))
                    slot.Action.ApplyBindingOverride(slot.BindingIndex, d.path);
                else
                    Debug.LogWarning(
                        $"InputRemapper: no keyboard/mouse slot {d.slotIndex} on action '{entry.actionName}'.",
                        this
                    );
            }
        }

        foreach (var slot in _allSlots)
            if (_playerOverrides.TryGetValue(slot.BindingId, out var path))
                slot.Action.ApplyBindingOverride(slot.BindingIndex, path);

        Changed?.Invoke();
    }

    private bool TryFindSlot(string actionName, int slotIndex, out KeybindSlot found)
    {
        foreach (var s in _allSlots)
        {
            if (s.SlotIndex != slotIndex || s.Action.name != actionName)
                continue;
            found = s;
            return true;
        }
        found = default;
        return false;
    }

    private void BuildSlots()
    {
        _allSlots.Clear();
        _visibleSlots.Clear();

        var byName = new Dictionary<string, ActionEntry>();
        foreach (var e in entries)
            if (e != null && !string.IsNullOrEmpty(e.actionName))
                byName[e.actionName] = e;

        foreach (var action in _manager.Map.actions)
        {
            byName.TryGetValue(action.name, out var entry);
            AddSlots(action, entry, _allSlots);
        }

        foreach (var s in _allSlots)
            if (!s.IsHidden)
                _visibleSlots.Add(s);
    }

    // Keyboard/mouse bindings of an action that a player can rebind. Skips composites themselves,
    // gamepad bindings, and Vector2/Axis bindings that aren't composite parts (e.g. Look = mouse delta).
    private static void AddSlots(InputAction action, ActionEntry entry, List<KeybindSlot> into)
    {
        string baseName = string.IsNullOrEmpty(entry?.displayName)
            ? action.name
            : entry.displayName;
        bool isLocked = entry != null && entry.locked;
        bool isHidden = entry != null && entry.hidden;
        bool isAnalog = action.expectedControlType is "Vector2" or "Axis";
        var bindings = action.bindings;

        int standalone = 0; // standalone slots decide whether labels get a (1), (2) suffix
        for (int i = 0; i < bindings.Count; i++)
            if (IsSlotBinding(bindings[i], isAnalog) && !bindings[i].isPartOfComposite)
                standalone++;

        int slotIndex = 0;
        int standaloneSeen = 0;
        for (int i = 0; i < bindings.Count; i++)
        {
            var b = bindings[i];
            if (!IsSlotBinding(b, isAnalog))
                continue;

            string label = baseName;
            if (b.isPartOfComposite)
            {
                string part = string.IsNullOrEmpty(b.name)
                    ? "Part"
                    : char.ToUpper(b.name[0]) + b.name.Substring(1);
                label = $"{baseName} - {part}";
            }
            else if (standalone > 1)
                label = $"{baseName} ({++standaloneSeen})";

            into.Add(new KeybindSlot(action, i, slotIndex++, label, isLocked, isHidden));
        }
    }

    private static bool IsSlotBinding(InputBinding b, bool isAnalog)
    {
        if (b.isComposite || string.IsNullOrEmpty(b.path))
            return false;
        if (isAnalog && !b.isPartOfComposite)
            return false;
        return b.path.StartsWith("<Keyboard>") || b.path.StartsWith("<Mouse>");
    }

    private void DisposeOperation()
    {
        _operation?.Dispose();
        _operation = null;
    }

    private string SavePath => Path.Combine(Application.persistentDataPath, saveFileName);

    private void SaveOverrides()
    {
        string path = SavePath;
        try
        {
            var data = new SaveData();
            foreach (var pair in _playerOverrides)
                data.items.Add(new SaveItem { id = pair.Key, path = pair.Value });
            File.WriteAllText(path, JsonUtility.ToJson(data, true));
        }
        catch (Exception e)
        {
            Debug.LogWarning($"InputRemapper: could not write '{path}' ({e.Message}).", this);
        }
    }

    private void LoadOverrides()
    {
        _playerOverrides.Clear();
        string path = SavePath;
        try
        {
            if (!File.Exists(path))
                return;
            var data = JsonUtility.FromJson<SaveData>(File.ReadAllText(path));
            if (data?.items == null)
                return;
            foreach (var item in data.items)
                if (!string.IsNullOrEmpty(item.id) && !string.IsNullOrEmpty(item.path))
                    _playerOverrides[item.id] = item.path;
        }
        catch (Exception e)
        {
            Debug.LogWarning($"InputRemapper: could not read '{path}' ({e.Message}).", this);
        }
    }

    /// <summary>
    /// Fills the Designer Defaults list from the input asset: one entry per action that has
    /// keyboard/mouse bindings, set to the asset's current paths. Existing entries are kept.
    /// </summary>
    [ContextMenu("Populate From Asset")]
    private void PopulateFromAsset()
    {
        var manager = GetComponent<InputManager>();
        var map =
            manager != null && manager.InputAsset != null
                ? manager.InputAsset.FindActionMap(manager.ActionMapName, false)
                : null;
        if (map == null)
        {
            Debug.LogWarning(
                "InputRemapper: assign the Input Asset and a valid map name on InputManager first.",
                this
            );
            return;
        }

#if UNITY_EDITOR
        UnityEditor.Undo.RecordObject(this, "Populate keybinds");
#endif
        var slots = new List<KeybindSlot>();
        foreach (var action in map.actions)
        {
            slots.Clear();
            AddSlots(action, null, slots);
            if (slots.Count == 0)
                continue;

            var entry = entries.Find(e => e != null && e.actionName == action.name);
            if (entry == null)
            {
                entry = new ActionEntry
                {
                    actionName = action.name,
                    displayName = action.name,
                    hidden = action.name == HIDDEN_BY_DEFAULT_NAME,
                };
                entries.Add(entry);
            }

            foreach (var slot in slots)
                if (!entry.defaults.Exists(d => d.slotIndex == slot.SlotIndex))
                    entry.defaults.Add(
                        new SlotDefault
                        {
                            slotIndex = slot.SlotIndex,
                            path = action.bindings[slot.BindingIndex].path,
                        }
                    );
        }
#if UNITY_EDITOR
        UnityEditor.EditorUtility.SetDirty(this);
#endif
    }
}
