#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Keeps Hotbar and HotbarUI together on a GameObject: adding one adds the other, removing one
/// removes the other. Editor-only (#if), so it travels with the Hotbar addon.
/// Not a [RequireComponent] pair on purpose: that would block removing either component.
/// </summary>
[InitializeOnLoad]
static class HotbarPairSync
{
    // Whether a GameObject had both components the last time we looked. Needed to tell an
    // added component (add the partner) from a removed one (remove the partner).
    // Refreshed for the current selection: components are added/removed on selected objects.
    static readonly Dictionary<int, bool> _hadPair = new Dictionary<int, bool>();

    static HotbarPairSync()
    {
        ObjectChangeEvents.changesPublished += OnChanges;
        Selection.selectionChanged += CacheSelection;
        CacheSelection();
    }

    static void CacheSelection()
    {
        _hadPair.Clear();
        foreach (var go in Selection.gameObjects)
            _hadPair[go.GetInstanceID()] =
                go.TryGetComponent<Hotbar>(out _) && go.TryGetComponent<HotbarUI>(out _);
    }

    static void OnChanges(ref ObjectChangeEventStream stream)
    {
        HashSet<int> touched = null;
        for (int i = 0; i < stream.length; i++)
        {
            if (stream.GetEventType(i) != ObjectChangeKind.ChangeGameObjectStructure)
                continue;
            stream.GetChangeGameObjectStructureEvent(i, out var e);
            (touched ??= new HashSet<int>()).Add(e.instanceId);
        }
        if (touched == null)
            return;

        // Objects must not be edited inside this callback.
        int group = Undo.GetCurrentGroup();
        EditorApplication.delayCall += () => SyncAll(touched, group);
    }

    static void SyncAll(HashSet<int> ids, int group)
    {
        bool changed = false;
        foreach (int id in ids)
            changed |= Sync(id);
        if (changed)
            Undo.CollapseUndoOperations(group); // one Ctrl+Z undoes both
    }

    // True if it added or removed a component.
    static bool Sync(int id)
    {
        var go = EditorUtility.InstanceIDToObject(id) as GameObject;
        if (go == null)
        {
            _hadPair.Remove(id);
            return false;
        }

        bool hasHotbar = go.TryGetComponent<Hotbar>(out var hotbar);
        bool hasUI = go.TryGetComponent<HotbarUI>(out var ui);
        if (hasHotbar == hasUI) // both or neither: nothing to fix
        {
            _hadPair[id] = hasHotbar;
            return false;
        }

        if (_hadPair.TryGetValue(id, out bool wasPaired) && wasPaired)
        {
            // One of them was removed: remove the other too.
            Undo.DestroyObjectImmediate(hasHotbar ? (Component)hotbar : ui);
            _hadPair[id] = false;
        }
        else
        {
            // One of them was added: add the other.
            if (hasHotbar)
                Undo.AddComponent<HotbarUI>(go);
            else
                Undo.AddComponent<Hotbar>(go);
            _hadPair[id] = true;
        }
        return true;
    }
}
#endif
