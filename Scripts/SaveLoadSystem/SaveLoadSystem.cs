using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Zenject;

public class SaveLoadSystem : ISaveLoader
{
    public bool HasSave => _saveStorage.HasSave();
    public bool HasLoadedSave => _loadedSave != null;
    public bool IsTutorialCompleted => _isTutorialCompleted;
    public bool PersistAllowed { get; set; }
    public bool IsWorldUntrusted { get; private set; }
    public bool IsHostWorldMismatch =>
        _loadedSave != null && WorldCatalog.IsHostMismatch(_loadedSave.CurrentWorldId, GetHostWorldId());
    public GameSaveData LoadedSave => _loadedSave;

    private readonly ISaveStorage _saveStorage;
    private readonly IslandProgressRegistry _islandRegistry;
    private readonly LazyInject<Inventory> _inventory;
    private readonly IapSaveBridge _iapSaveBridge;
    private GameSaveData _loadedSave;
    private GameSaveData _lastKnownGood;
    private string _lastKnownGoodJson;
    private bool _isTutorialCompleted;
    private LiveSceneSaveCache _scene;
    private string _hostWorldId;

    public SaveLoadSystem(
        ISaveStorage saveStorage,
        IslandProgressRegistry islandRegistry,
        LazyInject<Inventory> inventory,
        IapSaveBridge iapSaveBridge)
    {
        _saveStorage = saveStorage;
        _islandRegistry = islandRegistry;
        _inventory = inventory;
        _iapSaveBridge = iapSaveBridge;
    }

    public bool TryLoad()
    {
        SaveSystemTestLog.Info(
            $"SaveLoadSystem.TryLoad storage.HasSave={_saveStorage.HasSave()} init={_saveStorage.InitState}");

        if (_saveStorage.InitState == SaveStorageInitState.Unknown)
        {
            SaveSystemTestLog.Warn("SaveLoadSystem.TryLoad blocked — storage init is Unknown");
            _loadedSave = null;
            return false;
        }

        string pickedJson = SaveSlotSelector.PickBestJson(_saveStorage.Load(), _saveStorage.LoadPrevious());
        if (string.IsNullOrEmpty(pickedJson))
        {
            SaveSystemTestLog.Info("SaveLoadSystem.TryLoad no valid json in primary or previous slot");
            _loadedSave = null;
            return false;
        }

        if (!SaveSlotSelector.TryParse(pickedJson, out GameSaveData save))
        {
            SaveSystemTestLog.Warn("SaveLoadSystem.TryLoad picked json failed parse");
            _loadedSave = null;
            return false;
        }

        _loadedSave = save;
        _isTutorialCompleted = _loadedSave.IsTutorialCompleted;
        RememberLastKnownGood(_loadedSave, JsonUtility.ToJson(_loadedSave));

        SaveSystemTestLog.Info($"SaveLoadSystem.TryLoad OK {SaveSystemTestLog.SummarizeSave(_loadedSave)}");
        return true;
    }

    public void BindLiveScene(LiveSceneSaveCache scene)
    {
        _scene = scene;
        if (!string.IsNullOrEmpty(scene?.HostWorldId))
            _hostWorldId = scene.HostWorldId;
    }

    public void Save() => Save(collectIslandProgress: true);

    public void Save(bool collectIslandProgress)
    {
        AutosaveHangTrace.Mark("Save.enter collectIslands=" + collectIslandProgress);

        int registeredIslands = _islandRegistry?.Islands.Count ?? 0;
        if (!SavePersistPolicy.ShouldCollectLiveWorld(
                IsWorldUntrusted,
                registeredIslands,
                _lastKnownGood != null,
                !IsHostWorldMismatch))
        {
            SaveSystemTestLog.Warn(
                $"SaveLoadSystem.Save skip live collect untrusted={IsWorldUntrusted} " +
                $"islands={registeredIslands} mismatch={IsHostWorldMismatch} — flushing last-known-good");
            FlushLastKnownGood();
            return;
        }

        if (!CanWriteToStorage())
        {
            SaveSystemTestLog.Warn(
                $"SaveLoadSystem.Save BLOCKED persistAllowed={PersistAllowed} " +
                $"init={_saveStorage.InitState} worldUntrusted={IsWorldUntrusted}");
            return;
        }

        if (!collectIslandProgress && TrySaveVolatile())
        {
            AutosaveHangTrace.Mark("Save.volatileReturned");
            return;
        }

        AutosaveHangTrace.Mark("Save.fullCollect");

        LiveWorldSnapshot snapshot = CollectLiveSnapshot(collectIslandProgress);
        GameSaveData saveData = SaveDataMerger.Merge(_lastKnownGood ?? _loadedSave, snapshot);
        ApplyIapToSaveData(saveData);

        if (SaveIntegrityGuard.IsRegression(saveData, _lastKnownGood))
        {
            SaveSystemTestLog.Error(
                $"SaveLoadSystem.Save REJECTED regression " +
                $"reason={SaveIntegrityGuard.TryGetRegressionReason(saveData, _lastKnownGood)} " +
                $"candidate={SaveSystemTestLog.SummarizeSave(saveData)} " +
                $"lastGood={SaveSystemTestLog.SummarizeSave(_lastKnownGood)}");
            return;
        }

        _loadedSave = saveData;
        AutosaveHangTrace.Mark("Save.fullToJson");
        string json = JsonUtility.ToJson(saveData);
        if (string.Equals(json, _lastKnownGoodJson, StringComparison.Ordinal))
        {
            SaveSystemTestLog.Info("SaveLoadSystem.Save skipped unchanged json");
            return;
        }

        RememberLastKnownGood(saveData, json);
        AutosaveHangTrace.Mark("Save.fullStorage bytes=" + json.Length);
        _saveStorage.Save(json);
        AutosaveHangTrace.Mark("Save.fullStorageDone");
        SaveSystemTestLog.Info("SaveLoadSystem.Save END");
    }

