public interface ISaveLoader
{
    bool HasSave { get; }
    bool HasLoadedSave { get; }
    bool IsTutorialCompleted { get; }
    bool PersistAllowed { get; set; }
    bool IsWorldUntrusted { get; }
    bool IsHostWorldMismatch { get; }
    GameSaveData LoadedSave { get; }

    bool TryLoad();
    void BindLiveScene(LiveSceneSaveCache scene);
    void Save();
    void Save(bool collectIslandProgress);
    void FlushLastKnownGood();
    void MarkWorldUntrusted();
    void MarkTutorialCompleted();
    string GetHostWorldId();
    string[] GetUnlockedWorldIds();
    void GrantWorldUnlocks(System.Collections.Generic.IReadOnlyList<string> worldIds);
    bool TryResetWorldProgress(string worldId);
    bool SaveThenTravelTo(string targetWorldId);
    string ResolveRedirectSceneName();
    int GetCompletedGoalsCount();

    void ApplyInventory(Inventory inventory, AllInstruments allInstruments, AllEquipment allEquipment);
    void ApplyIslands();
    void DisableCompletedOpeningHexagons();
    void ApplyOpeningObjectChains();
    void ApplyHarvestedOneTimeHolders();
    void ApplyKilledOneTimeEnemies();
    void ApplyArrivedNpcWorkers();
    void ApplyShips();

    ProductionSaveData GetProductionSave(string saveId);
    InstrumentUpgradeSaveData GetInstrumentUpgradeSave(InstrumentType instrumentType);
    EquipmentUpgradeSaveData GetEquipmentUpgradeSave(EquipmentType equipmentType);
}
