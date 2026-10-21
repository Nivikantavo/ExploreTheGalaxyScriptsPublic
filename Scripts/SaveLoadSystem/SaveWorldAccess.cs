using System;
using System.Collections.Generic;
using UnityEngine;

public static class SaveWorldAccess
{
    public static WorldSaveData FindWorld(GameSaveData save, string worldId)
    {
        if (save == null)
            return null;

        if (string.IsNullOrEmpty(worldId))
            worldId = SaveWorldIds.World1;

        if (save.Worlds != null)
        {
            for (int i = 0; i < save.Worlds.Length; i++)
            {
                WorldSaveData world = save.Worlds[i];
                if (world == null || string.IsNullOrEmpty(world.WorldId))
                    continue;

                if (string.Equals(world.WorldId, worldId, StringComparison.Ordinal))
                    return world;
            }
        }

        if (string.Equals(worldId, SaveWorldIds.World1, StringComparison.Ordinal) &&
            (save.Worlds == null || save.Worlds.Length == 0))
        {
            return FromRoot(save);
        }

        return null;
    }

    public static WorldSaveData[] EnumerateWorlds(GameSaveData save)
    {
        if (save == null)
            return Array.Empty<WorldSaveData>();

        if (save.Worlds != null && save.Worlds.Length > 0)
        {
            var result = new List<WorldSaveData>(save.Worlds.Length);
            for (int i = 0; i < save.Worlds.Length; i++)
            {
                WorldSaveData world = save.Worlds[i];
                if (world == null || string.IsNullOrEmpty(world.WorldId))
                    continue;

                result.Add(world);
            }

            if (result.Count > 0)
                return result.ToArray();
        }

        return new[] { FromRoot(save) };
    }

    public static WorldSaveData FromRoot(GameSaveData save)
    {
        if (save == null)
            return new WorldSaveData { WorldId = SaveWorldIds.World1 };

        return new WorldSaveData
        {
            WorldId = SaveWorldIds.World1,
            FurthestMainPathIslandIndex = save.FurthestMainPathIslandIndex,
            CompletedGoalsCount = save.CompletedGoalsCount,
            Islands = save.Islands,
            Productions = save.Productions,
            OpeningObjectChains = save.OpeningObjectChains,
            Ships = save.Ships,
            HarvestedOneTimeHolderIds = save.HarvestedOneTimeHolderIds,
            KilledOneTimeEnemyIds = save.KilledOneTimeEnemyIds,
            ArrivedNpcWorkerIds = save.ArrivedNpcWorkerIds
        };
    }

    public static void MirrorWorld1ToRoot(GameSaveData save)
    {
        if (save == null)
            return;

        WorldSaveData world1 = FindWorld(save, SaveWorldIds.World1);
        if (world1 == null)
            return;

        save.FurthestMainPathIslandIndex = world1.FurthestMainPathIslandIndex;
        save.CompletedGoalsCount = world1.CompletedGoalsCount;
        save.Islands = world1.Islands;
        save.Productions = world1.Productions;
        save.OpeningObjectChains = world1.OpeningObjectChains;
        save.Ships = world1.Ships;
        save.HarvestedOneTimeHolderIds = world1.HarvestedOneTimeHolderIds;
        save.KilledOneTimeEnemyIds = world1.KilledOneTimeEnemyIds;
        save.ArrivedNpcWorkerIds = world1.ArrivedNpcWorkerIds;
    }

    public static string[] CollectUniqueIds(GameSaveData save, Func<WorldSaveData, string[]> selector)
    {
        var ids = new HashSet<string>();
        if (save == null || selector == null)
            return Array.Empty<string>();

        AddIds(ids, selector(FromRoot(save)));

        WorldSaveData[] worlds = EnumerateWorlds(save);
        for (int i = 0; i < worlds.Length; i++)
            AddIds(ids, selector(worlds[i]));

        if (ids.Count == 0)
            return Array.Empty<string>();

        var result = new string[ids.Count];
        ids.CopyTo(result);
        return result;
    }

    public static int GetCompletedGoalsCount(GameSaveData save, string worldId)
    {
        if (string.IsNullOrEmpty(worldId))
            worldId = SaveWorldIds.World1;

        WorldSaveData world = FindWorld(save, worldId);
        if (world != null)
            return Math.Max(0, world.CompletedGoalsCount);

        if (string.Equals(worldId, SaveWorldIds.World1, StringComparison.Ordinal))
            return Math.Max(0, save?.CompletedGoalsCount ?? 0);

        return 0;
    }

    public static bool CanResetWorld(string worldId)
    {
        return !string.IsNullOrEmpty(worldId) &&
               !string.Equals(worldId, SaveWorldIds.World1, StringComparison.Ordinal);
    }

