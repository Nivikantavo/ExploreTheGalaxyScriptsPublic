using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public class Inventory
{
    public event Action<ResourceCell> CellChanged;
    public event Action<Instrument> InstrumentUpgraded;
    public event Action<Equipment> EquipmentChanged;
    public event Action<ResourceType, int> LifetimeResourceChanged;
    public event Action<int> EnemiesKilledChanged;

    public IReadOnlyList<ResourceCell> Cells => _cells;
    public IReadOnlyList<Instrument> Instruments => _instruments;
    public IReadOnlyList<Equipment> Equipment => _equipment;
    public int EnemiesKilled => _enemiesKilled;

    private List<ResourceCell> _cells;
    private List<Instrument> _instruments;
    private List<Equipment> _equipment;
    private readonly Dictionary<ResourceType, int> _lifetimeAmounts = new();
    private int _enemiesKilled;

    public Inventory(DefaultInstruments defaultInstruments)
    {
        _cells = new List<ResourceCell>();
        _instruments = new List<Instrument>();
        _equipment = new List<Equipment>();
        foreach(InstrumentConfig instrumentConfig in defaultInstruments.Instruments)
        {
            _instruments.Add(new Instrument(instrumentConfig.Type, instrumentConfig.Level, instrumentConfig.DamagePerHit, instrumentConfig.InstrumentObj));
        }
    }

    public Inventory(Dictionary<ResourceType, int> initialResources, List<Instrument> initialInstruments)
    {
        _cells = new List<ResourceCell>();
        _instruments = initialInstruments;
        _equipment = new List<Equipment>();
        foreach(ResourceType resourseType in Enum.GetValues(typeof(ResourceType)))
        {
            if(initialResources.ContainsKey(resourseType))
            {
                _cells.Add(new ResourceCell(resourseType, initialResources[resourseType]));
            }
        }
    }

    public void AddResource(ResourceType resourseType, int amount)
    {
        GrantResource(resourseType, amount, countTowardsLifetime: true);
    }

    /// <summary>
    /// Grants resources that must not advance lifetime progress:
    /// rewarded ads, shop and IAP purchases, and debug cheats.
    /// They appear in inventory but do not count toward achievements or celebrations.
    /// </summary>
    public void AddResourceWithoutLifetime(ResourceType resourseType, int amount)
    {
        GrantResource(resourseType, amount, countTowardsLifetime: false);
    }

    /// <summary>
    /// Grants resources from a rewarded ad. They appear in inventory
    /// but do not count toward lifetime totals, achievements, or celebrations.
    /// </summary>
    public void AddRewardedAdResource(ResourceType resourseType, int amount)
    {
        AddResourceWithoutLifetime(resourseType, amount);
    }

    public int GetLifetimeAmount(ResourceType resourseType)
    {
        return _lifetimeAmounts.TryGetValue(resourseType, out int amount) ? amount : 0;
    }

    public ResourceAmountSave[] CreateLifetimeResourcesSaveData()
    {
        if (_lifetimeAmounts.Count == 0)
            return Array.Empty<ResourceAmountSave>();

        var result = new ResourceAmountSave[_lifetimeAmounts.Count];
        int index = 0;
        foreach (var pair in _lifetimeAmounts)
        {
            if (pair.Value <= 0)
                continue;

            result[index++] = new ResourceAmountSave { Type = pair.Key, Amount = pair.Value };
        }

        if (index == result.Length)
            return result;

        if (index == 0)
            return Array.Empty<ResourceAmountSave>();

        var trimmed = new ResourceAmountSave[index];
        Array.Copy(result, trimmed, index);
        return trimmed;
    }

    public void ApplyLifetimeSave(ResourceAmountSave[] lifetimeResources, int enemiesKilled)
    {
        var lifetimeHandlers = LifetimeResourceChanged;
        var killHandlers = EnemiesKilledChanged;
        LifetimeResourceChanged = null;
        EnemiesKilledChanged = null;

        try
        {
            _lifetimeAmounts.Clear();
            if (lifetimeResources != null)
            {
                foreach (var resource in lifetimeResources)
                {
                    if (resource.Amount > 0)
                        _lifetimeAmounts[resource.Type] = resource.Amount;
                }
            }

            _enemiesKilled = Mathf.Max(0, enemiesKilled);
        }
        finally
        {
            LifetimeResourceChanged = lifetimeHandlers;
            EnemiesKilledChanged = killHandlers;
        }
    }

    public void RegisterEnemyKill()
    {
        _enemiesKilled++;
        EnemiesKilledChanged?.Invoke(_enemiesKilled);
    }

    public void RemoveResource(ResourceType resourseType, int amount)
    {
        if (amount <= 0) return;

        ResourceCell cell = GetOrAddResourceCell(resourseType);
        cell.RemoveAmount(amount);
        CellChanged?.Invoke(cell);
    }

    public int GetResourceAmount(ResourceType resourseType)
    {
        ResourceCell cell = GetOrAddResourceCell(resourseType);
        return cell.Amount;
    }

    public void UpgradeInstrument(InstrumentConfig instrumentConfig)
    {
        Instrument instrument = _instruments.FirstOrDefault(instrument => instrument.Type == instrumentConfig.Type);
        if(instrument == null) return;
        instrument.Upgrade(instrumentConfig);
        InstrumentUpgraded?.Invoke(instrument);
    }

    public Equipment GetEquipment(EquipmentType type)
    {
        return _equipment.FirstOrDefault(item => item.Type == type);
    }

    public int GetEquipmentLevel(EquipmentType type)
    {
        Equipment equipment = GetEquipment(type);
        return equipment != null ? equipment.Level : 0;
    }

    public int GetEquippedHealthBonus()
    {
        int bonus = 0;
        foreach (var item in _equipment)
            bonus += item.BonusHealth;
        return bonus;
    }

    public void ApplyEquipment(EquipmentConfig equipmentConfig)
    {
        if (equipmentConfig == null)
            return;

        Equipment equipment = GetEquipment(equipmentConfig.Type);
        if (equipment == null)
        {
            equipment = new Equipment(equipmentConfig);
            _equipment.Add(equipment);
        }
        else
        {
            equipment.Upgrade(equipmentConfig);
        }

        EquipmentChanged?.Invoke(equipment);
    }

    public void ApplySave(
        ResourceAmountSave[] resources,
        InstrumentSaveData[] instruments,
        AllInstruments allInstruments,
        EquipmentSaveData[] equipment = null,
        AllEquipment allEquipment = null)
    {
        // Suppress gameplay listeners while restoring. On WebGL, CellChanged can
        // trigger LocalizedString.GetLocalizedString() -> Addressables.WaitForCompletion,
        // which is unsupported and aborts the rest of save apply.
        var cellHandlers = CellChanged;
        var instrumentHandlers = InstrumentUpgraded;
        var equipmentHandlers = EquipmentChanged;
        var lifetimeHandlers = LifetimeResourceChanged;
        CellChanged = null;
        InstrumentUpgraded = null;
        EquipmentChanged = null;
        LifetimeResourceChanged = null;

        try
        {
            _cells.Clear();

            if (resources != null)
            {
                foreach (var resource in resources)
                {
                    if (resource.Amount > 0)
                        _cells.Add(new ResourceCell(resource.Type, resource.Amount));
                }
            }

            if (instruments != null && allInstruments != null)
            {
                foreach (var instrumentSave in instruments)
                {
                    Instrument instrument = _instruments.FirstOrDefault(item => item.Type == instrumentSave.Type);
                    InstrumentConfig config = allInstruments.Instruments
                        .FirstOrDefault(item => item.Type == instrumentSave.Type && item.Level == instrumentSave.Level);

                    if (instrument != null && config != null)
                        instrument.Restore(config);
                }
            }

            RestoreEquipment(equipment, allEquipment);
        }
        finally
        {
            CellChanged = cellHandlers;
            InstrumentUpgraded = instrumentHandlers;
            EquipmentChanged = equipmentHandlers;
            LifetimeResourceChanged = lifetimeHandlers;
        }
    }

    private void RestoreEquipment(EquipmentSaveData[] equipment, AllEquipment allEquipment)
    {
        _equipment.Clear();
        if (equipment == null || allEquipment == null)
            return;

        foreach (var equipmentSave in equipment)
        {
            if (equipmentSave.Level <= 0)
                continue;

            EquipmentConfig config = allEquipment.Get(equipmentSave.Type, equipmentSave.Level);
            if (config == null)
                continue;

            _equipment.Add(new Equipment(config));
        }
    }

    private void GrantResource(ResourceType resourseType, int amount, bool countTowardsLifetime)
    {
        if (amount <= 0)
            return;

        ResourceCell cell = GetOrAddResourceCell(resourseType);
        cell.AddAmount(amount);
        if (countTowardsLifetime)
            AddLifetimeAmount(resourseType, amount);
        CellChanged?.Invoke(cell);
    }

    private void AddLifetimeAmount(ResourceType resourseType, int amount)
    {
        if (amount <= 0)
            return;

        int next = GetLifetimeAmount(resourseType) + amount;
        _lifetimeAmounts[resourseType] = next;
        LifetimeResourceChanged?.Invoke(resourseType, next);
    }

    private ResourceCell GetOrAddResourceCell(ResourceType resourseType)
    {
        for (int i = 0; i < _cells.Count; i++)
        {
            if (_cells[i].ResourseType == resourseType)
                return _cells[i];
        }

        var created = new ResourceCell(resourseType, 0);
        _cells.Add(created);
        return created;
    }
}