    private bool TrySaveVolatile()
    {
        AutosaveHangTrace.Mark("volatile.enter jsonBytes=" + (_lastKnownGoodJson?.Length ?? 0), "A");
        if (_lastKnownGood == null || string.IsNullOrEmpty(_lastKnownGoodJson))
            return false;

        Inventory inventory = _inventory.Value;
        AutosaveHangTrace.Mark("volatile.inventoryResolved null=" + (inventory == null), "A");
        if (inventory == null)
            return false;

        string worldId = GetHostWorldId();
        WorldSaveData previousWorld = SaveWorldAccess.FindWorld(_lastKnownGood, worldId);
        CreatingResourceObject[] productions = _scene?.Productions;
        InstrumentUpgrader[] instrumentUpgraders = _scene?.InstrumentUpgraders;
        EquipmentUpgrader[] equipmentUpgraders = _scene?.EquipmentUpgraders;

        ResourceAmountSave[] resources = CollectResources(inventory);
        ResourceAmountSave[] lifetime = inventory.CreateLifetimeResourcesSaveData();
        InstrumentSaveData[] instruments = CollectInstruments(inventory);
        EquipmentSaveData[] equipment = CollectEquipment(inventory);
        int enemiesKilled = Math.Max(inventory.EnemiesKilled, _lastKnownGood.EnemiesKilled);
        ProductionSaveData[] previousProductions = previousWorld?.Productions ?? _lastKnownGood.Productions;
        AutosaveHangTrace.Mark(
            "volatile.beforeProductions scene=" + (productions?.Length ?? -1) +
            " prev=" + (previousProductions?.Length ?? 0) +
            " cells=" + (inventory.Cells?.Count ?? -1),
            "B");

        ProductionSaveData[] liveProductions = CollectProductions(productions);
        ProductionSaveData[] mergedProductions = SaveDataMerger.MergePresentKeyed(
            previousWorld?.Productions ?? _lastKnownGood.Productions,
            liveProductions,
            CollectPresentProductionIds(productions),
            ProductionSaveKey);
        InstrumentUpgradeSaveData[] mergedInstrumentUpgrades = SaveDataMerger.MergePresentKeyed(
            _lastKnownGood.InstrumentUpgrades,
            CollectInstrumentUpgrades(instrumentUpgraders),
            CollectPresentInstrumentUpgradeTypes(instrumentUpgraders),
            InstrumentUpgradeSaveKey);
        EquipmentUpgradeSaveData[] mergedEquipmentUpgrades = SaveDataMerger.MergePresentKeyed(
            _lastKnownGood.EquipmentUpgrades,
            CollectEquipmentUpgrades(equipmentUpgraders),
            CollectPresentEquipmentUpgradeTypes(equipmentUpgraders),
            EquipmentUpgradeSaveKey);

        CelebrationSystem celebrationSystem = _scene?.CelebrationSystem;
        AchievementSystem achievementSystem = _scene?.AchievementSystem;
        string[] celebrations = celebrationSystem != null
            ? celebrationSystem.CreateTriggeredIdsSaveData()
            : _lastKnownGood.TriggeredCelebrationIds ?? Array.Empty<string>();
        string[] achievements = achievementSystem != null
            ? achievementSystem.CreateUnlockedIdsSaveData()
            : _lastKnownGood.TriggeredAchievementIds ?? Array.Empty<string>();

        AutosaveHangTrace.Mark(
            "volatile.beforeSame liveProd=" + (liveProductions?.Length ?? 0) +
            " mergedProd=" + (mergedProductions?.Length ?? 0) +
            " prevProd=" + (previousProductions?.Length ?? 0) +
            " instUp=" + (mergedInstrumentUpgrades?.Length ?? 0) +
            " eqUp=" + (mergedEquipmentUpgrades?.Length ?? 0) +
            " cel=" + (celebrations?.Length ?? 0) +
            " savedCel=" + (_lastKnownGood.TriggeredCelebrationIds?.Length ?? 0) +
            " ach=" + (achievements?.Length ?? 0) +
            " savedAch=" + (_lastKnownGood.TriggeredAchievementIds?.Length ?? 0),
            "C");

        bool resourcesChanged = !SameResources(resources, _lastKnownGood.Resources);
        bool lifetimeChanged = !SameResources(lifetime, _lastKnownGood.LifetimeResources);
        bool instrumentsChanged = !SameInstruments(instruments, _lastKnownGood.Instruments);
        bool equipmentChanged = !SameEquipment(equipment, _lastKnownGood.Equipment);
        bool enemiesChanged = enemiesKilled != _lastKnownGood.EnemiesKilled;
        AutosaveHangTrace.Mark("volatile.sameScalars", "D");
        bool productionsChanged = !SameProductions(
            mergedProductions,
            previousProductions);
        AutosaveHangTrace.Mark("volatile.sameProductions changed=" + productionsChanged, "D");
        bool instrumentUpgradesChanged = !SameInstrumentUpgrades(mergedInstrumentUpgrades, _lastKnownGood.InstrumentUpgrades);
        bool equipmentUpgradesChanged = !SameEquipmentUpgrades(mergedEquipmentUpgrades, _lastKnownGood.EquipmentUpgrades);
        AutosaveHangTrace.Mark("volatile.sameUpgrades", "D");
        bool celebrationsChanged = !SameIds(celebrations, _lastKnownGood.TriggeredCelebrationIds);
        AutosaveHangTrace.Mark("volatile.sameCelebrations changed=" + celebrationsChanged, "E");
        bool achievementsChanged = !SameIds(achievements, _lastKnownGood.TriggeredAchievementIds);
        bool tutorialChanged = _isTutorialCompleted && !_lastKnownGood.IsTutorialCompleted;

        AutosaveHangTrace.Mark(
            "volatile.compared resources=" + resourcesChanged + " productions=" + productionsChanged +
            " lifetime=" + lifetimeChanged + " celebrations=" + celebrationsChanged);
        if (!resourcesChanged && !lifetimeChanged && !instrumentsChanged && !equipmentChanged &&
            !enemiesChanged && !productionsChanged && !instrumentUpgradesChanged &&
            !equipmentUpgradesChanged && !celebrationsChanged && !achievementsChanged && !tutorialChanged)
        {
            SaveSystemTestLog.Info("SaveLoadSystem.Save skipped unchanged inventory/production");
            return true;
        }

        GameSaveData candidate = CopySaveShell(_lastKnownGood);
        candidate.SavedAtUtcTicks = DateTime.UtcNow.Ticks;
        candidate.IsTutorialCompleted = _lastKnownGood.IsTutorialCompleted || _isTutorialCompleted;
        if (resourcesChanged)
            candidate.Resources = resources;
        if (lifetimeChanged)
            candidate.LifetimeResources = lifetime;
        if (instrumentsChanged)
            candidate.Instruments = instruments;
        if (equipmentChanged)
            candidate.Equipment = equipment;
        if (enemiesChanged)
            candidate.EnemiesKilled = enemiesKilled;
        if (instrumentUpgradesChanged)
            candidate.InstrumentUpgrades = mergedInstrumentUpgrades;
        if (equipmentUpgradesChanged)
            candidate.EquipmentUpgrades = mergedEquipmentUpgrades;
        if (celebrationsChanged)
            candidate.TriggeredCelebrationIds = celebrations;
        if (achievementsChanged)
            candidate.TriggeredAchievementIds = achievements;

        if (productionsChanged)
            ReplaceWorldProductions(candidate, worldId, mergedProductions);

        if (SaveIntegrityGuard.IsRegression(candidate, _lastKnownGood))
        {
            SaveSystemTestLog.Error(
                $"SaveLoadSystem.Save REJECTED regression " +
                $"reason={SaveIntegrityGuard.TryGetRegressionReason(candidate, _lastKnownGood)} " +
                $"candidate={SaveSystemTestLog.SummarizeSave(candidate)} " +
                $"lastGood={SaveSystemTestLog.SummarizeSave(_lastKnownGood)}");
            return true;
        }

        AutosaveHangTrace.Mark("volatile.beforePatch");
        bool replaceRootProductions = productionsChanged &&
                                       string.Equals(worldId, SaveWorldIds.World1, StringComparison.Ordinal);
        if (!SaveJsonPatch.TryPatch(
                _lastKnownGoodJson,
                worldId,
                candidate.SavedAtUtcTicks,
                resourcesChanged ? candidate.Resources : null,
                lifetimeChanged ? candidate.LifetimeResources : null,
                enemiesChanged ? candidate.EnemiesKilled : (int?)null,
                instrumentsChanged ? candidate.Instruments : null,
                equipmentChanged ? candidate.Equipment : null,
                instrumentUpgradesChanged ? candidate.InstrumentUpgrades : null,
                equipmentUpgradesChanged ? candidate.EquipmentUpgrades : null,
                celebrationsChanged ? candidate.TriggeredCelebrationIds : null,
                achievementsChanged ? candidate.TriggeredAchievementIds : null,
                tutorialChanged ? candidate.IsTutorialCompleted : (bool?)null,
                productionsChanged ? mergedProductions : null,
                replaceRootProductions,
                out string json))
        {
            SaveSystemTestLog.Warn("SaveLoadSystem.Save volatile patch failed — disk write skipped");
            return true;
        }

        AutosaveHangTrace.Mark("volatile.afterPatch bytes=" + json.Length);
        if (string.Equals(json, _lastKnownGoodJson, StringComparison.Ordinal))
        {
            SaveSystemTestLog.Info("SaveLoadSystem.Save skipped unchanged json");
            return true;
        }

        _loadedSave = candidate;
        RememberLastKnownGood(candidate, json);
        AutosaveHangTrace.Mark("volatile.beforeStorage");
        _saveStorage.Save(json);
        AutosaveHangTrace.Mark("volatile.afterStorage");
        SaveSystemTestLog.Info("SaveLoadSystem.Save END volatile");
        return true;
    }

    public void FlushLastKnownGood()
    {
        if (string.IsNullOrEmpty(_lastKnownGoodJson) || _lastKnownGood == null)
        {
            SaveSystemTestLog.Warn("SaveLoadSystem.FlushLastKnownGood skipped — no snapshot");
            return;
        }

        if (!SavePersistPolicy.CanWriteToStorage(_saveStorage.InitState))
        {
            SaveSystemTestLog.Warn(
                $"SaveLoadSystem.FlushLastKnownGood blocked init={_saveStorage.InitState}");
            return;
        }

        SaveSystemTestLog.Warn(
            $"SaveLoadSystem.FlushLastKnownGood {SaveSystemTestLog.SummarizeSave(_lastKnownGood)}");
        _loadedSave = _lastKnownGood;
        _saveStorage.Save(_lastKnownGoodJson);
    }

    public void MarkWorldUntrusted()
    {
        if (IsWorldUntrusted)
            return;

        IsWorldUntrusted = true;
        SaveSystemTestLog.Error("SaveLoadSystem.MarkWorldUntrusted — live collect disabled");
    }

    public void MarkTutorialCompleted()
    {
        _isTutorialCompleted = true;
    }

    public string GetHostWorldId()
    {
        if (!string.IsNullOrEmpty(_hostWorldId))
            return _hostWorldId;

        if (WorldSaveIdentity.TryFindHostWorldId(out string worldId))
        {
            _hostWorldId = worldId;
            return worldId;
        }

        _hostWorldId = WorldCatalog.ResolveWorldIdFromLoadedHostScene();
        return _hostWorldId;
    }

    public string[] GetUnlockedWorldIds()
    {
        return PlanetTravelAccess.EnsureDefaultUnlocks(
            _loadedSave?.UnlockedWorldIds ?? _lastKnownGood?.UnlockedWorldIds,
            _loadedSave?.CurrentWorldId ?? GetHostWorldId());
    }

    public void GrantWorldUnlocks(IReadOnlyList<string> worldIds)
    {
        string[] current = _loadedSave?.UnlockedWorldIds ?? _lastKnownGood?.UnlockedWorldIds;
        string[] next = PlanetTravelAccess.EnsureDefaultUnlocks(
            PlanetTravelAccess.UnionWorldIds(current, worldIds),
            GetHostWorldId());

        if (PlanetTravelAccess.SameWorldIds(current, next))
            return;

        if (_loadedSave != null)
            _loadedSave.UnlockedWorldIds = next;

        if (_lastKnownGood != null)
            _lastKnownGood.UnlockedWorldIds = next;

        Save();
    }

