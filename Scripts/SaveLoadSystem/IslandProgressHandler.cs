using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Zenject;

public class IslandProgressHandler : MonoBehaviour
{
    public event Action<IslandProgressHandler> MainPathReached;
    public event Action<IslandProgressHandler, HexTile> HexUnlocked;
    public event Action<IslandProgressHandler, ResourcePaymentOpener> OpeningHexagonChanged;

    [SerializeField] private int _islandIndex;
    [SerializeField] private bool _isMainPathIsland;
    [SerializeField] private HexTile[] _hexTiles;
    [SerializeField] private OpeningHexagon[] _openingHexagons;

    private IslandProgressRegistry _registry;
    private DynamicNavMeshRebuilder _navMeshRebuilder;
    private bool _suppressMainPathSync;
    private bool _saveCacheValid;
    private IslandSaveData _cachedSaveData;

    public int IslandIndex => _islandIndex;
    public bool IsMainPathIsland => _isMainPathIsland;
    public bool IsMainPathReached { get; private set; }

    [Inject]
    private void Construct(IslandProgressRegistry registry, DynamicNavMeshRebuilder navMeshRebuilder)
    {
        _registry = registry;
        _navMeshRebuilder = navMeshRebuilder;
        _registry.Register(this);
        CacheReferences();
    }

    private void OnDestroy()
    {
        UnsubscribeFromHexTiles();
        UnsubscribeFromOpeningHexagons();
        _registry?.Unregister(this);
    }

    public void MarkMainPathReached()
    {
        if (IsMainPathReached)
            return;

        IsMainPathReached = true;
        MainPathReached?.Invoke(this);
    }

    public void ForceFullyOpen()
    {
        bool wasMainPathReached = IsMainPathReached;

        ApplySave(new IslandSaveData
        {
            IslandIndex = _islandIndex,
            State = IslandUnlockState.FullyOpened
        });

        if (IsMainPathReached && !wasMainPathReached)
            MainPathReached?.Invoke(this);

        // ApplySave suppresses HexUnlocked; still need NavMesh for newly active tiles.
        _navMeshRebuilder?.RequestRebuild();
    }

    public bool IsHexUnlocked(int q, int r)
    {
        HexTile hexTile = FindHexTile(q, r);
        return hexTile != null && !hexTile.IsLocked;
    }

    public OpeningHexagon GetOpeningHexagon(int q, int r) => FindOpeningHexagon(q, r);

    public bool IsOpeningHexagonFilled(int q, int r)
    {
        OpeningHexagon openingHexagon = FindOpeningHexagon(q, r);
        if (openingHexagon != null)
            return openingHexagon.IsFilled;

        return IsHexUnlocked(q, r);
    }

    public float GetOpenProgress01()
    {
        if (_hexTiles == null || _hexTiles.Length == 0)
            return 1f;

        int totalStartsLocked = 0;
        int unlockedStartsLocked = 0;

        foreach (var tile in _hexTiles)
        {
            if (tile == null || !tile.StartsLocked)
                continue;

            totalStartsLocked++;
            if (!tile.IsLocked)
                unlockedStartsLocked++;
        }

        if (totalStartsLocked == 0)
            return 1f;

        return (float)unlockedStartsLocked / totalStartsLocked;
    }

    public int GetOpenPercent() => Mathf.RoundToInt(GetOpenProgress01() * 100f);

    public bool IsFullyOpened => AreAllLockableHexesUnlocked();

    public Vector3 GetFocusWorldPosition()
    {
        if (_hexTiles == null || _hexTiles.Length == 0)
            return transform.position;

        Vector3 sum = Vector3.zero;
        int count = 0;

        foreach (var tile in _hexTiles)
        {
            if (tile == null)
                continue;

            sum += tile.transform.position;
            count++;
        }

        return count > 0 ? sum / count : transform.position;
    }

    public IslandSaveData CreateSaveData()
    {
        if (_saveCacheValid)
            return _cachedSaveData;

        _cachedSaveData = BuildSaveData();
        _saveCacheValid = true;
        return _cachedSaveData;
    }

