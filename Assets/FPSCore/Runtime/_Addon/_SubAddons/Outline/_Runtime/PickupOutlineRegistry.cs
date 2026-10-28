using System.Collections.Generic;
using System.Runtime.CompilerServices;
using UnityEngine;

/// <summary>
/// Outline bookkeeping. Fed by static events (Interactable/Carriable Enabled/Disabled,
/// PlacementPreviewBus), so the base package and other addons never reference this.
/// </summary>
public static class PickupOutlineRegistry
{
    public sealed class Entry
    {
        public Interactable Owner;
        public Carriable SubOwner;
        public Renderer[] Renderers;
        public int[] SubMeshCounts;
        public int Highlight;
        internal int Index = -1; // slot in s_entries, -1 = not registered
        internal int HighlightIndex = -1; // slot in s_highlighted, -1 = not highlighted
    }

    public struct Ghost
    {
        public Mesh Mesh;
        public Matrix4x4 Matrix;
        public int SubMeshCount;
        public PreviewValidity Style;
    }

    private static readonly List<Ghost> s_ghosts = new();
    private static int s_ghostFrame = -1;

    private static readonly List<Entry> s_entries = new();
    private static readonly List<Entry> s_highlighted = new();
    private static readonly Dictionary<Carriable, Entry> s_pickuplookup = new();
    private static readonly Dictionary<Interactable, Entry> s_interactlookup = new();

    // Built once per object, reused on re-enable (pools). Weak: never keeps a destroyed object alive.
    private static readonly ConditionalWeakTable<Component, Entry> s_cache = new();

    public static IReadOnlyList<Entry> Entries => s_entries;

    /// <summary>Only entries with Highlight &gt; 0. Order is not stable.</summary>
    public static IReadOnlyList<Entry> HighlightedEntries => s_highlighted;
    public static int Count => s_entries.Count;

    public static IReadOnlyList<Ghost> Ghosts => s_ghosts;

    /// <summary>Ghosts submitted this frame (stale ones are ignored).</summary>
    public static int GhostCount => s_ghostFrame == Time.frameCount ? s_ghosts.Count : 0;

    public static int HighlightedCount => s_highlightedTotal;
    static int s_highlightedTotal;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStatics()
    {
        s_entries.Clear();
        s_highlighted.Clear();
        s_pickuplookup.Clear();
        s_interactlookup.Clear();
        s_highlightedTotal = 0;
        s_ghosts.Clear();
        s_ghostFrame = -1;
    }

    // Runs before any scene object's OnEnable. Unsubscribe first: safe with domain reload off.
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    static void Bootstrap()
    {
        Interactable.Enabled -= Register;
        Interactable.Disabled -= Unregister;
        Carriable.Enabled -= Register;
        Carriable.Disabled -= Unregister;
        PlacementPreviewBus.PreviewRequested -= DrawGhost;

        Interactable.Enabled += Register;
        Interactable.Disabled += Unregister;
        Carriable.Enabled += Register;
        Carriable.Disabled += Unregister;
        PlacementPreviewBus.PreviewRequested += DrawGhost;
    }

    static void DrawGhost(
        Mesh mesh,
        Matrix4x4 localToWorld,
        int subMeshCount,
        PreviewValidity style
    )
    {
        if (mesh == null)
            return;
        if (s_ghostFrame != Time.frameCount)
        {
            s_ghosts.Clear();
            s_ghostFrame = Time.frameCount;
        }
        s_ghosts.Add(
            new Ghost
            {
                Mesh = mesh,
                Matrix = localToWorld,
                SubMeshCount = Mathf.Max(1, subMeshCount),
                Style = style,
            }
        );
    }

    public static void Highlight(Interactable interact)
    {
        if (interact != null && s_interactlookup.TryGetValue(interact, out var e))
            AddHighlight(e);
    }

    public static void Highlight(Carriable pickup)
    {
        if (pickup != null && s_pickuplookup.TryGetValue(pickup, out var e))
            AddHighlight(e);
    }

    public static void Unhighlight(Interactable interact)
    {
        if (interact != null && s_interactlookup.TryGetValue(interact, out var e))
            RemoveHighlight(e);
    }

    public static void Unhighlight(Carriable pickup)
    {
        if (pickup != null && s_pickuplookup.TryGetValue(pickup, out var e))
            RemoveHighlight(e);
    }