    public bool TryResetWorldProgress(string worldId)
    {
        if (!SaveWorldAccess.CanResetWorld(worldId))
        {
            SaveSystemTestLog.Warn($"SaveLoadSystem.TryResetWorldProgress rejected world={worldId}");
            return false;
        }

        if (!SavePersistPolicy.CanWriteToStorage(_saveStorage.InitState))
        {
            SaveSystemTestLog.Warn("SaveLoadSystem.TryResetWorldProgress blocked — storage not ready");
            return false;
        }

        GameSaveData source = SaveDataMigration.Clone(_lastKnownGood ?? _loadedSave);
        if (source == null)
        {
            string pickedJson = SaveSlotSelector.PickBestJson(_saveStorage.Load(), _saveStorage.LoadPrevious());
            if (!SaveSlotSelector.TryParse(pickedJson, out source) || source == null)
            {
                SaveSystemTestLog.Warn("SaveLoadSystem.TryResetWorldProgress no save to reset");
                return false;
            }
        }

        source = SaveDataMigration.ToCurrent(source);
        if (!SaveWorldAccess.ClearWorldProgress(source, worldId))
            return false;

        source.SavedAtUtcTicks = DateTime.UtcNow.Ticks;
        ApplyIapToSaveData(source);

        string json = JsonUtility.ToJson(source);
        _loadedSave = source;
        RememberLastKnownGood(source, json);
        PersistAllowed = false;
        _saveStorage.SaveBothSlots(json);
        SaveSystemTestLog.Info(
            $"SaveLoadSystem.TryResetWorldProgress OK world={worldId} {SaveSystemTestLog.SummarizeSave(source)}");
        return true;
    }

    public bool SaveThenTravelTo(string targetWorldId)
    {
        if (string.IsNullOrEmpty(targetWorldId))
            return false;

        if (!SavePersistPolicy.CanWriteToStorage(_saveStorage.InitState))
        {
            SaveSystemTestLog.Warn("SaveLoadSystem.SaveThenTravelTo blocked — storage not ready");
            return false;
        }

        if (!PersistAllowed)
        {
            SaveSystemTestLog.Warn("SaveLoadSystem.SaveThenTravelTo blocked — persist not allowed");
            return false;
        }

        int registeredIslands = _islandRegistry?.Islands.Count ?? 0;
        if (!SavePersistPolicy.ShouldCollectLiveWorld(
                IsWorldUntrusted,
                registeredIslands,
                _lastKnownGood != null,
                !IsHostWorldMismatch))
        {
            SaveSystemTestLog.Warn("SaveLoadSystem.SaveThenTravelTo blocked — live collect skipped");
            return false;
        }

        string[] nextUnlocks = PlanetTravelAccess.EnsureDefaultUnlocks(
            PlanetTravelAccess.UnionWorldIds(
                _loadedSave?.UnlockedWorldIds ?? _lastKnownGood?.UnlockedWorldIds,
                new[] { targetWorldId }),
            targetWorldId);

        if (_loadedSave != null)
            _loadedSave.UnlockedWorldIds = nextUnlocks;

        if (_lastKnownGood != null)
            _lastKnownGood.UnlockedWorldIds = nextUnlocks;

        LiveWorldSnapshot snapshot = CollectLiveSnapshot(collectIslandProgress: true);
        GameSaveData saveData = SaveDataMerger.Merge(_lastKnownGood ?? _loadedSave, snapshot);
        ApplyIapToSaveData(saveData);
        saveData.CurrentWorldId = targetWorldId;
        saveData.UnlockedWorldIds = PlanetTravelAccess.EnsureDefaultUnlocks(
            PlanetTravelAccess.UnionWorldIds(saveData.UnlockedWorldIds, new[] { targetWorldId }),
            targetWorldId);

        if (SaveIntegrityGuard.IsRegression(saveData, _lastKnownGood))
        {
            SaveSystemTestLog.Error(
                $"SaveLoadSystem.SaveThenTravelTo REJECTED regression " +
                $"reason={SaveIntegrityGuard.TryGetRegressionReason(saveData, _lastKnownGood)} " +
                $"candidate={SaveSystemTestLog.SummarizeSave(saveData)} " +
                $"lastGood={SaveSystemTestLog.SummarizeSave(_lastKnownGood)}");
            return false;
        }

        _loadedSave = saveData;
        string json = JsonUtility.ToJson(saveData);
        RememberLastKnownGood(saveData, json);
        _saveStorage.Save(json);
        PersistAllowed = false;
        SaveSystemTestLog.Info(
            $"SaveLoadSystem.SaveThenTravelTo OK target={targetWorldId} {SaveSystemTestLog.SummarizeSave(saveData)}");
        return true;
    }

    public string ResolveRedirectSceneName()
    {
        if (_loadedSave == null)
            return WorldCatalog.World1SceneName;

        return WorldCatalog.ResolveSceneName(_loadedSave.CurrentWorldId);
    }

    public int GetCompletedGoalsCount()
    {
        return SaveWorldAccess.GetCompletedGoalsCount(_loadedSave, GetHostWorldId());
    }

    public void ApplyInventory(Inventory inventory, AllInstruments allInstruments, AllEquipment allEquipment)
    {
        if (_loadedSave == null)
            return;

        if (_loadedSave.Resources != null || _loadedSave.Instruments != null || _loadedSave.Equipment != null)
            inventory.ApplySave(
                _loadedSave.Resources,
                _loadedSave.Instruments,
                allInstruments,
                _loadedSave.Equipment,
                allEquipment);

        inventory.ApplyLifetimeSave(_loadedSave.LifetimeResources, _loadedSave.EnemiesKilled);
    }

    public void ApplyIslands()
    {
        WorldSaveData world = GetCurrentWorldSave();
        IslandSaveData[] islands = world?.Islands ?? Array.Empty<IslandSaveData>();

        int furthestMainPath = world?.FurthestMainPathIslandIndex ?? 0;
        bool hasIslandSaves = false;
        for (int i = 0; i < islands.Length; i++)
        {
            if (islands[i] != null)
            {
                hasIslandSaves = true;
                break;
            }
        }

        bool hasMainPathProgress = furthestMainPath > 0;

        SaveSystemTestLog.Info(
            $"SaveLoadSystem.ApplyIslands world={GetHostWorldId()} islandSaves={islands.Length} " +
            $"furthestMainPath={furthestMainPath} registryCount={_islandRegistry.Islands.Count}");

        if (!hasIslandSaves && !hasMainPathProgress)
        {
            SaveSystemTestLog.Info("SaveLoadSystem.ApplyIslands skipped (nothing to apply)");
            return;
        }

        foreach (var island in _islandRegistry.Islands)
        {
            IslandSaveData islandSave = FindBestSaveForIsland(islands, island);
            if (islandSave != null)
            {
                SaveSystemTestLog.Info(
                    $"SaveLoadSystem.ApplyIslands apply island={island.IslandIndex} " +
                    $"scene={island.gameObject.scene.name} state={islandSave.State} " +
                    $"unlocked={islandSave.UnlockedHexes?.Length ?? 0} openings={islandSave.OpeningHexes?.Length ?? 0}");
                island.ApplySave(islandSave);
                continue;
            }

            if (!WorldCatalog.IsKnownHostScene(island.gameObject.scene.name))
                continue;

            if (island.IslandIndex < furthestMainPath)
            {
                SaveSystemTestLog.Info($"SaveLoadSystem.ApplyIslands ForceFullyOpen island={island.IslandIndex}");
                island.ForceFullyOpen();
            }
            else if (island.IslandIndex == furthestMainPath && island.IsMainPathIsland)
            {
                SaveSystemTestLog.Info($"SaveLoadSystem.ApplyIslands MarkMainPathReached island={island.IslandIndex}");
                island.MarkMainPathReached();
            }
        }
    }

    private static IslandSaveData FindBestSaveForIsland(IslandSaveData[] saves, IslandProgressHandler island)
    {
        if (saves == null || island == null)
            return null;

        string sceneName = island.gameObject.scene.name;
        IslandSaveData best = null;
        for (int i = 0; i < saves.Length; i++)
        {
            IslandSaveData save = saves[i];
            if (!SaveDataMerger.MatchesIslandScene(save, island.IslandIndex, sceneName))
                continue;

            best = best == null ? save : SaveDataMerger.ChooseBetterIsland(best, save);
        }

        return best;
    }

    public void DisableCompletedOpeningHexagons()
    {
        var openings = UnityEngine.Object.FindObjectsByType<OpeningHexagon>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        foreach (var opening in openings)
        {
            if (opening == null)
                continue;

            var opened = opening.OpenedHexagon;
            if (opened == null)
                continue;

            if (!opened.IsLocked || opened.gameObject.activeInHierarchy)
                opening.DisableBecauseHexAlreadyOpened();
        }
    }

    public void ApplyOpeningObjectChains()
    {
        OpeningObjectChainSaveData[] chainSaves = GetCurrentWorldSave()?.OpeningObjectChains
                                                   ?? _loadedSave?.OpeningObjectChains;
        if (chainSaves == null || chainSaves.Length == 0)
            return;

        var saveById = chainSaves
            .Where(save => save != null && !string.IsNullOrEmpty(save.ChainSaveId))
            .GroupBy(save => save.ChainSaveId)
            .ToDictionary(group => group.Key, group => group.Last());

        var chains = UnityEngine.Object.FindObjectsByType<OpeningObjectChain>(
            FindObjectsInactive.Include,
            FindObjectsSortMode.None);

        foreach (var chain in chains)
        {
            if (chain == null || string.IsNullOrEmpty(chain.ChainSaveId))
                continue;

            if (saveById.TryGetValue(chain.ChainSaveId, out var chainSave))
                chain.ApplySave(chainSave);
        }
    }

