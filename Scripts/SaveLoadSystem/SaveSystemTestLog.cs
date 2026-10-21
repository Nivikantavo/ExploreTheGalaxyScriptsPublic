using System.Diagnostics;
using System.Text;
using UnityEngine;

public static class SaveSystemTestLog
{
    public const string Prefix = "SaveSystemTest:";

    [Conditional("UNITY_EDITOR")]
    public static void Info(string message) => UnityEngine.Debug.Log($"{Prefix} {message}");

    [Conditional("UNITY_EDITOR")]
    public static void Warn(string message) => UnityEngine.Debug.LogWarning($"{Prefix} {message}");

    [Conditional("UNITY_EDITOR")]
    public static void Error(string message) => UnityEngine.Debug.LogError($"{Prefix} {message}");

    public static string SummarizeSave(GameSaveData save)
    {
        if (save == null)
            return "null";

        var sb = new StringBuilder(256);
        sb.Append("tutorial=").Append(save.IsTutorialCompleted);
        sb.Append(" schema=").Append(save.SchemaVersion);
        sb.Append(" currentWorld=").Append(save.CurrentWorldId ?? "");
        sb.Append(" unlockedWorlds=").Append(save.UnlockedWorldIds?.Length ?? 0);
        sb.Append(" worlds=").Append(save.Worlds?.Length ?? 0);
        sb.Append(" furthestMainPath=").Append(save.FurthestMainPathIslandIndex);
        sb.Append(" goals=").Append(save.CompletedGoalsCount);
        sb.Append(" resources=").Append(save.Resources?.Length ?? 0);
        sb.Append(" lifetimeResources=").Append(save.LifetimeResources?.Length ?? 0);
        sb.Append(" enemiesKilled=").Append(save.EnemiesKilled);
        sb.Append(" instruments=").Append(save.Instruments?.Length ?? 0);
        sb.Append(" equipment=").Append(save.Equipment?.Length ?? 0);
        sb.Append(" productions=").Append(save.Productions?.Length ?? 0);
        sb.Append(" upgrades=").Append(save.InstrumentUpgrades?.Length ?? 0);
        sb.Append(" equipmentUpgrades=").Append(save.EquipmentUpgrades?.Length ?? 0);
        sb.Append(" celebrations=").Append(save.TriggeredCelebrationIds?.Length ?? 0);
        sb.Append(" achievements=").Append(save.TriggeredAchievementIds?.Length ?? 0);
        sb.Append(" chains=").Append(save.OpeningObjectChains?.Length ?? 0);
        sb.Append(" harvestedHolders=").Append(save.HarvestedOneTimeHolderIds?.Length ?? 0);
        sb.Append(" killedOneTimeEnemies=").Append(save.KilledOneTimeEnemyIds?.Length ?? 0);
        sb.Append(" arrivedNpcWorkers=").Append(save.ArrivedNpcWorkerIds?.Length ?? 0);
        sb.Append(" ships=").Append(save.Ships?.Length ?? 0);
        sb.Append(" islands=").Append(save.Islands?.Length ?? 0);
        sb.Append(" adsRemoved=").Append(save.AdsRemoved);
        sb.Append(" starterPack=").Append(save.StarterPackPurchased);
        sb.Append(" islandOffers=").Append(save.PurchasedIslandOfferIndices?.Length ?? 0);

        if (save.Productions != null)
        {
            foreach (var production in save.Productions)
            {
                if (production == null)
                    continue;

                sb.Append(" | prod[")
                    .Append(production.SaveId)
                    .Append("] state=")
                    .Append(production.State)
                    .Append(" count=")
                    .Append(production.ResourceCount)
                    .Append(" duration=")
                    .Append(production.TotalDuration);
            }
        }

        if (save.InstrumentUpgrades != null)
        {
            foreach (var upgrade in save.InstrumentUpgrades)
            {
                if (upgrade == null)
                    continue;

                sb.Append(" | upgrade[")
                    .Append(upgrade.InstrumentType)
                    .Append("] state=")
                    .Append(upgrade.State)
                    .Append(" target=")
                    .Append(upgrade.TargetLevel);
            }
        }

        if (save.Equipment != null)
        {
            foreach (var equipment in save.Equipment)
            {
                if (equipment == null)
                    continue;

                sb.Append(" | equipment[")
                    .Append(equipment.Type)
                    .Append("] lv=")
                    .Append(equipment.Level);
            }
        }

        if (save.EquipmentUpgrades != null)
        {
            foreach (var upgrade in save.EquipmentUpgrades)
            {
                if (upgrade == null)
                    continue;

                sb.Append(" | eqUpgrade[")
                    .Append(upgrade.EquipmentType)
                    .Append("] state=")
                    .Append(upgrade.State)
                    .Append(" target=")
                    .Append(upgrade.TargetLevel);
            }
        }

        if (save.Worlds != null)
        {
            foreach (var world in save.Worlds)
            {
                if (world == null)
                    continue;

                sb.Append(" | world[")
                    .Append(world.WorldId)
                    .Append("] furthest=")
                    .Append(world.FurthestMainPathIslandIndex)
                    .Append(" goals=")
                    .Append(world.CompletedGoalsCount)
                    .Append(" islands=")
                    .Append(world.Islands?.Length ?? 0)
                    .Append(" weight=")
                    .Append(SaveIntegrityGuard.GetIslandArrayWeight(world.Islands));
            }
        }

        if (save.Islands != null)
        {
            foreach (var island in save.Islands)
            {
                if (island == null)
                    continue;

                sb.Append(" | island[")
                    .Append(island.IslandIndex)
                    .Append("] state=")
                    .Append(island.State)
                    .Append(" unlocked=")
                    .Append(island.UnlockedHexes?.Length ?? 0)
                    .Append(" openings=")
                    .Append(island.OpeningHexes?.Length ?? 0);
            }
        }

        return sb.ToString();
    }
}