    private void InvalidateSaveCache()
    {
        _saveCacheValid = false;
        _cachedSaveData = null;
    }

    private IslandSaveData BuildSaveData()
    {
        if (_hexTiles == null)
            return null;

        int lockableCount = 0;
        int unlockedCount = 0;
        for (int i = 0; i < _hexTiles.Length; i++)
        {
            HexTile tile = _hexTiles[i];
            if (tile == null || !tile.StartsLocked)
                continue;

            lockableCount++;
            if (!tile.IsLocked)
                unlockedCount++;
        }

        OpeningHexSaveData[] openingHexes = CollectOpeningHexes();
        bool fullyOpened = lockableCount > 0 && unlockedCount == lockableCount;
        if (!fullyOpened && unlockedCount == 0 && openingHexes.Length == 0)
            return null;

        HexCoordSave[] unlockedCoords = CollectUnlockedCoords();
        if (fullyOpened)
        {
            return new IslandSaveData
            {
                IslandIndex = _islandIndex,
                State = IslandUnlockState.FullyOpened,
                UnlockedHexes = unlockedCoords,
                OpeningHexes = Array.Empty<OpeningHexSaveData>()
            };
        }

        return new IslandSaveData
        {
            IslandIndex = _islandIndex,
            State = IslandUnlockState.PartiallyOpened,
            UnlockedHexes = unlockedCoords,
            OpeningHexes = openingHexes
        };
    }

    private HexCoordSave[] CollectUnlockedCoords()
    {
        var coords = new List<HexCoordSave>();
        if (_hexTiles != null)
        {
            for (int i = 0; i < _hexTiles.Length; i++)
            {
                HexTile tile = _hexTiles[i];
                if (tile != null && tile.StartsLocked && !tile.IsLocked)
                    coords.Add(new HexCoordSave(tile.Q, tile.R));
            }
        }

        if (_openingHexagons != null)
        {
            for (int i = 0; i < _openingHexagons.Length; i++)
            {
                HexTile target = _openingHexagons[i] != null ? _openingHexagons[i].OpenedHexagon : null;
                if (target == null || !target.StartsLocked || target.IsLocked)
                    continue;

                if (!CoordListContains(coords, target.Q, target.R))
                    coords.Add(new HexCoordSave(target.Q, target.R));
            }
        }

        if (coords.Count == 0)
            return Array.Empty<HexCoordSave>();

        var result = new HexCoordSave[coords.Count];
        for (int i = 0; i < coords.Count; i++)
            result[i] = coords[i];

        return result;
    }

    private static bool CoordListContains(List<HexCoordSave> coords, int q, int r)
    {
        for (int i = 0; i < coords.Count; i++)
        {
            if (coords[i].Q == q && coords[i].R == r)
                return true;
        }

        return false;
    }

    private OpeningHexSaveData[] CollectOpeningHexes()
    {
        if (_openingHexagons == null || _openingHexagons.Length == 0)
            return Array.Empty<OpeningHexSaveData>();

        OpeningHexSaveData[] buffer = null;
        int count = 0;
        for (int i = 0; i < _openingHexagons.Length; i++)
        {
            OpeningHexagon opener = _openingHexagons[i];
            if (opener == null)
                continue;

            OpeningHexSaveData save = opener.CreateSaveData();
            if (save == null)
                continue;

            if (buffer == null)
                buffer = new OpeningHexSaveData[_openingHexagons.Length];

            buffer[count++] = save;
        }

        if (count == 0)
            return Array.Empty<OpeningHexSaveData>();

        if (count == buffer.Length)
            return buffer;

        var trimmed = new OpeningHexSaveData[count];
        Array.Copy(buffer, trimmed, count);
        return trimmed;
    }