    public void ApplyHarvestedOneTimeHolders()
    {
        string[] harvested = SaveWorldAccess.GetWorldScopedIds(
            _loadedSave,
            GetHostWorldId(),
            world => world.HarvestedOneTimeHolderIds);
        if (harvested.Length == 0)
            return;

        var harvestedIds = new HashSet<string>(harvested);
        var holders = UnityEngine.Object.FindObjectsByType<ResourseHolderView>(
            FindObjectsInactive.Include,
            FindObjectsSortMode.None);

        foreach (var holder in holders)
        {
            if (holder == null || !holder.IsOneTime)
                continue;

            string saveId = holder.GetSaveId();
            if (!string.IsNullOrEmpty(saveId) && harvestedIds.Contains(saveId))
                holder.ApplyHarvestedFromSave();
        }
    }

    public void ApplyKilledOneTimeEnemies()
    {
        string[] killed = SaveWorldAccess.GetWorldScopedIds(
            _loadedSave,
            GetHostWorldId(),
            world => world.KilledOneTimeEnemyIds);
        if (killed.Length == 0)
            return;

        var killedIds = new HashSet<string>(killed);
        var spawnPoints = UnityEngine.Object.FindObjectsByType<EnemySpawnPoint>(
            FindObjectsInactive.Include,
            FindObjectsSortMode.None);

        foreach (var spawnPoint in spawnPoints)
        {
            if (spawnPoint == null || !spawnPoint.IsOneTime)
                continue;

            string saveId = spawnPoint.GetSaveId();
            if (!string.IsNullOrEmpty(saveId) && killedIds.Contains(saveId))
                spawnPoint.ApplyClearedFromSave();
        }
    }

    public void ApplyArrivedNpcWorkers()
    {
        string[] arrived = SaveWorldAccess.GetWorldScopedIds(
            _loadedSave,
            GetHostWorldId(),
            world => world.ArrivedNpcWorkerIds);
        var arrivedIds = new HashSet<string>(arrived);

        var sites = UnityEngine.Object.FindObjectsByType<NpcRescueSite>(
            FindObjectsInactive.Include,
            FindObjectsSortMode.None);

        foreach (var site in sites)
        {
            if (site == null)
                continue;

            string saveId = site.GetSaveId();
            bool hasArrived = !string.IsNullOrEmpty(saveId) && arrivedIds.Contains(saveId);
            site.ApplySave(hasArrived);
        }
    }

    public void ApplyShips()
    {
        var saveById = new Dictionary<string, ShipSaveData>();
        ShipSaveData[] ships = GetCurrentWorldSave()?.Ships ?? _loadedSave?.Ships;
        if (ships != null)
        {
            foreach (var save in ships)
            {
                if (save == null || string.IsNullOrEmpty(save.SaveId) || saveById.ContainsKey(save.SaveId))
                    continue;

                saveById.Add(save.SaveId, save);
            }
        }

        var sites = UnityEngine.Object.FindObjectsByType<ShipSite>(
            FindObjectsInactive.Include,
            FindObjectsSortMode.None);

        foreach (var site in sites)
        {
            if (site == null)
                continue;

            string saveId = site.GetSaveId();
            ShipSaveData save = null;
            if (!string.IsNullOrEmpty(saveId))
                saveById.TryGetValue(saveId, out save);

            site.ApplySave(save);
        }
    }

    public ProductionSaveData GetProductionSave(string saveId)
    {
        if (string.IsNullOrEmpty(saveId) || _loadedSave == null)
            return null;

        ProductionSaveData match = FindProduction(GetCurrentWorldSave()?.Productions, saveId);
        if (match != null)
            return match;

        WorldSaveData[] worlds = SaveWorldAccess.EnumerateWorlds(_loadedSave);
        for (int i = 0; i < worlds.Length; i++)
        {
            match = FindProduction(worlds[i]?.Productions, saveId);
            if (match != null)
                return match;
        }

        return FindProduction(_loadedSave.Productions, saveId);
    }

    public InstrumentUpgradeSaveData GetInstrumentUpgradeSave(InstrumentType instrumentType)
    {
        if (_loadedSave?.InstrumentUpgrades == null)
            return null;

        return _loadedSave.InstrumentUpgrades
            .FirstOrDefault(upgrade => upgrade.InstrumentType == instrumentType);
    }

    public EquipmentUpgradeSaveData GetEquipmentUpgradeSave(EquipmentType equipmentType)
    {
        if (_loadedSave?.EquipmentUpgrades == null)
            return null;

        return _loadedSave.EquipmentUpgrades
            .FirstOrDefault(upgrade => upgrade.EquipmentType == equipmentType);
    }

    public static int CalculateElapsedSeconds(long startUtcTicks, int totalDuration)
    {
        if (startUtcTicks <= 0 || totalDuration <= 0)
            return 0;

        long elapsedTicks = DateTime.UtcNow.Ticks - startUtcTicks;
        if (elapsedTicks <= 0)
            return 0;

        int elapsedSeconds = (int)(elapsedTicks / TimeSpan.TicksPerSecond);
        return Mathf.Min(elapsedSeconds, totalDuration);
    }

    private WorldSaveData GetCurrentWorldSave()
    {
        return SaveWorldAccess.FindWorld(_loadedSave, GetHostWorldId());
    }

    private bool CanWriteToStorage()
    {
        return PersistAllowed && SavePersistPolicy.CanWriteToStorage(_saveStorage.InitState);
    }

    private void RememberLastKnownGood(GameSaveData save, string json)
    {
        _lastKnownGood = save;
        _lastKnownGoodJson = json;
    }

    private LiveWorldSnapshot CollectLiveSnapshot(bool collectIslandProgress)
    {
        CreatingResourceObject[] productions = _scene?.Productions;
        InstrumentUpgrader[] instrumentUpgraders = _scene?.InstrumentUpgraders;
        EquipmentUpgrader[] equipmentUpgraders = _scene?.EquipmentUpgraders;
        OpeningObjectChain[] chains = _scene?.OpeningChains;
        ShipSite[] shipSites = _scene?.ShipSites;
        GoalSystem goalSystem = _scene?.GoalSystem;
        CelebrationSystem celebrationSystem = _scene?.CelebrationSystem;
        AchievementSystem achievementSystem = _scene?.AchievementSystem;

        int furthestMainPathIslandIndex = collectIslandProgress
            ? GetFurthestMainPathIslandIndex()
            : 0;

        int completedGoalsCount = goalSystem != null ? goalSystem.CompletedGoalsCount : 0;

        SaveSystemTestLog.Info(
            $"SaveLoadSystem.Save context world={GetHostWorldId()} furthestMainPath={furthestMainPathIslandIndex} " +
            $"goals={completedGoalsCount} tutorialFlag={_isTutorialCompleted} " +
            $"registryIslands={_islandRegistry.Islands.Count}");

        Inventory inventory = _inventory.Value;
        return new LiveWorldSnapshot
        {
            WorldId = GetHostWorldId(),
            FurthestMainPathIslandIndex = furthestMainPathIslandIndex,
            CompletedGoalsCount = Mathf.Max(0, completedGoalsCount),
            Islands = collectIslandProgress ? CollectIslands(furthestMainPathIslandIndex) : null,
            Productions = CollectProductions(productions),
            PresentProductionSaveIds = CollectPresentProductionIds(productions),
            OpeningObjectChains = CollectOpeningObjectChains(chains),
            PresentChainSaveIds = CollectPresentChainIds(chains),
            Ships = CollectShips(shipSites),
            PresentShipSaveIds = CollectPresentShipIds(shipSites),
            HarvestedOneTimeHolderIds = CollectLiveHarvestedOneTimeHolderIds(_scene?.ResourceHolders),
            KilledOneTimeEnemyIds = CollectLiveKilledOneTimeEnemyIds(_scene?.EnemySpawnPoints),
            ArrivedNpcWorkerIds = CollectLiveArrivedNpcWorkerIds(_scene?.NpcRescueSites),
            Resources = CollectResources(inventory),
            LifetimeResources = inventory.CreateLifetimeResourcesSaveData(),
            EnemiesKilled = inventory.EnemiesKilled,
            Instruments = CollectInstruments(inventory),
            Equipment = CollectEquipment(inventory),
            InstrumentUpgrades = CollectInstrumentUpgrades(instrumentUpgraders),
            PresentInstrumentUpgradeTypes = CollectPresentInstrumentUpgradeTypes(instrumentUpgraders),
            EquipmentUpgrades = CollectEquipmentUpgrades(equipmentUpgraders),
            PresentEquipmentUpgradeTypes = CollectPresentEquipmentUpgradeTypes(equipmentUpgraders),
            TriggeredCelebrationIds = celebrationSystem != null
                ? celebrationSystem.CreateTriggeredIdsSaveData()
                : Array.Empty<string>(),
            TriggeredAchievementIds = achievementSystem != null
                ? achievementSystem.CreateUnlockedIdsSaveData()
                : Array.Empty<string>(),
            UnlockedWorldIds = _loadedSave?.UnlockedWorldIds ?? _lastKnownGood?.UnlockedWorldIds,
            IsTutorialCompleted = _isTutorialCompleted
        };
    }