    public static void Register(Interactable interact)
    {
        if (interact == null || s_interactlookup.ContainsKey(interact))
            return;
        var e = GetOrBuildEntry(interact);
        s_interactlookup.Add(interact, e);
        AddEntry(e);
    }

    public static void Register(Carriable pickup)
    {
        if (pickup == null || s_pickuplookup.ContainsKey(pickup))
            return;
        var e = GetOrBuildEntry(pickup);
        s_pickuplookup.Add(pickup, e);
        AddEntry(e);
    }

    public static void Unregister(Interactable interact)
    {
        if (interact == null || !s_interactlookup.TryGetValue(interact, out var e))
            return;
        s_interactlookup.Remove(interact);
        DropEntry(e);
    }

    public static void Unregister(Carriable pickup)
    {
        if (pickup == null || !s_pickuplookup.TryGetValue(pickup, out var e))
            return;
        s_pickuplookup.Remove(pickup);
        DropEntry(e);
    }

    /// <summary>Call if renderers are added/removed under a carriable at runtime.</summary>
    public static void Refresh(Carriable pickup)
    {
        if (pickup == null || !s_pickuplookup.TryGetValue(pickup, out var e))
            return;
        CollectRenderers(pickup, out e.Renderers, out e.SubMeshCounts);
    }

    static void AddHighlight(Entry e)
    {
        if (e.Highlight++ == 0)
        {
            e.HighlightIndex = s_highlighted.Count;
            s_highlighted.Add(e);
        }
        s_highlightedTotal++;
    }

    static void RemoveHighlight(Entry e)
    {
        if (e.Highlight <= 0)
            return;
        e.Highlight--;
        s_highlightedTotal--;
        if (e.Highlight == 0)
            RemoveHighlighted(e);
    }

    static void AddEntry(Entry e)
    {
        e.Index = s_entries.Count;
        s_entries.Add(e);
    }

    // Object gone (stowed/destroyed): drop its highlights. The entry is reused, so reset it.
    static void DropEntry(Entry e)
    {
        RemoveEntry(e);
        s_highlightedTotal -= e.Highlight;
        e.Highlight = 0;
        RemoveHighlighted(e);
    }

    static void RemoveEntry(Entry e)
    {
        int i = e.Index;
        if (i < 0)
            return;
        e.Index = -1;
        int last = s_entries.Count - 1;
        if (i != last)
        {
            var moved = s_entries[last];
            s_entries[i] = moved;
            moved.Index = i;
        }
        s_entries.RemoveAt(last);
    }

    static void RemoveHighlighted(Entry e)
    {
        int i = e.HighlightIndex;
        if (i < 0)
            return;
        e.HighlightIndex = -1;
        int last = s_highlighted.Count - 1;
        if (i != last)
        {
            var moved = s_highlighted[last];
            s_highlighted[i] = moved;
            moved.HighlightIndex = i;
        }
        s_highlighted.RemoveAt(last);
    }

    static Entry GetOrBuildEntry(Component root)
    {
        if (s_cache.TryGetValue(root, out var e))
            return e;
        e = new Entry
        {
            Owner = root as Interactable ?? root.GetComponent<Interactable>(),
            SubOwner = root as Carriable,
        };
        CollectRenderers(root, out e.Renderers, out e.SubMeshCounts);
        s_cache.Add(root, e);
        return e;
    }

    // Mesh renderers only: particles, lines, trails, sprites are not outlined.
    static void CollectRenderers(Component root, out Renderer[] renderers, out int[] subMeshCounts)
    {
        var all = root.GetComponentsInChildren<Renderer>(true);
        var rends = new List<Renderer>(all.Length);
        var subs = new List<int>(all.Length);
        foreach (var r in all)
        {
            Mesh mesh = null;
            if (r is SkinnedMeshRenderer smr)
                mesh = smr.sharedMesh;
            else if (r is MeshRenderer && r.TryGetComponent(out MeshFilter mf))
                mesh = mf.sharedMesh;
            else
                continue;

            rends.Add(r);
            subs.Add(mesh != null ? Mathf.Max(1, mesh.subMeshCount) : 1);
        }
        renderers = rends.ToArray();
        subMeshCounts = subs.ToArray();
    }
}
