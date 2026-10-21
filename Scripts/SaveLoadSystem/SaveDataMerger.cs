using System;
using System.Collections.Generic;
using UnityEngine;

public class LiveWorldSnapshot
{
    public string WorldId;
    public int FurthestMainPathIslandIndex;
    public int CompletedGoalsCount;
    public IslandSaveData[] Islands;
    public ProductionSaveData[] Productions;
    public string[] PresentProductionSaveIds;
    public OpeningObjectChainSaveData[] OpeningObjectChains;
    public string[] PresentChainSaveIds;
    public ShipSaveData[] Ships;
    public string[] PresentShipSaveIds;
    public string[] HarvestedOneTimeHolderIds;
    public string[] KilledOneTimeEnemyIds;
    public string[] ArrivedNpcWorkerIds;
    public ResourceAmountSave[] Resources;
    public ResourceAmountSave[] LifetimeResources;
    public int EnemiesKilled;
    public InstrumentSaveData[] Instruments;
    public EquipmentSaveData[] Equipment;
    public InstrumentUpgradeSaveData[] InstrumentUpgrades;
    public string[] PresentInstrumentUpgradeTypes;
    public EquipmentUpgradeSaveData[] EquipmentUpgrades;
    public string[] PresentEquipmentUpgradeTypes;
    public string[] TriggeredCelebrationIds;
    public string[] TriggeredAchievementIds;
    public string[] UnlockedWorldIds;
    public bool IsTutorialCompleted;
}

public static class SaveDataMerger
{
    public static GameSaveData Merge(GameSaveData lastKnownGood, LiveWorldSnapshot live)
    {
        if (live == null || string.IsNullOrEmpty(live.WorldId))
            return SaveDataMigration.ToCurrent(SaveDataMigration.Clone(lastKnownGood));

        GameSaveData previous = SaveDataMigration.ToCurrent(SaveDataMigration.Clone(lastKnownGood));

        var result = new GameSaveData
        {
            SchemaVersion = SaveWorldIds.CurrentSchemaVersion,
            SavedAtUtcTicks = DateTime.UtcNow.Ticks,
            CurrentWorldId = live.WorldId,
            IsTutorialCompleted = live.IsTutorialCompleted || (previous?.IsTutorialCompleted ?? false),
            Resources = live.Resources,
            LifetimeResources = live.LifetimeResources,
            EnemiesKilled = Math.Max(live.EnemiesKilled, previous?.EnemiesKilled ?? 0),
            Instruments = live.Instruments,
            Equipment = live.Equipment,
            InstrumentUpgrades = MergePresentKeyed(
                previous?.InstrumentUpgrades,
                live.InstrumentUpgrades,
                live.PresentInstrumentUpgradeTypes,
                upgrade => upgrade.InstrumentType.ToString()),
            EquipmentUpgrades = MergePresentKeyed(
                previous?.EquipmentUpgrades,
                live.EquipmentUpgrades,
                live.PresentEquipmentUpgradeTypes,
                upgrade => upgrade.EquipmentType.ToString()),
            TriggeredCelebrationIds = UnionIds(
                live.TriggeredCelebrationIds,
                previous?.TriggeredCelebrationIds),
            TriggeredAchievementIds = UnionIds(
                live.TriggeredAchievementIds,
                previous?.TriggeredAchievementIds),
            AdsRemoved = previous?.AdsRemoved ?? false,
            StarterPackPurchased = previous?.StarterPackPurchased ?? false,
            PurchasedIslandOfferIndices = previous?.PurchasedIslandOfferIndices,
            ShownIslandOfferIndices = previous?.ShownIslandOfferIndices,
            UnlockedWorldIds = UnionIds(live.UnlockedWorldIds, previous?.UnlockedWorldIds)
        };

        result.Worlds = MergeWorldList(previous, live);
        SaveWorldAccess.MirrorWorld1ToRoot(result);
        return result;
    }

    public static T[] MergePresentKeyed<T>(
        T[] previous,
        T[] live,
        IReadOnlyCollection<string> presentIds,
        Func<T, string> keySelector)
        where T : class
    {
        var result = new List<T>();
        var usedKeys = new HashSet<string>();
        var present = presentIds != null
            ? new HashSet<string>(presentIds)
            : new HashSet<string>();

        if (live != null)
        {
            for (int i = 0; i < live.Length; i++)
            {
                T item = live[i];
                if (item == null)
                    continue;

                string key = keySelector(item);
                if (string.IsNullOrEmpty(key) || !usedKeys.Add(key))
                    continue;

                result.Add(item);
            }
        }

        if (previous != null)
        {
            for (int i = 0; i < previous.Length; i++)
            {
                T item = previous[i];
                if (item == null)
                    continue;

                string key = keySelector(item);
                if (string.IsNullOrEmpty(key) || usedKeys.Contains(key) || present.Contains(key))
                    continue;

                usedKeys.Add(key);
                result.Add(item);
            }
        }

        return result.ToArray();
    }

