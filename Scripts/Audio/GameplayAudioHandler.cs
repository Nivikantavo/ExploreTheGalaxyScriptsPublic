using System;
using System.Collections.Generic;
using UnityEngine;
using Zenject;

public class GameplayAudioHandler : MonoBehaviour, IInitializable
{
    private IAudioService _audioService;
    private AudioClipsConfig _clipsConfig;
    private LifecycleEventBus _lifecycleEventBus;
    private readonly Dictionary<ResourseHolderView, Action> _resourceHitHandlers = new();
    private bool _subscribedToLifecycle;

    [Inject]
    private void Construct(IAudioService audioService, AudioClipsConfig clipsConfig, LifecycleEventBus lifecycleEventBus)
    {
        _audioService = audioService;
        _clipsConfig = clipsConfig;
        _lifecycleEventBus = lifecycleEventBus;
    }

    public void Initialize()
    {
        SubscribeLifecycle();
    }

    private void Start()
    {
        // Start runs after scene Awakes, so holders are ready.
        SubscribeResourceHolders();
    }

    private void OnDestroy()
    {
        UnsubscribeLifecycle();
        UnsubscribeResourceHolders();
    }

    private void SubscribeLifecycle()
    {
        if (_subscribedToLifecycle || _lifecycleEventBus == null)
            return;

        _lifecycleEventBus.OnEvent += OnLifecycleEvent;
        _subscribedToLifecycle = true;
    }

    private void UnsubscribeLifecycle()
    {
        if (!_subscribedToLifecycle || _lifecycleEventBus == null)
            return;

        _lifecycleEventBus.OnEvent -= OnLifecycleEvent;
        _subscribedToLifecycle = false;
    }

    private void SubscribeResourceHolders()
    {
        var holders = FindObjectsByType<ResourseHolderView>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        foreach (var holder in holders)
        {
            if (holder == null || _resourceHitHandlers.ContainsKey(holder))
                continue;

            ResourseHolderView capturedHolder = holder;
            Action handler = () => OnResourceHolderHit(capturedHolder);
            _resourceHitHandlers[holder] = handler;
            holder.OnHit += handler;
        }
    }

    private void UnsubscribeResourceHolders()
    {
        foreach (var pair in _resourceHitHandlers)
        {
            if (pair.Key != null)
                pair.Key.OnHit -= pair.Value;
        }

        _resourceHitHandlers.Clear();
    }

    private void OnResourceHolderHit(ResourseHolderView holder)
    {
        if (holder == null || _audioService == null)
            return;

        _audioService.PlayResourceHit(holder.ResourseType);
    }

    private void OnLifecycleEvent(LifecycleEventData data)
    {
        if (_audioService == null)
            return;

        switch (data.Type)
        {
            case LifecycleEventType.PlayerDied:
            case LifecycleEventType.PlayerDrowned:
                _audioService.PlaySfx(AudioClipId.PlayerDeath);
                break;
            case LifecycleEventType.EnemyDied:
                _audioService.PlaySfx(AudioClipId.EnemyDeath);
                break;
        }
    }
}
