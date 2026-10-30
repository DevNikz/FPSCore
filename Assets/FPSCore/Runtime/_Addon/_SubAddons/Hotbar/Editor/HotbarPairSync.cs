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

#if UNITY_6000_5_OR_NEWER
    static readonly Dictionary<EntityId, bool> _hadPair = new Dictionary<EntityId, bool>();
#else
    static readonly Dictionary<int, bool> _hadPair = new Dictionary<int, bool>();
#endif

    static HotbarPairSync()
    {
        ObjectChangeEvents.changesPublished += OnChanges;
        Selection.selectionChanged += CacheSelection;
        CacheSelection();
    }

    static void CacheSelection()
    {
#if UNITY_6000_5_OR_NEWER
        _hadPair.Clear();
        foreach (var go in Selection.gameObjects)
            _hadPair[go.GetEntityId()] =
                go.TryGetComponent<Hotbar>(out _) && go.TryGetComponent<HotbarUI>(out _);
#else
        _hadPair.Clear();
        foreach (var go in Selection.gameObjects)
            _hadPair[go.GetInstanceID()] =
                go.TryGetComponent<Hotbar>(out _) && go.TryGetComponent<HotbarUI>(out _);
#endif
    }

    static void OnChanges(ref ObjectChangeEventStream stream)
    {
#if UNITY_6000_5_OR_NEWER
        HashSet<EntityId> touched = null;
        for (int i = 0; i < stream.length; i++)
        {
            if (stream.GetEventType(i) != ObjectChangeKind.ChangeGameObjectStructure)
                continue;
            stream.GetChangeGameObjectStructureEvent(i, out var e);
            (touched ??= new HashSet<EntityId>()).Add(e.entityId);
        }
        if (touched == null)
            return;

        // Objects must not be edited inside this callback.
        int group = Undo.GetCurrentGroup();
        EditorApplication.delayCall += () => SyncAll(touched, group);
#else
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
#endif
    }

#if UNITY_6000_5_OR_NEWER
    static void SyncAll(HashSet<EntityId> ids, int group)
    {
        bool changed = false;
        foreach (var id in ids)
            changed |= Sync(id);
        if (changed)
            Undo.CollapseUndoOperations(group); // one Ctrl+Z undoes both
    }
#else
    static void SyncAll(HashSet<int> ids, int group)
    {
        bool changed = false;
        foreach (int id in ids)
            changed |= Sync(id);
        if (changed)
            Undo.CollapseUndoOperations(group); // one Ctrl+Z undoes both
    }
#endif

#if UNITY_6000_5_OR_NEWER
    // True if it added or removed a component.
    static bool Sync(EntityId id)
    {
        var go = EditorUtility.EntityIdToObject(id) as GameObject;
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
#else
    static bool Sync(int id)
    {
        var go = EditorUtility.EntityIdToObject(id) as GameObject;
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
#endif
}

#endif