    private void ApplyIapToSaveData(GameSaveData saveData)
    {
        if (_iapSaveBridge != null)
            _iapSaveBridge.WriteToSaveData(saveData);
        else if (_lastKnownGood != null)
        {
            saveData.AdsRemoved = _lastKnownGood.AdsRemoved;
            saveData.StarterPackPurchased = _lastKnownGood.StarterPackPurchased;
            saveData.PurchasedIslandOfferIndices = _lastKnownGood.PurchasedIslandOfferIndices;
            saveData.ShownIslandOfferIndices = _lastKnownGood.ShownIslandOfferIndices;
        }
        else if (_loadedSave != null)
        {
            saveData.AdsRemoved = _loadedSave.AdsRemoved;
            saveData.StarterPackPurchased = _loadedSave.StarterPackPurchased;
            saveData.PurchasedIslandOfferIndices = _loadedSave.PurchasedIslandOfferIndices;
            saveData.ShownIslandOfferIndices = _loadedSave.ShownIslandOfferIndices;
        }

        if (_lastKnownGood == null)
            return;

        saveData.AdsRemoved = saveData.AdsRemoved || _lastKnownGood.AdsRemoved;
        saveData.StarterPackPurchased = saveData.StarterPackPurchased || _lastKnownGood.StarterPackPurchased;
        saveData.PurchasedIslandOfferIndices = UnionIntIds(
            saveData.PurchasedIslandOfferIndices,
            _lastKnownGood.PurchasedIslandOfferIndices);
        saveData.ShownIslandOfferIndices = UnionIntIds(
            saveData.ShownIslandOfferIndices,
            _lastKnownGood.ShownIslandOfferIndices);
    }

    private int GetFurthestMainPathIslandIndex()
    {
        int furthest = 0;
        foreach (var island in _islandRegistry.Islands)
        {
            if (island == null || !island.IsMainPathIsland || !island.IsMainPathReached)
                continue;

            if (!IsHostWorldIsland(island))
                continue;

            if (island.IslandIndex > furthest)
                furthest = island.IslandIndex;
        }

        return furthest;
    }

    private IslandSaveData[] CollectIslands(int furthestMainPathIslandIndex)
    {
        var result = new List<IslandSaveData>();

        foreach (var island in _islandRegistry.Islands)
        {
            IslandSaveData save = island.CreateSaveData();
            if (save != null)
            {
                save.SceneName = SceneNameForIslandSave(island);
                result.Add(save);
                continue;
            }

            if (!IsHostWorldIsland(island))
                continue;

            if (island.IsFullyOpened ||
                (island.IslandIndex < furthestMainPathIslandIndex && island.IsMainPathReached))
            {
                result.Add(new IslandSaveData
                {
                    IslandIndex = island.IslandIndex,
                    State = IslandUnlockState.FullyOpened,
                    UnlockedHexes = Array.Empty<HexCoordSave>(),
                    OpeningHexes = Array.Empty<OpeningHexSaveData>()
                });
            }
        }

        return result.ToArray();
    }

    private static bool IsHostWorldIsland(IslandProgressHandler island)
    {
        return island != null && WorldCatalog.IsKnownHostScene(island.gameObject.scene.name);
    }

    private static string SceneNameForIslandSave(IslandProgressHandler island)
    {
        if (island == null)
            return null;

        string sceneName = island.gameObject.scene.name;
        return WorldCatalog.IsKnownHostScene(sceneName) ? null : sceneName;
    }

    private static ResourceAmountSave[] CollectResources(Inventory inventory)
    {
        if (inventory?.Cells == null || inventory.Cells.Count == 0)
            return Array.Empty<ResourceAmountSave>();

        int count = 0;
        for (int i = 0; i < inventory.Cells.Count; i++)
        {
            if (inventory.Cells[i] != null && inventory.Cells[i].Amount > 0)
                count++;
        }

        if (count == 0)
            return Array.Empty<ResourceAmountSave>();

        var result = new ResourceAmountSave[count];
        int index = 0;
        for (int i = 0; i < inventory.Cells.Count; i++)
        {
            ResourceCell cell = inventory.Cells[i];
            if (cell == null || cell.Amount <= 0)
                continue;

            result[index++] = new ResourceAmountSave { Type = cell.ResourseType, Amount = cell.Amount };
        }

        return result;
    }

    private static InstrumentSaveData[] CollectInstruments(Inventory inventory)
    {
        if (inventory?.Instruments == null || inventory.Instruments.Count == 0)
            return Array.Empty<InstrumentSaveData>();

        var result = new InstrumentSaveData[inventory.Instruments.Count];
        for (int i = 0; i < inventory.Instruments.Count; i++)
        {
            Instrument instrument = inventory.Instruments[i];
            result[i] = new InstrumentSaveData
            {
                Type = instrument.Type,
                Level = instrument.Level
            };
        }

        return result;
    }

    private static EquipmentSaveData[] CollectEquipment(Inventory inventory)
    {
        if (inventory?.Equipment == null || inventory.Equipment.Count == 0)
            return Array.Empty<EquipmentSaveData>();

        int count = 0;
        for (int i = 0; i < inventory.Equipment.Count; i++)
        {
            Equipment item = inventory.Equipment[i];
            if (item != null && item.Level > 0)
                count++;
        }

        if (count == 0)
            return Array.Empty<EquipmentSaveData>();

        var result = new EquipmentSaveData[count];
        int index = 0;
        for (int i = 0; i < inventory.Equipment.Count; i++)
        {
            Equipment item = inventory.Equipment[i];
            if (item == null || item.Level <= 0)
                continue;

            result[index++] = new EquipmentSaveData
            {
                Type = item.Type,
                Level = item.Level
            };
        }

        return result;
    }

    private static ProductionSaveData[] CollectProductions(IReadOnlyList<CreatingResourceObject> productions)
    {
        if (productions == null || productions.Count == 0)
            return Array.Empty<ProductionSaveData>();

        int count = 0;
        for (int i = 0; i < productions.Count; i++)
        {
            ProductionSaveData save = productions[i] != null ? productions[i].CreateSaveData() : null;
            if (save != null && save.State != TimedProcessState.Idle)
                count++;
        }

        if (count == 0)
            return Array.Empty<ProductionSaveData>();

        var result = new ProductionSaveData[count];
        int index = 0;
        for (int i = 0; i < productions.Count; i++)
        {
            ProductionSaveData save = productions[i] != null ? productions[i].CreateSaveData() : null;
            if (save == null || save.State == TimedProcessState.Idle)
                continue;

            result[index++] = save;
        }

        return result;
    }

    private static string[] CollectPresentProductionIds(IReadOnlyList<CreatingResourceObject> productions)
    {
        if (productions == null || productions.Count == 0)
            return Array.Empty<string>();

        var ids = new HashSet<string>();
        for (int i = 0; i < productions.Count; i++)
        {
            string id = productions[i] != null ? productions[i].GetSaveId() : null;
            if (!string.IsNullOrEmpty(id))
                ids.Add(id);
        }

        return ToArray(ids);
    }

    private static InstrumentUpgradeSaveData[] CollectInstrumentUpgrades(IReadOnlyList<InstrumentUpgrader> upgraders)
    {
        if (upgraders == null || upgraders.Count == 0)
            return Array.Empty<InstrumentUpgradeSaveData>();

        int count = 0;
        for (int i = 0; i < upgraders.Count; i++)
        {
            InstrumentUpgradeSaveData save = upgraders[i] != null ? upgraders[i].CreateSaveData() : null;
            if (save != null && save.State != TimedProcessState.Idle)
                count++;
        }

        if (count == 0)
            return Array.Empty<InstrumentUpgradeSaveData>();

        var result = new InstrumentUpgradeSaveData[count];
        int index = 0;
        for (int i = 0; i < upgraders.Count; i++)
        {
            InstrumentUpgradeSaveData save = upgraders[i] != null ? upgraders[i].CreateSaveData() : null;
            if (save == null || save.State == TimedProcessState.Idle)
                continue;

            result[index++] = save;
        }

        return result;
    }

    private static string[] CollectPresentInstrumentUpgradeTypes(IReadOnlyList<InstrumentUpgrader> upgraders)
    {
        if (upgraders == null || upgraders.Count == 0)
            return Array.Empty<string>();

        var ids = new HashSet<string>();
        for (int i = 0; i < upgraders.Count; i++)
        {
            if (upgraders[i] == null)
                continue;

            ids.Add(upgraders[i].InstrumentType.ToString());
        }

        return ToArray(ids);
    }

    private static EquipmentUpgradeSaveData[] CollectEquipmentUpgrades(IReadOnlyList<EquipmentUpgrader> upgraders)
    {
        if (upgraders == null || upgraders.Count == 0)
            return Array.Empty<EquipmentUpgradeSaveData>();

        int count = 0;
        for (int i = 0; i < upgraders.Count; i++)
        {
            EquipmentUpgradeSaveData save = upgraders[i] != null ? upgraders[i].CreateSaveData() : null;
            if (save != null && save.State != TimedProcessState.Idle)
                count++;
        }

        if (count == 0)
            return Array.Empty<EquipmentUpgradeSaveData>();

        var result = new EquipmentUpgradeSaveData[count];
        int index = 0;
        for (int i = 0; i < upgraders.Count; i++)
        {
            EquipmentUpgradeSaveData save = upgraders[i] != null ? upgraders[i].CreateSaveData() : null;
            if (save == null || save.State == TimedProcessState.Idle)
                continue;

            result[index++] = save;
        }

        return result;
    }

