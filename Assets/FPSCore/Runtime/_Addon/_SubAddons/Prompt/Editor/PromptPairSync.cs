#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Keeps PromptPublisher and PromptUI together on a GameObject: adding one adds the other, removing one
/// removes the other. Editor-only (#if), so it travels with the PromptPublisher addon.
/// Not a [RequireComponent] pair on purpose: that would block removing either component.
/// </summary>
[InitializeOnLoad]
static class PromptPairSync
{
    // Whether a GameObject had both components the last time we looked. Needed to tell an
    // added component (add the partner) from a removed one (remove the partner).
    // Refreshed for the current selection: components are added/removed on selected objects.
    static readonly Dictionary<int, bool> _hadPair = new Dictionary<int, bool>();

    static PromptPairSync()
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
                go.TryGetComponent<PromptPublisher>(out _) && go.TryGetComponent<PromptUI>(out _);
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
        var go = EditorUtility.EntityIdToObject(id) as GameObject;
        if (go == null)
        {
            _hadPair.Remove(id);
            return false;
        }

        bool hasPrompt = go.TryGetComponent<PromptPublisher>(out var PromptPublisher);
        bool hasUI = go.TryGetComponent<PromptUI>(out var ui);
        if (hasPrompt == hasUI) // both or neither: nothing to fix
        {
            _hadPair[id] = hasPrompt;
            return false;
        }

        if (_hadPair.TryGetValue(id, out bool wasPaired) && wasPaired)
        {
            // One of them was removed: remove the other too.
            Undo.DestroyObjectImmediate(hasPrompt ? (Component)PromptPublisher : ui);
            _hadPair[id] = false;
        }
        else
        {
            // One of them was added: add the other.
            if (hasPrompt)
                Undo.AddComponent<PromptUI>(go);
            else
                Undo.AddComponent<PromptPublisher>(go);
            _hadPair[id] = true;
        }
        return true;
    }
}
#endif
