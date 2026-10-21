using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Zenject;

/// <summary>
/// Listens for island unlocks (index >= 3) and notifies IAP UI about special offers.
/// Ignores unlocks during save restore and islands that already had progress at session start.
/// </summary>
public class IslandOfferController : MonoBehaviour
{
    private IapService _iapService;
    private IslandProgressRegistry _islandRegistry;
    private readonly HashSet<IslandProgressHandler> _subscribed = new();
    private readonly HashSet<int> _notifiedIslandIndices = new();
    private bool _listening;

    [Inject]
    private void Construct(IapService iapService, IslandProgressRegistry islandRegistry)
    {
        _iapService = iapService;
        _islandRegistry = islandRegistry;
    }

    private void OnEnable()
    {
        SubscribeAllIslands();
    }

    private void Start()
    {
        SubscribeAllIslands();
        StartCoroutine(EnableListeningAfterSaveApplied());
    }

    private void Update()
    {
        // Islands register during Construct; keep subscriptions in sync early on.
        if (_subscribed.Count < (_islandRegistry?.Islands.Count ?? 0))
            SubscribeAllIslands();
    }

    private void OnDisable()
    {
        UnsubscribeAll();
    }

    private IEnumerator EnableListeningAfterSaveApplied()
    {
        // Wait until IAP save state is applied so we can seed already-open islands.
        float timeoutSeconds = 60f;
        while (_iapService != null && !_iapService.HasStarted && timeoutSeconds > 0f)
        {
            timeoutSeconds -= Time.unscaledDeltaTime;
            yield return null;
        }

        // Let island ApplySave finish in the same bootstrap pass.
        yield return null;

        SeedAlreadyProgressedIslands();
        _listening = true;
    }

    private void SeedAlreadyProgressedIslands()
    {
        if (_islandRegistry == null)
            return;

        foreach (IslandProgressHandler island in _islandRegistry.Islands)
        {
            if (island == null || island.IslandIndex < IslandOfferCatalogSO.MinIslandIndex)
                continue;

            if (HasIslandProgress(island))
                _notifiedIslandIndices.Add(island.IslandIndex);
        }
    }

    private void SubscribeAllIslands()
    {
        if (_islandRegistry == null)
            return;

        foreach (IslandProgressHandler island in _islandRegistry.Islands)
        {
            if (island == null || _subscribed.Contains(island))
                continue;

            island.HexUnlocked += OnHexUnlocked;
            _subscribed.Add(island);
        }
    }

    private void UnsubscribeAll()
    {
        foreach (IslandProgressHandler island in _subscribed)
        {
            if (island != null)
                island.HexUnlocked -= OnHexUnlocked;
        }

        _subscribed.Clear();
    }

    private void OnHexUnlocked(IslandProgressHandler island, HexTile hex)
    {
        if (!_listening || island == null || _iapService == null)
            return;

        if (island.IslandIndex < IslandOfferCatalogSO.MinIslandIndex)
            return;

        if (!HasIslandProgress(island))
            return;

        // One notification per island per session — first progress after a fresh island open.
        if (!_notifiedIslandIndices.Add(island.IslandIndex))
            return;

        _iapService.NotifyIslandBecameAvailable(island.IslandIndex);
    }

    private static bool HasIslandProgress(IslandProgressHandler island)
    {
        if (island == null)
            return false;

        return island.IsFullyOpened
               || island.IsMainPathReached
               || island.GetOpenProgress01() > 0f;
    }
}