    private static string[] CollectPresentEquipmentUpgradeTypes(IReadOnlyList<EquipmentUpgrader> upgraders)
    {
        if (upgraders == null || upgraders.Count == 0)
            return Array.Empty<string>();

        var ids = new HashSet<string>();
        for (int i = 0; i < upgraders.Count; i++)
        {
            if (upgraders[i] == null)
                continue;

            ids.Add(upgraders[i].EquipmentType.ToString());
        }

        return ToArray(ids);
    }

    private static OpeningObjectChainSaveData[] CollectOpeningObjectChains(IReadOnlyList<OpeningObjectChain> chains)
    {
        if (chains == null || chains.Count == 0)
            return Array.Empty<OpeningObjectChainSaveData>();

        int count = 0;
        for (int i = 0; i < chains.Count; i++)
        {
            if (chains[i] != null && chains[i].CreateSaveData() != null)
                count++;
        }

        if (count == 0)
            return Array.Empty<OpeningObjectChainSaveData>();

        var result = new OpeningObjectChainSaveData[count];
        int index = 0;
        for (int i = 0; i < chains.Count; i++)
        {
            OpeningObjectChainSaveData save = chains[i] != null ? chains[i].CreateSaveData() : null;
            if (save == null)
                continue;

            result[index++] = save;
        }

        return result;
    }

    private static string[] CollectPresentChainIds(IReadOnlyList<OpeningObjectChain> chains)
    {
        if (chains == null || chains.Count == 0)
            return Array.Empty<string>();

        var ids = new HashSet<string>();
        for (int i = 0; i < chains.Count; i++)
        {
            string id = chains[i] != null ? chains[i].ChainSaveId : null;
            if (!string.IsNullOrEmpty(id))
                ids.Add(id);
        }

        return ToArray(ids);
    }

    private static ShipSaveData[] CollectShips(IReadOnlyList<ShipSite> sites)
    {
        if (sites == null || sites.Count == 0)
            return Array.Empty<ShipSaveData>();

        int count = 0;
        for (int i = 0; i < sites.Count; i++)
        {
            if (sites[i] != null && sites[i].CreateSaveData() != null)
                count++;
        }

        if (count == 0)
            return Array.Empty<ShipSaveData>();

        var result = new ShipSaveData[count];
        int index = 0;
        for (int i = 0; i < sites.Count; i++)
        {
            ShipSaveData save = sites[i] != null ? sites[i].CreateSaveData() : null;
            if (save == null)
                continue;

            result[index++] = save;
        }

        return result;
    }

    private static string[] CollectPresentShipIds(IReadOnlyList<ShipSite> sites)
    {
        if (sites == null || sites.Count == 0)
            return Array.Empty<string>();

        var ids = new HashSet<string>();
        for (int i = 0; i < sites.Count; i++)
        {
            string id = sites[i] != null ? sites[i].GetSaveId() : null;
            if (!string.IsNullOrEmpty(id))
                ids.Add(id);
        }

        return ToArray(ids);
    }

    private static string[] CollectLiveHarvestedOneTimeHolderIds(ResourseHolderView[] holders)
    {
        var ids = new HashSet<string>();
        if (holders == null)
            return Array.Empty<string>();

        foreach (var holder in holders)
        {
            if (holder == null || !holder.IsOneTime || !holder.IsHarvested)
                continue;

            string saveId = holder.GetSaveId();
            if (!string.IsNullOrEmpty(saveId))
                ids.Add(saveId);
        }

        return ToArray(ids);
    }

    private static string[] CollectLiveKilledOneTimeEnemyIds(EnemySpawnPoint[] spawnPoints)
    {
        var ids = new HashSet<string>();
        if (spawnPoints == null)
            return Array.Empty<string>();

        foreach (var spawnPoint in spawnPoints)
        {
            if (spawnPoint == null || !spawnPoint.IsOneTime || !spawnPoint.IsCleared)
                continue;

            string saveId = spawnPoint.GetSaveId();
            if (!string.IsNullOrEmpty(saveId))
                ids.Add(saveId);
        }

        return ToArray(ids);
    }

    private static string[] CollectLiveArrivedNpcWorkerIds(NpcRescueSite[] sites)
    {
        var ids = new HashSet<string>();
        if (sites == null)
            return Array.Empty<string>();

        foreach (var site in sites)
        {
            if (site == null || !site.HasArrived)
                continue;

            string saveId = site.GetSaveId();
            if (!string.IsNullOrEmpty(saveId))
                ids.Add(saveId);
        }

        return ToArray(ids);
    }

    private static ProductionSaveData FindProduction(ProductionSaveData[] productions, string saveId)
    {
        if (productions == null)
            return null;

        return productions.FirstOrDefault(production => production != null && production.SaveId == saveId);
    }

    private static int[] UnionIntIds(int[] first, int[] second)
    {
        var ids = new HashSet<int>();
        if (first != null)
        {
            for (int i = 0; i < first.Length; i++)
                ids.Add(first[i]);
        }

        if (second != null)
        {
            for (int i = 0; i < second.Length; i++)
                ids.Add(second[i]);
        }

        if (ids.Count == 0)
            return Array.Empty<int>();

        int[] result = new int[ids.Count];
        ids.CopyTo(result);
        Array.Sort(result);
        return result;
    }

    private static string[] ToArray(HashSet<string> ids)
    {
        if (ids == null || ids.Count == 0)
            return Array.Empty<string>();

        var result = new string[ids.Count];
        ids.CopyTo(result);
        return result;
    }

    private static readonly Func<ProductionSaveData, string> ProductionSaveKey = static production => production.SaveId;
    private static readonly Func<InstrumentUpgradeSaveData, string> InstrumentUpgradeSaveKey =
        static upgrade => upgrade.InstrumentType.ToString();
    private static readonly Func<EquipmentUpgradeSaveData, string> EquipmentUpgradeSaveKey =
        static upgrade => upgrade.EquipmentType.ToString();

    private static void ReplaceWorldProductions(GameSaveData save, string worldId, ProductionSaveData[] productions)
    {
        if (save.Worlds == null || save.Worlds.Length == 0)
        {
            save.Productions = productions;
            return;
        }

        var worlds = new WorldSaveData[save.Worlds.Length];
        for (int i = 0; i < save.Worlds.Length; i++)
        {
            WorldSaveData world = save.Worlds[i];
            if (world == null || !string.Equals(world.WorldId, worldId, StringComparison.Ordinal))
            {
                worlds[i] = world;
                continue;
            }

            worlds[i] = new WorldSaveData
            {
                WorldId = world.WorldId,
                FurthestMainPathIslandIndex = world.FurthestMainPathIslandIndex,
                CompletedGoalsCount = world.CompletedGoalsCount,
                Islands = world.Islands,
                Productions = productions,
                OpeningObjectChains = world.OpeningObjectChains,
                Ships = world.Ships,
                HarvestedOneTimeHolderIds = world.HarvestedOneTimeHolderIds,
                KilledOneTimeEnemyIds = world.KilledOneTimeEnemyIds,
                ArrivedNpcWorkerIds = world.ArrivedNpcWorkerIds
            };
        }

        save.Worlds = worlds;
        if (string.Equals(worldId, SaveWorldIds.World1, StringComparison.Ordinal))
            SaveWorldAccess.MirrorWorld1ToRoot(save);
    }

    private static GameSaveData CopySaveShell(GameSaveData source)
    {
        return new GameSaveData
        {
            SchemaVersion = source.SchemaVersion,
            CurrentWorldId = source.CurrentWorldId,
            UnlockedWorldIds = source.UnlockedWorldIds,
            Worlds = source.Worlds,
            SavedAtUtcTicks = source.SavedAtUtcTicks,
            FurthestMainPathIslandIndex = source.FurthestMainPathIslandIndex,
            IsTutorialCompleted = source.IsTutorialCompleted,
            CompletedGoalsCount = source.CompletedGoalsCount,
            TriggeredCelebrationIds = source.TriggeredCelebrationIds,
            TriggeredAchievementIds = source.TriggeredAchievementIds,
            Resources = source.Resources,
            LifetimeResources = source.LifetimeResources,
            EnemiesKilled = source.EnemiesKilled,
            Instruments = source.Instruments,
            Equipment = source.Equipment,
            Productions = source.Productions,
            InstrumentUpgrades = source.InstrumentUpgrades,
            EquipmentUpgrades = source.EquipmentUpgrades,
            Islands = source.Islands,
            OpeningObjectChains = source.OpeningObjectChains,
            Ships = source.Ships,
            HarvestedOneTimeHolderIds = source.HarvestedOneTimeHolderIds,
            KilledOneTimeEnemyIds = source.KilledOneTimeEnemyIds,
            ArrivedNpcWorkerIds = source.ArrivedNpcWorkerIds,
            AdsRemoved = source.AdsRemoved,
            StarterPackPurchased = source.StarterPackPurchased,
            PurchasedIslandOfferIndices = source.PurchasedIslandOfferIndices,
            ShownIslandOfferIndices = source.ShownIslandOfferIndices
        };
    }