    public static string[] UnionIds(params string[][] sources)
    {
        var ids = new HashSet<string>();
        if (sources == null)
            return Array.Empty<string>();

        for (int i = 0; i < sources.Length; i++)
            SaveWorldAccess.AddIds(ids, sources[i]);

        if (ids.Count == 0)
            return Array.Empty<string>();

        var result = new string[ids.Count];
        ids.CopyTo(result);
        return result;
    }

    private static WorldSaveData[] MergeWorldList(GameSaveData previous, LiveWorldSnapshot live)
    {
        var result = new List<WorldSaveData>();
        bool replaced = false;

        if (previous?.Worlds != null)
        {
            for (int i = 0; i < previous.Worlds.Length; i++)
            {
                WorldSaveData world = previous.Worlds[i];
                if (world == null || string.IsNullOrEmpty(world.WorldId))
                    continue;

                if (string.Equals(world.WorldId, live.WorldId, StringComparison.Ordinal))
                {
                    result.Add(MergeWorldSlice(world, live));
                    replaced = true;
                }
                else
                {
                    result.Add(CloneWorld(world));
                }
            }
        }

        if (!replaced)
            result.Add(MergeWorldSlice(null, live));

        return result.ToArray();
    }

    private static WorldSaveData MergeWorldSlice(WorldSaveData previous, LiveWorldSnapshot live)
    {
        return new WorldSaveData
        {
            WorldId = live.WorldId,
            FurthestMainPathIslandIndex = Math.Max(
                live.FurthestMainPathIslandIndex,
                previous?.FurthestMainPathIslandIndex ?? 0),
            CompletedGoalsCount = Math.Max(
                live.CompletedGoalsCount,
                previous?.CompletedGoalsCount ?? 0),
            Islands = MergeIslands(previous?.Islands, live.Islands),
            Productions = MergePresentKeyed(
                previous?.Productions,
                live.Productions,
                live.PresentProductionSaveIds,
                production => production.SaveId),
            OpeningObjectChains = MergePresentKeyed(
                previous?.OpeningObjectChains,
                live.OpeningObjectChains,
                live.PresentChainSaveIds,
                chain => chain.ChainSaveId),
            Ships = MergePresentKeyed(
                previous?.Ships,
                live.Ships,
                live.PresentShipSaveIds,
                ship => ship.SaveId),
            HarvestedOneTimeHolderIds = UnionIds(
                live.HarvestedOneTimeHolderIds,
                previous?.HarvestedOneTimeHolderIds),
            KilledOneTimeEnemyIds = UnionIds(
                live.KilledOneTimeEnemyIds,
                previous?.KilledOneTimeEnemyIds),
            ArrivedNpcWorkerIds = UnionIds(
                live.ArrivedNpcWorkerIds,
                previous?.ArrivedNpcWorkerIds)
        };
    }

    public static IslandSaveData[] MergeIslands(IslandSaveData[] previous, IslandSaveData[] live)
    {
        var result = new Dictionary<string, IslandSaveData>();
        AddIslands(result, previous);
        AddIslands(result, live);

        if (result.Count == 0)
            return Array.Empty<IslandSaveData>();

        var islands = new IslandSaveData[result.Count];
        result.Values.CopyTo(islands, 0);
        return islands;
    }

    public static bool MatchesIslandScene(IslandSaveData save, int islandIndex, string sceneName)
    {
        if (save == null || save.IslandIndex != islandIndex)
            return false;

        bool sceneIsHost = WorldCatalog.IsKnownHostScene(sceneName);
        if (string.IsNullOrEmpty(save.SceneName))
            return sceneIsHost;

        return string.Equals(save.SceneName, sceneName, StringComparison.Ordinal);
    }

    private static void AddIslands(Dictionary<string, IslandSaveData> result, IslandSaveData[] source)
    {
        if (source == null)
            return;

        for (int i = 0; i < source.Length; i++)
        {
            IslandSaveData island = source[i];
            if (island == null)
                continue;

            string key = SaveIntegrityGuard.GetIslandKey(island);
            if (!result.TryGetValue(key, out IslandSaveData existing))
            {
                result[key] = island;
                continue;
            }

            result[key] = ChooseBetterIsland(existing, island);
        }
    }

    public static IslandSaveData ChooseBetterIsland(IslandSaveData current, IslandSaveData incoming)
    {
        if (current == null)
            return incoming;
        if (incoming == null)
            return current;

        int currentWeight = SaveIntegrityGuard.GetIslandWeight(current);
        int incomingWeight = SaveIntegrityGuard.GetIslandWeight(incoming);
        if (incomingWeight != currentWeight)
            return incomingWeight > currentWeight ? incoming : current;

        if (incoming.State < current.State)
            return current;

        return incoming;
    }

    private static WorldSaveData CloneWorld(WorldSaveData world)
    {
        if (world == null)
            return null;

        return JsonUtility.FromJson<WorldSaveData>(JsonUtility.ToJson(world));
    }
}