    public void ApplySave(IslandSaveData saveData)
    {
        if (saveData == null)
            return;

        InvalidateSaveCache();
        SaveSystemTestLog.Info(
            $"Island[{_islandIndex}].ApplySave state={saveData.State} " +
            $"unlocked={saveData.UnlockedHexes?.Length ?? 0} openings={saveData.OpeningHexes?.Length ?? 0} " +
            $"tiles={_hexTiles?.Length ?? 0} openers={_openingHexagons?.Length ?? 0}");

        _suppressMainPathSync = true;
        try
        {
            if (saveData.State == IslandUnlockState.FullyOpened)
            {
                UnlockAllLockableHexes();
                UnlockAllOpeningTargets();
                SyncOpeningHexagonsAfterFullyOpened();
                IsMainPathReached = true;
                SaveSystemTestLog.Info($"Island[{_islandIndex}].ApplySave FullyOpened done percent={GetOpenPercent()}");
                return;
            }

            if (saveData.UnlockedHexes != null)
            {
                foreach (var coord in saveData.UnlockedHexes)
                {
                    bool foundTile = FindHexTile(coord.Q, coord.R) != null;
                    bool foundOpening = FindOpeningHexagon(coord.Q, coord.R) != null;
                    SaveSystemTestLog.Info(
                        $"Island[{_islandIndex}].UnlockHexByCoord q={coord.Q} r={coord.R} " +
                        $"foundTile={foundTile} foundOpening={foundOpening}");
                    UnlockHexByCoord(coord.Q, coord.R);
                }
            }

            if (saveData.OpeningHexes != null)
            {
                foreach (var openingSave in saveData.OpeningHexes)
                {
                    bool found = FindOpeningHexagon(openingSave.Q, openingSave.R) != null;
                    SaveSystemTestLog.Info(
                        $"Island[{_islandIndex}].ApplyOpeningSave q={openingSave.Q} r={openingSave.R} found={found}");
                    FindOpeningHexagon(openingSave.Q, openingSave.R)?.ApplySave(openingSave);
                }
            }

            if (AreAllLockableHexesUnlocked())
                IsMainPathReached = true;

            SaveSystemTestLog.Info(
                $"Island[{_islandIndex}].ApplySave partial done percent={GetOpenPercent()} mainPath={IsMainPathReached}");
        }
        finally
        {
            _suppressMainPathSync = false;
        }
    }

    private void UnlockAllLockableHexes()
    {
        if (_hexTiles == null)
            return;

        foreach (var hexTile in _hexTiles)
        {
            if (hexTile != null && hexTile.StartsLocked)
                hexTile.UnlockFromSave();
        }
    }

    private void UnlockAllOpeningTargets()
    {
        if (_openingHexagons == null)
            return;

        foreach (var opening in _openingHexagons)
        {
            HexTile target = opening != null ? opening.OpenedHexagon : null;
            if (target != null && target.StartsLocked)
                target.UnlockFromSave();
        }
    }

    private void UnlockHexByCoord(int q, int r)
    {
        HexTile hexTile = FindHexTile(q, r);
        if (hexTile != null)
            hexTile.UnlockFromSave();

        OpeningHexagon opening = FindOpeningHexagon(q, r);
        if (opening == null)
            return;

        // DisableBecauseHexAlreadyOpened alone used to hide the opener without
        // unlocking a target that is missing from _hexTiles.
        if (opening.OpenedHexagon != null && opening.OpenedHexagon.StartsLocked)
            opening.OpenedHexagon.UnlockFromSave();

        opening.DisableBecauseHexAlreadyOpened();
    }

    private void CacheReferences()
    {
        if (_hexTiles == null || _hexTiles.Length == 0)
            _hexTiles = GetComponentsInChildren<HexTile>(true);

        if (_openingHexagons == null || _openingHexagons.Length == 0)
            _openingHexagons = GetComponentsInChildren<OpeningHexagon>(true);

        SubscribeToHexTiles();
        SubscribeToOpeningHexagons();
    }

    private void SubscribeToHexTiles()
    {
        if (_hexTiles == null)
            return;

        foreach (var hexTile in _hexTiles)
        {
            if (hexTile == null)
                continue;

            hexTile.OnUnlocked -= OnHexTileUnlocked;
            hexTile.OnUnlocked += OnHexTileUnlocked;
        }
    }