    private static bool SameResources(ResourceAmountSave[] left, ResourceAmountSave[] right)
    {
        int leftCount = left?.Length ?? 0;
        int rightCount = right?.Length ?? 0;
        if (leftCount != rightCount)
            return false;

        for (int i = 0; i < leftCount; i++)
        {
            if (left[i].Type != right[i].Type || left[i].Amount != right[i].Amount)
                return false;
        }

        return true;
    }

    private static bool SameInstruments(InstrumentSaveData[] left, InstrumentSaveData[] right)
    {
        int leftCount = left?.Length ?? 0;
        int rightCount = right?.Length ?? 0;
        if (leftCount != rightCount)
            return false;

        for (int i = 0; i < leftCount; i++)
        {
            if (left[i] == null || right[i] == null)
                return left[i] == right[i];

            if (left[i].Type != right[i].Type || left[i].Level != right[i].Level)
                return false;
        }

        return true;
    }

    private static bool SameEquipment(EquipmentSaveData[] left, EquipmentSaveData[] right)
    {
        int leftCount = left?.Length ?? 0;
        int rightCount = right?.Length ?? 0;
        if (leftCount != rightCount)
            return false;

        for (int i = 0; i < leftCount; i++)
        {
            if (left[i] == null || right[i] == null)
                return left[i] == right[i];

            if (left[i].Type != right[i].Type || left[i].Level != right[i].Level)
                return false;
        }

        return true;
    }

    private static bool SameProductions(ProductionSaveData[] left, ProductionSaveData[] right)
    {
        int leftCount = left?.Length ?? 0;
        int rightCount = right?.Length ?? 0;
        if (leftCount != rightCount)
            return false;

        for (int i = 0; i < leftCount; i++)
        {
            ProductionSaveData candidate = left[i];
            if (candidate == null)
                return false;

            bool found = false;
            for (int j = 0; j < rightCount; j++)
            {
                ProductionSaveData saved = right[j];
                if (saved == null || !string.Equals(saved.SaveId, candidate.SaveId, StringComparison.Ordinal))
                    continue;

                found = saved.State == candidate.State &&
                        saved.ResourceCount == candidate.ResourceCount &&
                        saved.TotalDuration == candidate.TotalDuration &&
                        saved.StartUtcTicks == candidate.StartUtcTicks;
                break;
            }

            if (!found)
                return false;
        }

        return true;
    }

    private static bool SameInstrumentUpgrades(InstrumentUpgradeSaveData[] left, InstrumentUpgradeSaveData[] right)
    {
        int leftCount = left?.Length ?? 0;
        int rightCount = right?.Length ?? 0;
        if (leftCount != rightCount)
            return false;

        for (int i = 0; i < leftCount; i++)
        {
            InstrumentUpgradeSaveData candidate = left[i];
            if (candidate == null)
                return false;

            bool found = false;
            for (int j = 0; j < rightCount; j++)
            {
                InstrumentUpgradeSaveData saved = right[j];
                if (saved == null || saved.InstrumentType != candidate.InstrumentType)
                    continue;

                found = saved.State == candidate.State &&
                        saved.TargetLevel == candidate.TargetLevel &&
                        saved.TotalDuration == candidate.TotalDuration &&
                        saved.StartUtcTicks == candidate.StartUtcTicks;
                break;
            }

            if (!found)
                return false;
        }

        return true;
    }

    private static bool SameEquipmentUpgrades(EquipmentUpgradeSaveData[] left, EquipmentUpgradeSaveData[] right)
    {
        int leftCount = left?.Length ?? 0;
        int rightCount = right?.Length ?? 0;
        if (leftCount != rightCount)
            return false;

        for (int i = 0; i < leftCount; i++)
        {
            EquipmentUpgradeSaveData candidate = left[i];
            if (candidate == null)
                return false;

            bool found = false;
            for (int j = 0; j < rightCount; j++)
            {
                EquipmentUpgradeSaveData saved = right[j];
                if (saved == null || saved.EquipmentType != candidate.EquipmentType)
                    continue;

                found = saved.State == candidate.State &&
                        saved.TargetLevel == candidate.TargetLevel &&
                        saved.TotalDuration == candidate.TotalDuration &&
                        saved.StartUtcTicks == candidate.StartUtcTicks;
                break;
            }

            if (!found)
                return false;
        }

        return true;
    }

    private static bool SameIds(string[] left, string[] right)
    {
        int leftCount = left?.Length ?? 0;
        int rightCount = right?.Length ?? 0;
        if (leftCount != rightCount)
            return false;

        if (leftCount == 0)
            return true;

        var seen = new HashSet<string>(left);
        if (seen.Count != leftCount)
            return false;

        for (int i = 0; i < rightCount; i++)
        {
            if (!seen.Contains(right[i]))
                return false;
        }

        return true;
    }
}

public class LiveSceneSaveCache
{
    public CreatingResourceObject[] Productions;
    public InstrumentUpgrader[] InstrumentUpgraders;
    public EquipmentUpgrader[] EquipmentUpgraders;
    public OpeningObjectChain[] OpeningChains;
    public ShipSite[] ShipSites;
    public NpcRescueSite[] NpcRescueSites;
    public ResourseHolderView[] ResourceHolders;
    public EnemySpawnPoint[] EnemySpawnPoints;
    public GoalSystem GoalSystem;
    public CelebrationSystem CelebrationSystem;
    public AchievementSystem AchievementSystem;
    public string HostWorldId;
}

public static class SaveJsonPatch
{
    public static bool TryPatch(
        string json,
        string worldId,
        long savedAtUtcTicks,
        ResourceAmountSave[] resources,
        ResourceAmountSave[] lifetimeResources,
        int? enemiesKilled,
        InstrumentSaveData[] instruments,
        EquipmentSaveData[] equipment,
        InstrumentUpgradeSaveData[] instrumentUpgrades,
        EquipmentUpgradeSaveData[] equipmentUpgrades,
        string[] celebrations,
        string[] achievements,
        bool? tutorialCompleted,
        ProductionSaveData[] productions,
        bool replaceRootProductions,
        out string result)
    {
        result = null;
        if (string.IsNullOrEmpty(json))
            return false;

        var fields = new FieldValue[]
        {
            new FieldValue("SavedAtUtcTicks", savedAtUtcTicks.ToString(System.Globalization.CultureInfo.InvariantCulture)),
            resources != null ? new FieldValue("Resources", ArrayJson.Resources(resources)) : default,
            lifetimeResources != null ? new FieldValue("LifetimeResources", ArrayJson.Resources(lifetimeResources)) : default,
            enemiesKilled.HasValue ? new FieldValue("EnemiesKilled", enemiesKilled.Value.ToString(System.Globalization.CultureInfo.InvariantCulture)) : default,
            instruments != null ? new FieldValue("Instruments", ArrayJson.Instruments(instruments)) : default,
            equipment != null ? new FieldValue("Equipment", ArrayJson.Equipment(equipment)) : default,
            instrumentUpgrades != null ? new FieldValue("InstrumentUpgrades", ArrayJson.InstrumentUpgrades(instrumentUpgrades)) : default,
            equipmentUpgrades != null ? new FieldValue("EquipmentUpgrades", ArrayJson.EquipmentUpgrades(equipmentUpgrades)) : default,
            celebrations != null ? new FieldValue("TriggeredCelebrationIds", ArrayJson.Strings(celebrations)) : default,
            achievements != null ? new FieldValue("TriggeredAchievementIds", ArrayJson.Strings(achievements)) : default,
            tutorialCompleted.HasValue ? new FieldValue("IsTutorialCompleted", tutorialCompleted.Value ? "true" : "false") : default,
            replaceRootProductions ? new FieldValue("Productions", ArrayJson.Productions(productions)) : default
        };

        string worldProductions = productions != null ? ArrayJson.Productions(productions) : null;
        var sb = new System.Text.StringBuilder(json.Length + 128);
        bool[] written = new bool[fields.Length];
        int depth = 0;
        bool inTargetWorld = false;
        bool sawTargetWorld = false;
        int targetWorldDepth = -1;
        bool worldProductionsWritten = worldProductions == null;
        int i = 0;
        int nextReport = 262144;

        while (i < json.Length)
        {
            if (i >= nextReport)
            {
                AutosaveHangTrace.Mark("patch.index=" + i + " depth=" + depth + " of " + json.Length);
                nextReport += 262144;
            }

            char current = json[i];
            if (current == '"')
            {
                int quoteEnd = EndOfString(json, i);
                if (quoteEnd < 0)
                    return false;

                int afterKey = SkipSpace(json, quoteEnd + 1);
                bool isKey = afterKey < json.Length && json[afterKey] == ':';
                if (isKey && depth == 1)
                {
                    int fieldIndex = MatchField(json, i + 1, quoteEnd, fields);
                    if (fieldIndex >= 0 && !written[fieldIndex])
                    {
                        sb.Append(json, i, afterKey - i + 1);
                        int valueStart = SkipSpace(json, afterKey + 1);
                        int valueEnd = EndOfValue(json, valueStart);
                        if (valueEnd <= valueStart)
                            return false;

                        sb.Append(fields[fieldIndex].Value);
                        written[fieldIndex] = true;
                        i = valueEnd;
                        continue;
                    }
                }

                if (isKey && inTargetWorld && depth == targetWorldDepth && !worldProductionsWritten &&
                    KeyEquals(json, i + 1, quoteEnd, "Productions"))
                {
                    sb.Append(json, i, afterKey - i + 1);
                    int valueStart = SkipSpace(json, afterKey + 1);
                    int valueEnd = EndOfValue(json, valueStart);
                    if (valueEnd <= valueStart)
                        return false;

                    sb.Append(worldProductions);
                    worldProductionsWritten = true;
                    i = valueEnd;
                    continue;
                }

                if (isKey && !inTargetWorld && KeyEquals(json, i + 1, quoteEnd, "WorldId"))
                {
                    int valueStart = SkipSpace(json, afterKey + 1);
                    if (valueStart < json.Length && json[valueStart] == '"' &&
                        StringValueEquals(json, valueStart, worldId))
                    {
                        inTargetWorld = true;
                        sawTargetWorld = true;
                        targetWorldDepth = depth;
                    }
                }

                int length = quoteEnd - i + 1;
                if (length <= 0)
                    return false;

                sb.Append(json, i, length);
                i = quoteEnd + 1;
                continue;
            }

            if (current == '{' || current == '[')
            {
                depth++;
                sb.Append(current);
                i++;
                continue;
            }

            if (current == '}' || current == ']')
            {
                if (current == '}' && inTargetWorld && depth == targetWorldDepth && !worldProductionsWritten)
                {
                    sb.Append(",\"Productions\":");
                    sb.Append(worldProductions);
                    worldProductionsWritten = true;
                }

                if (current == '}' && depth == 1)
                    AppendMissingFields(sb, fields, written);

                if (inTargetWorld && depth == targetWorldDepth)
                    inTargetWorld = false;

                depth--;
                sb.Append(current);
                i++;
                continue;
            }

            sb.Append(current);
            i++;
        }

        if (depth != 0 || (sawTargetWorld && !worldProductionsWritten))
            return false;

        for (int fieldIndex = 0; fieldIndex < fields.Length; fieldIndex++)
        {
            if (fields[fieldIndex].Value != null && !written[fieldIndex])
                return false;
        }

        result = sb.ToString();
        return true;
    }

