using System;
using System.Collections.Generic;

[Serializable]
public enum IslandUnlockState
{
    Locked = 0,
    PartiallyOpened = 1,
    FullyOpened = 2
}

[Serializable]
public enum TimedProcessState
{
    Idle = 0,
    InProgress = 1,
    Completed = 2
}

[Serializable]
public struct HexCoordSave
{
    public int Q;
    public int R;

    public HexCoordSave(int q, int r)
    {
        Q = q;
        R = r;
    }
}

[Serializable]
public struct ResourceAmountSave
{
    public ResourceType Type;
    public int Amount;
}

[Serializable]
public class InstrumentSaveData
{
    public InstrumentType Type;
    public int Level;
}

[Serializable]
public class ProductionSaveData
{
    public string SaveId;
    public TimedProcessState State;
    public int ResourceCount;
    public int TotalDuration;
    public long StartUtcTicks;
}

[Serializable]
public class InstrumentUpgradeSaveData
{
    public InstrumentType InstrumentType;
    public TimedProcessState State;
    public int TargetLevel;
    public int TotalDuration;
    public long StartUtcTicks;
}

[Serializable]
public class EquipmentSaveData
{
    public EquipmentType Type;
    public int Level;
}

[Serializable]
public class EquipmentUpgradeSaveData
{
    public EquipmentType EquipmentType;
    public TimedProcessState State;
    public int TargetLevel;
    public int TotalDuration;
    public long StartUtcTicks;
}

[Serializable]
public class OpeningHexSaveData
{
    public int Q;
    public int R;
    public ResourceAmountSave[] RemainsToPay;
    public float AccumulatedTransferTime;
}

[Serializable]
public class OpeningObjectChainSaveData
{
    public string ChainSaveId;
    public int CompletedStepsCount;
    public ResourceAmountSave[] RemainsToPay;
    public float AccumulatedTransferTime;
}

[Serializable]
public class ShipSaveData
{
    public string SaveId;
    public bool AtDestination;
}

[Serializable]
public class IslandSaveData
{
    public int IslandIndex;
    public string SceneName;
    public IslandUnlockState State;
    public HexCoordSave[] UnlockedHexes;
    public OpeningHexSaveData[] OpeningHexes;
}

[Serializable]
public class WorldSaveData
{
    public string WorldId;
    public int FurthestMainPathIslandIndex;
    public int CompletedGoalsCount;
    public IslandSaveData[] Islands;
    public ProductionSaveData[] Productions;
    public OpeningObjectChainSaveData[] OpeningObjectChains;
    public ShipSaveData[] Ships;
    public string[] HarvestedOneTimeHolderIds;
    public string[] KilledOneTimeEnemyIds;
    public string[] ArrivedNpcWorkerIds;
}

[Serializable]
public class GameSaveData
{
    public int SchemaVersion;
    public string CurrentWorldId;
    public string[] UnlockedWorldIds;
    public WorldSaveData[] Worlds;
    public long SavedAtUtcTicks;
    public int FurthestMainPathIslandIndex;
    public bool IsTutorialCompleted;
    public int CompletedGoalsCount;
    public string[] TriggeredCelebrationIds;
    public string[] TriggeredAchievementIds;
    public ResourceAmountSave[] Resources;
    public ResourceAmountSave[] LifetimeResources;
    public int EnemiesKilled;
    public InstrumentSaveData[] Instruments;
    public EquipmentSaveData[] Equipment;
    public ProductionSaveData[] Productions;
    public InstrumentUpgradeSaveData[] InstrumentUpgrades;
    public EquipmentUpgradeSaveData[] EquipmentUpgrades;
    public IslandSaveData[] Islands;
    public OpeningObjectChainSaveData[] OpeningObjectChains;
    public ShipSaveData[] Ships;
    public string[] HarvestedOneTimeHolderIds;
    public string[] KilledOneTimeEnemyIds;
    public string[] ArrivedNpcWorkerIds;
    public bool AdsRemoved;
    public bool StarterPackPurchased;
    public int[] PurchasedIslandOfferIndices;
    public int[] ShownIslandOfferIndices;
}

public static class SaveDataConversion
{
    public static ResourceAmountSave[] ToSaveArray(IReadOnlyDictionary<ResourceType, int> source)
    {
        if (source == null || source.Count == 0)
            return Array.Empty<ResourceAmountSave>();

        var result = new ResourceAmountSave[source.Count];
        int index = 0;
        foreach (var pair in source)
        {
            result[index++] = new ResourceAmountSave { Type = pair.Key, Amount = pair.Value };
        }

        return result;
    }

    public static Dictionary<ResourceType, int> ToDictionary(ResourceAmountSave[] source)
    {
        var result = new Dictionary<ResourceType, int>();
        if (source == null)
            return result;

        foreach (var item in source)
            result[item.Type] = item.Amount;

        return result;
    }
}