    public static WorldSaveData CreateEmpty(string worldId)
    {
        return new WorldSaveData
        {
            WorldId = string.IsNullOrEmpty(worldId) ? SaveWorldIds.World1 : worldId,
            FurthestMainPathIslandIndex = 0,
            CompletedGoalsCount = 0,
            Islands = Array.Empty<IslandSaveData>(),
            Productions = Array.Empty<ProductionSaveData>(),
            OpeningObjectChains = Array.Empty<OpeningObjectChainSaveData>(),
            Ships = Array.Empty<ShipSaveData>(),
            HarvestedOneTimeHolderIds = Array.Empty<string>(),
            KilledOneTimeEnemyIds = Array.Empty<string>(),
            ArrivedNpcWorkerIds = Array.Empty<string>()
        };
    }

    public static string[] GetWorldScopedIds(
        GameSaveData save,
        string worldId,
        Func<WorldSaveData, string[]> selector)
    {
        if (save == null || selector == null)
            return Array.Empty<string>();

        WorldSaveData world = FindWorld(save, worldId);
        if (world == null)
            return Array.Empty<string>();

        return selector(world) ?? Array.Empty<string>();
    }

    public static bool ClearWorldProgress(GameSaveData save, string worldId)
    {
        if (save == null || !CanResetWorld(worldId))
            return false;

        SaveDataMigration.ToCurrent(save);
        WorldSaveData existing = FindWorld(save, worldId);
        var leakedIds = new HashSet<string>();
        AddIds(leakedIds, existing?.HarvestedOneTimeHolderIds);
        AddIds(leakedIds, existing?.KilledOneTimeEnemyIds);
        AddIds(leakedIds, existing?.ArrivedNpcWorkerIds);

        WorldSaveData empty = CreateEmpty(worldId);
        var result = new List<WorldSaveData>();
        bool replaced = false;

        if (save.Worlds != null)
        {
            for (int i = 0; i < save.Worlds.Length; i++)
            {
                WorldSaveData world = save.Worlds[i];
                if (world == null || string.IsNullOrEmpty(world.WorldId))
                    continue;

                if (string.Equals(world.WorldId, worldId, StringComparison.Ordinal))
                {
                    result.Add(empty);
                    replaced = true;
                }
                else
                {
                    result.Add(world);
                }
            }
        }

        if (!replaced)
            result.Add(empty);

        save.Worlds = result.ToArray();
        StripWorldObjectIds(save, leakedIds);
        if (string.Equals(worldId, SaveWorldIds.World1, StringComparison.Ordinal))
            MirrorWorld1ToRoot(save);

        return true;
    }

    public static void StripWorldObjectIds(GameSaveData save, HashSet<string> ids)
    {
        if (save == null || ids == null || ids.Count == 0)
            return;

        save.HarvestedOneTimeHolderIds = ExceptIds(save.HarvestedOneTimeHolderIds, ids);
        save.KilledOneTimeEnemyIds = ExceptIds(save.KilledOneTimeEnemyIds, ids);
        save.ArrivedNpcWorkerIds = ExceptIds(save.ArrivedNpcWorkerIds, ids);

        if (save.Worlds == null)
            return;

        for (int i = 0; i < save.Worlds.Length; i++)
        {
            WorldSaveData world = save.Worlds[i];
            if (world == null)
                continue;

            world.HarvestedOneTimeHolderIds = ExceptIds(world.HarvestedOneTimeHolderIds, ids);
            world.KilledOneTimeEnemyIds = ExceptIds(world.KilledOneTimeEnemyIds, ids);
            world.ArrivedNpcWorkerIds = ExceptIds(world.ArrivedNpcWorkerIds, ids);
        }
    }

    public static string[] ExceptIds(string[] source, HashSet<string> remove)
    {
        if (source == null || source.Length == 0)
            return Array.Empty<string>();

        if (remove == null || remove.Count == 0)
            return source;

        var kept = new List<string>(source.Length);
        for (int i = 0; i < source.Length; i++)
        {
            string id = source[i];
            if (string.IsNullOrEmpty(id) || remove.Contains(id))
                continue;

            kept.Add(id);
        }

        return kept.ToArray();
    }

    public static bool TryResetInStorage(ISaveStorage storage, string worldId)
    {
        if (storage == null || !CanResetWorld(worldId))
            return false;

        string pickedJson = SaveSlotSelector.PickBestJson(storage.Load(), storage.LoadPrevious());
        if (!SaveSlotSelector.TryParse(pickedJson, out GameSaveData save) || save == null)
            return false;

        if (!ClearWorldProgress(save, worldId))
            return false;

        save.SavedAtUtcTicks = DateTime.UtcNow.Ticks;
        storage.SaveBothSlots(JsonUtility.ToJson(save));
        return true;
    }

    public static void AddIds(HashSet<string> ids, string[] source)
    {
        if (ids == null || source == null)
            return;

        for (int i = 0; i < source.Length; i++)
        {
            if (!string.IsNullOrEmpty(source[i]))
                ids.Add(source[i]);
        }
    }
}
