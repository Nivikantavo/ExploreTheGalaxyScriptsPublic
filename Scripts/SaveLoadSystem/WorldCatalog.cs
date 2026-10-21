using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Localization;
using UnityEngine.SceneManagement;

[Serializable]
public class WorldCatalogEntry
{
    public string WorldId;
    public string SceneName;
    public int Order;
    public LocalizedString DisplayName;
    public Sprite Icon;
    public GoalSequenceConfigSO GoalSequence;

    public string GetDisplayName()
    {
        string localized = LocalizationSafe.Get(DisplayName, WorldId);
        return string.IsNullOrEmpty(localized) ? WorldId : localized;
    }
}

public static class WorldCatalog
{
    public const string World1SceneName = "GameplayScene";
    public const string World2SceneName = "GameplayScene 1";

    public static readonly WorldCatalogEntry[] DefaultEntries =
    {
        new WorldCatalogEntry { WorldId = SaveWorldIds.World1, SceneName = World1SceneName, Order = 0 },
        new WorldCatalogEntry { WorldId = SaveWorldIds.World2, SceneName = World2SceneName, Order = 1 }
    };

    public static string ResolveSceneName(
        string worldId,
        IReadOnlyList<WorldCatalogEntry> entries = null,
        string fallbackSceneName = World1SceneName)
    {
        if (string.IsNullOrEmpty(worldId))
            worldId = SaveWorldIds.World1;

        IReadOnlyList<WorldCatalogEntry> catalog = entries != null && entries.Count > 0
            ? entries
            : DefaultEntries;

        for (int i = 0; i < catalog.Count; i++)
        {
            WorldCatalogEntry entry = catalog[i];
            if (entry == null || string.IsNullOrEmpty(entry.WorldId) || string.IsNullOrEmpty(entry.SceneName))
                continue;

            if (string.Equals(entry.WorldId, worldId, StringComparison.Ordinal))
                return entry.SceneName;
        }

        return string.IsNullOrEmpty(fallbackSceneName) ? World1SceneName : fallbackSceneName;
    }

    public static string ResolveWorldIdFromScene(
        string sceneName,
        IReadOnlyList<WorldCatalogEntry> entries = null)
    {
        if (string.IsNullOrEmpty(sceneName))
            return SaveWorldIds.World1;

        IReadOnlyList<WorldCatalogEntry> catalog = entries != null && entries.Count > 0
            ? entries
            : DefaultEntries;

        for (int i = 0; i < catalog.Count; i++)
        {
            WorldCatalogEntry entry = catalog[i];
            if (entry == null || string.IsNullOrEmpty(entry.WorldId) || string.IsNullOrEmpty(entry.SceneName))
                continue;

            if (string.Equals(entry.SceneName, sceneName, StringComparison.Ordinal))
                return entry.WorldId;
        }

        return SaveWorldIds.World1;
    }

    public static string ResolveWorldIdFromLoadedHostScene(
        IReadOnlyList<WorldCatalogEntry> entries = null)
    {
        int count = SceneManager.sceneCount;
        for (int i = 0; i < count; i++)
        {
            Scene scene = SceneManager.GetSceneAt(i);
            if (!scene.IsValid() || !scene.isLoaded)
                continue;

            string worldId = ResolveWorldIdFromScene(scene.name, entries);
            if (IsKnownHostScene(scene.name, entries))
                return worldId;
        }

        return SaveWorldIds.World1;
    }

    public static bool IsHostMismatch(string savedWorldId, string hostWorldId)
    {
        if (string.IsNullOrEmpty(savedWorldId) || string.IsNullOrEmpty(hostWorldId))
            return false;

        return !string.Equals(savedWorldId, hostWorldId, StringComparison.Ordinal);
    }

    public static bool IsKnownHostScene(string sceneName, IReadOnlyList<WorldCatalogEntry> entries = null)
    {
        IReadOnlyList<WorldCatalogEntry> catalog = entries != null && entries.Count > 0
            ? entries
            : DefaultEntries;

        for (int i = 0; i < catalog.Count; i++)
        {
            WorldCatalogEntry entry = catalog[i];
            if (entry != null && string.Equals(entry.SceneName, sceneName, StringComparison.Ordinal))
                return true;
        }

        return false;
    }

    public static string GetNextWorldId(string worldId, IReadOnlyList<WorldCatalogEntry> entries = null)
    {
        WorldCatalogEntry[] ordered = PlanetTravelAccess.GetOrdered(
            entries != null && entries.Count > 0 ? entries : DefaultEntries);

        if (string.IsNullOrEmpty(worldId))
            worldId = SaveWorldIds.World1;

        for (int i = 0; i < ordered.Length - 1; i++)
        {
            WorldCatalogEntry entry = ordered[i];
            if (entry == null || !string.Equals(entry.WorldId, worldId, StringComparison.Ordinal))
                continue;

            WorldCatalogEntry next = ordered[i + 1];
            return next != null ? next.WorldId : null;
        }

        return null;
    }

    public static GoalSequenceConfigSO FindGoalSequence(
        string worldId,
        IReadOnlyList<WorldCatalogEntry> entries)
    {
        if (string.IsNullOrEmpty(worldId) || entries == null)
            return null;

        for (int i = 0; i < entries.Count; i++)
        {
            WorldCatalogEntry entry = entries[i];
            if (entry == null || entry.GoalSequence == null)
                continue;

            if (string.Equals(entry.WorldId, worldId, StringComparison.Ordinal))
                return entry.GoalSequence;
        }

        return null;
    }
}