    private void UnsubscribeFromHexTiles()
    {
        if (_hexTiles == null)
            return;

        foreach (var hexTile in _hexTiles)
        {
            if (hexTile == null)
                continue;

            hexTile.OnUnlocked -= OnHexTileUnlocked;
        }
    }

    private void OnHexTileUnlocked(HexTile hexTile)
    {
        if (_suppressMainPathSync)
            return;

        InvalidateSaveCache();
        HexUnlocked?.Invoke(this, hexTile);

        // MainPathReached goals listen to this flag; full exploration must set it
        // even if the player never walks through IslandMainPathTrigger.
        if (AreAllLockableHexesUnlocked())
            MarkMainPathReached();
    }

    private bool AreAllLockableHexesUnlocked()
    {
        if (_hexTiles == null || _hexTiles.Length == 0)
            return false;

        bool hasLockable = false;
        foreach (var tile in _hexTiles)
        {
            if (tile == null || !tile.StartsLocked)
                continue;

            hasLockable = true;
            if (tile.IsLocked)
                return false;
        }

        return hasLockable;
    }

    private void SubscribeToOpeningHexagons()
    {
        if (_openingHexagons == null)
            return;

        foreach (var openingHexagon in _openingHexagons)
        {
            if (openingHexagon == null)
                continue;

            openingHexagon.ValueChanged -= OnOpeningHexagonChanged;
            openingHexagon.OnOpened -= OnOpeningHexagonOpened;
            openingHexagon.ValueChanged += OnOpeningHexagonChanged;
            openingHexagon.OnOpened += OnOpeningHexagonOpened;
        }
    }

    private void UnsubscribeFromOpeningHexagons()
    {
        if (_openingHexagons == null)
            return;

        foreach (var openingHexagon in _openingHexagons)
        {
            if (openingHexagon == null)
                continue;

            openingHexagon.ValueChanged -= OnOpeningHexagonChanged;
            openingHexagon.OnOpened -= OnOpeningHexagonOpened;
        }
    }

    private void OnOpeningHexagonChanged(ResourcePaymentOpener openingHexagon)
    {
        InvalidateSaveCache();
        OpeningHexagonChanged?.Invoke(this, openingHexagon);
    }

    private void OnOpeningHexagonOpened(ResourcePaymentOpener openingHexagon)
    {
        InvalidateSaveCache();
        OpeningHexagonChanged?.Invoke(this, openingHexagon);
    }

    private void SyncOpeningHexagonsAfterFullyOpened()
    {
        if (_openingHexagons == null)
            return;

        foreach (var openingHexagon in _openingHexagons)
        {
            if (openingHexagon == null)
                continue;

            HexTile target = openingHexagon.OpenedHexagon;
            if (target == null)
            {
                openingHexagon.DisableBecauseHexAlreadyOpened();
                continue;
            }

            // Opener for a hex on this island (or already unlocked elsewhere) is done.
            // Opener that unlocks the next island must stay active while that hex is locked.
            if (OwnsHexTile(target) || !target.IsLocked)
                openingHexagon.DisableBecauseHexAlreadyOpened();
            else
                openingHexagon.EnableBecauseTargetStillLocked();
        }
    }

    private bool OwnsHexTile(HexTile hexTile)
    {
        if (hexTile == null || _hexTiles == null)
            return false;

        return _hexTiles.Contains(hexTile);
    }

    private HexTile FindHexTile(int q, int r)
    {
        if (_hexTiles == null)
            return null;

        return _hexTiles.FirstOrDefault(tile => tile != null && tile.Q == q && tile.R == r);
    }

    private OpeningHexagon FindOpeningHexagon(int q, int r)
    {
        if (_openingHexagons == null)
            return null;

        return _openingHexagons.FirstOrDefault(opener =>
            opener != null &&
            opener.OpenedHexagon != null &&
            opener.OpenedHexagon.Q == q &&
            opener.OpenedHexagon.R == r);
    }
}
