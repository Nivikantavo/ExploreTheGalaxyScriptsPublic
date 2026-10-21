using System;
using System.Collections.Generic;

public static class PlanetTravelAccess
{
    public static WorldCatalogEntry[] GetOrdered(IReadOnlyList<WorldCatalogEntry> entries)
    {
        IReadOnlyList<WorldCatalogEntry> catalog = entries != null && entries.Count > 0
            ? entries
            : WorldCatalog.DefaultEntries;

        var indexed = new List<(int Index, WorldCatalogEntry Entry)>(catalog.Count);
        for (int i = 0; i < catalog.Count; i++)
        {
            WorldCatalogEntry entry = catalog[i];
            if (entry == null || string.IsNullOrEmpty(entry.WorldId) || string.IsNullOrEmpty(entry.SceneName))
                continue;

            indexed.Add((i, entry));
        }

        indexed.Sort(CompareOrderedEntries);

        var result = new WorldCatalogEntry[indexed.Count];
        for (int i = 0; i < indexed.Count; i++)
            result[i] = indexed[i].Entry;

        return result;
    }

    public static string GetFirstWorldId(IReadOnlyList<WorldCatalogEntry> entries = null)
    {
        WorldCatalogEntry[] ordered = GetOrdered(entries);
        if (ordered.Length == 0)
            return SaveWorldIds.World1;

        return ordered[0].WorldId;
    }

    public static string[] EnsureDefaultUnlocks(
        string[] unlockedWorldIds,
        string currentWorldId = null,
        IReadOnlyList<WorldCatalogEntry> entries = null)
    {
        var ids = new List<string>();
        AddUnique(ids, GetFirstWorldId(entries));
        AddUniqueIds(ids, unlockedWorldIds);
        AddUnique(ids, currentWorldId);
        return ids.ToArray();
    }

    public static bool IsUnlocked(
        string worldId,
        string[] unlockedWorldIds,
        IReadOnlyList<WorldCatalogEntry> entries = null,
        string currentWorldId = null)
    {
        if (string.IsNullOrEmpty(worldId))
            return false;

        string[] unlocked = EnsureDefaultUnlocks(unlockedWorldIds, currentWorldId, entries);
        return Contains(unlocked, worldId);
    }

    public static string[] UnlockNext(
        string currentWorldId,
        string[] unlockedWorldIds,
        IReadOnlyList<WorldCatalogEntry> entries = null)
    {
        string[] unlocked = EnsureDefaultUnlocks(unlockedWorldIds, currentWorldId, entries);
        WorldCatalogEntry[] ordered = GetOrdered(entries);
        int currentIndex = IndexOfWorld(ordered, currentWorldId);
        if (currentIndex < 0 || currentIndex + 1 >= ordered.Length)
            return unlocked;

        return UnionWorldIds(unlocked, new[] { ordered[currentIndex + 1].WorldId });
    }

    public static bool IsChainComplete(
        IReadOnlyList<OpeningObjectChainSaveData> chains,
        string chainSaveId,
        int requiredSteps)
    {
        if (chains == null || string.IsNullOrEmpty(chainSaveId) || requiredSteps <= 0)
            return false;

        for (int i = 0; i < chains.Count; i++)
        {
            OpeningObjectChainSaveData chain = chains[i];
            if (chain == null || !string.Equals(chain.ChainSaveId, chainSaveId, StringComparison.Ordinal))
                continue;

            return chain.CompletedStepsCount >= requiredSteps;
        }

        return false;
    }

    public static bool CanTravel(
        string targetWorldId,
        string currentWorldId,
        string[] unlockedWorldIds,
        IReadOnlyList<WorldCatalogEntry> entries = null)
    {
        if (string.IsNullOrEmpty(targetWorldId))
            return false;

        if (string.Equals(targetWorldId, currentWorldId, StringComparison.Ordinal))
            return false;

        if (IndexOfWorld(GetOrdered(entries), targetWorldId) < 0)
            return false;

        return IsUnlocked(targetWorldId, unlockedWorldIds, entries, currentWorldId);
    }

    public static string[] UnionWorldIds(string[] existing, IReadOnlyList<string> extra)
    {
        var ids = new List<string>();
        AddUniqueIds(ids, existing);
        if (extra == null)
            return ids.ToArray();

        for (int i = 0; i < extra.Count; i++)
            AddUnique(ids, extra[i]);

        return ids.ToArray();
    }

    public static bool SameWorldIds(string[] left, string[] right)
    {
        if (ReferenceEquals(left, right))
            return true;

        int leftCount = left?.Length ?? 0;
        int rightCount = right?.Length ?? 0;
        if (leftCount != rightCount)
            return false;

        for (int i = 0; i < leftCount; i++)
        {
            if (!Contains(right, left[i]))
                return false;
        }

        return true;
    }

    private static int CompareOrderedEntries(
        (int Index, WorldCatalogEntry Entry) left,
        (int Index, WorldCatalogEntry Entry) right)
    {
        int cmp = left.Entry.Order.CompareTo(right.Entry.Order);
        return cmp != 0 ? cmp : left.Index.CompareTo(right.Index);
    }

    private static int IndexOfWorld(WorldCatalogEntry[] ordered, string worldId)
    {
        if (ordered == null || string.IsNullOrEmpty(worldId))
            return -1;

        for (int i = 0; i < ordered.Length; i++)
        {
            if (string.Equals(ordered[i].WorldId, worldId, StringComparison.Ordinal))
                return i;
        }

        return -1;
    }

    private static void AddUniqueIds(List<string> ids, string[] source)
    {
        if (source == null)
            return;

        for (int i = 0; i < source.Length; i++)
            AddUnique(ids, source[i]);
    }

    private static void AddUnique(List<string> ids, string worldId)
    {
        if (string.IsNullOrEmpty(worldId) || Contains(ids, worldId))
            return;

        ids.Add(worldId);
    }

    private static bool Contains(IReadOnlyList<string> ids, string worldId)
    {
        if (ids == null || string.IsNullOrEmpty(worldId))
            return false;

        for (int i = 0; i < ids.Count; i++)
        {
            if (string.Equals(ids[i], worldId, StringComparison.Ordinal))
                return true;
        }

        return false;
    }
}
