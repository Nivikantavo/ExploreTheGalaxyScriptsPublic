using System;
using System.Collections.Generic;

public static class SaveIntegrityGuard
{
    private const int FullyOpenedIslandWeight = 10_000;

    public static bool LooksLikeUninitialized(GameSaveData save)
    {
        if (save == null)
            return true;

        if (save.SavedAtUtcTicks > 0)
            return false;

        if (save.FurthestMainPathIslandIndex > 0
            || save.IsTutorialCompleted
            || save.CompletedGoalsCount > 0
            || save.EnemiesKilled > 0
            || save.AdsRemoved
            || save.StarterPackPurchased)
        {
            return false;
        }

        if (HasArrayItems(save.Islands)
            || HasArrayItems(save.Resources)
            || HasArrayItems(save.LifetimeResources)
            || HasArrayItems(save.PurchasedIslandOfferIndices)
            || HasArrayItems(save.Productions)
            || HasArrayItems(save.Ships)
            || HasArrayItems(save.HarvestedOneTimeHolderIds))
        {
            return false;
        }

        WorldSaveData[] worlds = save.Worlds;
        if (worlds == null)
            return true;

        for (int i = 0; i < worlds.Length; i++)
        {
            if (WorldHasProgress(worlds[i]))
                return false;
        }

        return true;
    }

    public static int ComputeProgressScore(GameSaveData save)
    {
        if (save == null || LooksLikeUninitialized(save))
            return 0;

        int score = 0;
        if (save.IsTutorialCompleted)
            score += 1_000_000;

        WorldSaveData[] worlds = SaveWorldAccess.EnumerateWorlds(save);
        for (int i = 0; i < worlds.Length; i++)
        {
            WorldSaveData world = worlds[i];
            if (world == null)
                continue;

            score += Math.Max(0, world.FurthestMainPathIslandIndex) * 10_000;
            score += GetIslandArrayWeight(world.Islands);
            score += Math.Max(0, world.CompletedGoalsCount) * 100;
        }

        score += Math.Max(0, save.EnemiesKilled);
        if (save.AdsRemoved)
            score += 50_000;
        if (save.StarterPackPurchased)
            score += 50_000;
        score += save.PurchasedIslandOfferIndices?.Length ?? 0;
        score += Math.Max(0, worlds.Length - 1) * 1_000;

        return score;
    }

    public static bool IsRegression(GameSaveData candidate, GameSaveData lastKnownGood)
    {
        return TryGetRegressionReason(candidate, lastKnownGood) != null;
    }

    public static string TryGetRegressionReason(GameSaveData candidate, GameSaveData lastKnownGood)
    {
        if (lastKnownGood == null || LooksLikeUninitialized(lastKnownGood))
            return null;

        if (candidate == null || LooksLikeUninitialized(candidate))
            return "candidate uninitialized";

        if (lastKnownGood.IsTutorialCompleted && !candidate.IsTutorialCompleted)
            return "tutorial cleared";

        if (candidate.EnemiesKilled < lastKnownGood.EnemiesKilled)
            return $"enemies {candidate.EnemiesKilled}<{lastKnownGood.EnemiesKilled}";

        if (lastKnownGood.AdsRemoved && !candidate.AdsRemoved)
            return "adsRemoved cleared";

        if (lastKnownGood.StarterPackPurchased && !candidate.StarterPackPurchased)
            return "starterPack cleared";

        int lastOffers = lastKnownGood.PurchasedIslandOfferIndices?.Length ?? 0;
        int candidateOffers = candidate.PurchasedIslandOfferIndices?.Length ?? 0;
        if (candidateOffers < lastOffers)
            return $"islandOffers {candidateOffers}<{lastOffers}";

        WorldSaveData[] lastWorlds = SaveWorldAccess.EnumerateWorlds(lastKnownGood);
        for (int i = 0; i < lastWorlds.Length; i++)
        {
            WorldSaveData lastWorld = lastWorlds[i];
            if (lastWorld == null || string.IsNullOrEmpty(lastWorld.WorldId))
                continue;

            WorldSaveData candidateWorld = SaveWorldAccess.FindWorld(candidate, lastWorld.WorldId);
            if (candidateWorld == null)
                return $"missing world {lastWorld.WorldId}";

            int candidateWeight = GetIslandArrayWeight(candidateWorld.Islands);
            int lastWeight = GetIslandArrayWeight(lastWorld.Islands);
            if (candidateWeight < lastWeight)
                return $"world {lastWorld.WorldId} islandWeight {candidateWeight}<{lastWeight}";

            if (candidateWorld.FurthestMainPathIslandIndex < lastWorld.FurthestMainPathIslandIndex)
            {
                return $"world {lastWorld.WorldId} furthest " +
                       $"{candidateWorld.FurthestMainPathIslandIndex}<{lastWorld.FurthestMainPathIslandIndex}";
            }

            if (candidateWorld.CompletedGoalsCount < lastWorld.CompletedGoalsCount)
            {
                return $"world {lastWorld.WorldId} goals " +
                       $"{candidateWorld.CompletedGoalsCount}<{lastWorld.CompletedGoalsCount}";
            }
        }

        return null;
    }

    public static string GetIslandKey(IslandSaveData island)
    {
        if (island == null)
            return string.Empty;

        return (island.SceneName ?? string.Empty) + "#" + island.IslandIndex;
    }

    public static int GetIslandWeight(IslandSaveData island)
    {
        if (island == null)
            return 0;

        if (island.State == IslandUnlockState.FullyOpened)
            return FullyOpenedIslandWeight;

        return island.UnlockedHexes?.Length ?? 0;
    }

    public static int GetIslandUnlockWeight(GameSaveData save)
    {
        int weight = 0;
        WorldSaveData[] worlds = SaveWorldAccess.EnumerateWorlds(save);
        for (int i = 0; i < worlds.Length; i++)
            weight += GetIslandArrayWeight(worlds[i]?.Islands);

        return weight;
    }

    public static int GetIslandArrayWeight(IslandSaveData[] islands)
    {
        if (islands == null || islands.Length == 0)
            return 0;

        var bestByKey = new Dictionary<string, int>();
        for (int i = 0; i < islands.Length; i++)
        {
            IslandSaveData island = islands[i];
            if (island == null)
                continue;

            string key = GetIslandKey(island);
            int weight = GetIslandWeight(island);
            if (!bestByKey.TryGetValue(key, out int current) || weight > current)
                bestByKey[key] = weight;
        }

        int total = 0;
        foreach (int weight in bestByKey.Values)
            total += weight;

        return total;
    }

    private static bool WorldHasProgress(WorldSaveData world)
    {
        if (world == null)
            return false;

        return world.FurthestMainPathIslandIndex > 0
               || world.CompletedGoalsCount > 0
               || HasArrayItems(world.Islands)
               || HasArrayItems(world.Productions)
               || HasArrayItems(world.Ships)
               || HasArrayItems(world.HarvestedOneTimeHolderIds)
               || HasArrayItems(world.KilledOneTimeEnemyIds)
               || HasArrayItems(world.ArrivedNpcWorkerIds)
               || HasArrayItems(world.OpeningObjectChains);
    }

    private static bool HasArrayItems<T>(T[] source)
    {
        return source != null && source.Length > 0;
    }
}