    private static void AppendMissingFields(System.Text.StringBuilder sb, FieldValue[] fields, bool[] written)
    {
        for (int i = 0; i < fields.Length; i++)
        {
            if (fields[i].Value == null || written[i])
                continue;

            sb.Append(",\"").Append(fields[i].Name).Append("\":").Append(fields[i].Value);
            written[i] = true;
        }
    }

    private static int MatchField(string json, int nameStart, int nameEnd, FieldValue[] fields)
    {
        for (int i = 0; i < fields.Length; i++)
        {
            if (fields[i].Value != null && KeyEquals(json, nameStart, nameEnd, fields[i].Name))
                return i;
        }

        return -1;
    }

    private static bool KeyEquals(string json, int start, int end, string name)
    {
        if (end - start != name.Length)
            return false;

        return string.CompareOrdinal(json, start, name, 0, name.Length) == 0;
    }

    private static bool StringValueEquals(string json, int quoteStart, string value)
    {
        int quoteEnd = EndOfString(json, quoteStart);
        if (quoteEnd < 0 || string.IsNullOrEmpty(value))
            return false;

        int start = quoteStart + 1;
        if (quoteEnd - start != value.Length)
            return false;

        return string.CompareOrdinal(json, start, value, 0, value.Length) == 0;
    }

    private static int EndOfString(string json, int quoteStart)
    {
        for (int i = quoteStart + 1; i < json.Length; i++)
        {
            if (json[i] == '\\')
            {
                if (i + 1 >= json.Length)
                    return -1;

                i++;
                continue;
            }

            if (json[i] == '"')
                return i;
        }

        return -1;
    }

    private static int EndOfValue(string json, int start)
    {
        if (start >= json.Length)
            return -1;

        char current = json[start];
        if (current == '"')
        {
            int quoteEnd = EndOfString(json, start);
            return quoteEnd < 0 ? -1 : quoteEnd + 1;
        }

        if (current != '{' && current != '[')
        {
            int end = start;
            while (end < json.Length && json[end] != ',' && json[end] != '}' && json[end] != ']')
                end++;

            return end > start ? end : -1;
        }

        int depth = 0;
        for (int i = start; i < json.Length; i++)
        {
            char token = json[i];
            if (token == '"')
            {
                int quoteEnd = EndOfString(json, i);
                if (quoteEnd < 0 || quoteEnd <= i)
                    return -1;

                i = quoteEnd;
                continue;
            }

            if (token == '{' || token == '[')
                depth++;
            else if (token == '}' || token == ']')
            {
                depth--;
                if (depth == 0)
                    return i + 1;
            }
        }

        return -1;
    }

    private static int SkipSpace(string json, int index)
    {
        while (index < json.Length && char.IsWhiteSpace(json[index]))
            index++;

        return index;
    }

    private struct FieldValue
    {
        public readonly string Name;
        public readonly string Value;

        public FieldValue(string name, string value)
        {
            Name = name;
            Value = value;
        }
    }
}

static class ArrayJson
{
    public static string Resources(ResourceAmountSave[] items) =>
        Items(JsonUtility.ToJson(new ResourceAmountList { Items = items ?? System.Array.Empty<ResourceAmountSave>() }));

    public static string Instruments(InstrumentSaveData[] items) =>
        Items(JsonUtility.ToJson(new InstrumentList { Items = items ?? System.Array.Empty<InstrumentSaveData>() }));

    public static string Equipment(EquipmentSaveData[] items) =>
        Items(JsonUtility.ToJson(new EquipmentList { Items = items ?? System.Array.Empty<EquipmentSaveData>() }));

    public static string InstrumentUpgrades(InstrumentUpgradeSaveData[] items) =>
        Items(JsonUtility.ToJson(new InstrumentUpgradeList { Items = items ?? System.Array.Empty<InstrumentUpgradeSaveData>() }));

    public static string EquipmentUpgrades(EquipmentUpgradeSaveData[] items) =>
        Items(JsonUtility.ToJson(new EquipmentUpgradeList { Items = items ?? System.Array.Empty<EquipmentUpgradeSaveData>() }));

    public static string Productions(ProductionSaveData[] items) =>
        Items(JsonUtility.ToJson(new ProductionList { Items = items ?? System.Array.Empty<ProductionSaveData>() }));

    public static string Strings(string[] items) =>
        Items(JsonUtility.ToJson(new StringList { Items = items ?? System.Array.Empty<string>() }));

    private static string Items(string json)
    {
        int colon = json.IndexOf(':');
        if (colon < 0 || json.Length < 2 || json[json.Length - 1] != '}')
            return "[]";

        return json.Substring(colon + 1, json.Length - colon - 2);
    }

    [Serializable]
    private class ResourceAmountList { public ResourceAmountSave[] Items; }

    [Serializable]
    private class InstrumentList { public InstrumentSaveData[] Items; }

    [Serializable]
    private class EquipmentList { public EquipmentSaveData[] Items; }

    [Serializable]
    private class InstrumentUpgradeList { public InstrumentUpgradeSaveData[] Items; }

    [Serializable]
    private class EquipmentUpgradeList { public EquipmentUpgradeSaveData[] Items; }

    [Serializable]
    private class ProductionList { public ProductionSaveData[] Items; }

    [Serializable]
    private class StringList { public string[] Items; }
}

static class AutosaveHangTrace
{
    private static System.IO.StreamWriter _writer;

    public static void Mark(string step, string hypothesisId = "")
    {
        if (_writer == null)
        {
            string directory = System.IO.Path.GetFullPath(System.IO.Path.Combine(Application.dataPath, "../Logs"));
            System.IO.Directory.CreateDirectory(directory);
            string path = System.IO.Path.Combine(directory, "autosave-hang.txt");
            var stream = new System.IO.FileStream(
                path,
                System.IO.FileMode.Create,
                System.IO.FileAccess.Write,
                System.IO.FileShare.ReadWrite);
            _writer = new System.IO.StreamWriter(stream) { AutoFlush = true };
        }

        _writer.WriteLine(System.DateTime.UtcNow.ToString("HH:mm:ss.fff") + " " + step);

        // #region agent log
        try
        {
            string debugPath = System.IO.Path.GetFullPath(
                System.IO.Path.Combine(Application.dataPath, "../debug-50c8ad.log"));
            string escaped = step.Replace("\\", "\\\\").Replace("\"", "\\\"");
            long timestamp = System.DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
            string line =
                "{\"sessionId\":\"50c8ad\",\"runId\":\"post-fix\",\"hypothesisId\":\"" + hypothesisId +
                "\",\"location\":\"SaveLoadSystem.TrySaveVolatile\",\"message\":\"" + escaped +
                "\",\"timestamp\":" + timestamp + "}\n";
            System.IO.File.AppendAllText(debugPath, line);
        }
        catch (System.Exception)
        {
        }
        // #endregion
    }
}

