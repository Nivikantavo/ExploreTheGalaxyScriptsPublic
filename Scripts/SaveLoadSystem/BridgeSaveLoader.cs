using System;
using System.Collections.Generic;
using Playgama;
using UnityEngine;

public class BridgeSaveLoader : ISaveStorage, IInitializableSaveLoader
{
    private const string SaveKey = "game_save_v1";
    private const string PreviousSaveKey = "game_save_v1_prev";
    private const float WriteTimeoutSeconds = 5f;
    private const int MaxGetRetries = 2;

    private string _cachedJson;
    private string _cachedPreviousJson;
    private bool _hasSave;
    private SaveStorageInitState _initState = SaveStorageInitState.Pending;
    private bool _isWriting;
    private readonly Queue<(string key, string json)> _writeQueue = new();
    private Action _pendingInitCallback;
    private float _writeStartedAt = -1f;
    private int _writeAttempt;
    private int _getRetries;
    private bool _primaryReceived;
    private bool _initCallbackInvoked;
    private string _writeKey = SaveKey;

    public bool IsInitialized =>
        _initState == SaveStorageInitState.Empty || _initState == SaveStorageInitState.Loaded;

    public SaveStorageInitState InitState => _initState;

    public event Action LateDataArrived;

    public void Initialize(Action onComplete)
    {
        SaveSystemTestLog.Info("BridgeSaveLoader.Initialize begin");

        if (IsInitialized)
        {
            SaveSystemTestLog.Info($"BridgeSaveLoader.Initialize already done hasSave={_hasSave}");
            onComplete?.Invoke();
            return;
        }

        _pendingInitCallback = onComplete;
        _getRetries = 0;
        _primaryReceived = false;
        _initCallbackInvoked = false;
        RequestPrimary();
    }

    public void ForceCompleteInitialization()
    {
        if (IsInitialized)
            return;

        SaveSystemTestLog.Warn("BridgeSaveLoader.ForceCompleteInitialization keeping InitUnknown");
        _initState = SaveStorageInitState.Unknown;
        InvokeInitCallback();
    }

    public bool HasSave() => _hasSave;

    public string Load()
    {
        SaveSystemTestLog.Info(
            $"BridgeSaveLoader.Load hasSave={_hasSave} jsonLength={_cachedJson?.Length ?? 0}");
        return _cachedJson ?? string.Empty;
    }

    public string LoadPrevious() => _cachedPreviousJson ?? string.Empty;

    public void Save(string json)
    {
        string previousGood = _cachedJson;
        _cachedJson = json;
        _hasSave = !string.IsNullOrEmpty(json);
        if (_hasSave)
            _initState = SaveStorageInitState.Loaded;

        SaveSystemTestLog.Info(
            $"BridgeSaveLoader.Save requested jsonLength={json?.Length ?? 0} isWriting={_isWriting}");

        if (Bridge.instance == null || Bridge.storage == null)
        {
            SaveSystemTestLog.Error("BridgeSaveLoader.Save storage unavailable");
            return;
        }

        RecoverStuckWriteIfNeeded();

        if (!string.IsNullOrEmpty(previousGood) && previousGood != json)
        {
            EnqueueWrite(PreviousSaveKey, previousGood);
            EnqueueWrite(SaveKey, json);
            return;
        }

        EnqueueWrite(SaveKey, json);
    }

    public void SaveBothSlots(string json)
    {
        _cachedJson = json;
        _cachedPreviousJson = json;
        _hasSave = !string.IsNullOrEmpty(json);
        if (_hasSave)
            _initState = SaveStorageInitState.Loaded;

        SaveSystemTestLog.Info(
            $"BridgeSaveLoader.SaveBothSlots requested jsonLength={json?.Length ?? 0}");

        if (Bridge.instance == null || Bridge.storage == null)
        {
            SaveSystemTestLog.Error("BridgeSaveLoader.SaveBothSlots storage unavailable");
            return;
        }

        RecoverStuckWriteIfNeeded();
        EnqueueWrite(PreviousSaveKey, json);
        EnqueueWrite(SaveKey, json);
    }

    public void Delete()
    {
        SaveSystemTestLog.Info("BridgeSaveLoader.Delete");
        _cachedJson = null;
        _cachedPreviousJson = null;
        _hasSave = false;
        _writeQueue.Clear();
        _initState = SaveStorageInitState.Empty;

        if (Bridge.instance == null || Bridge.storage == null)
            return;

        Bridge.storage.Delete(SaveKey);
        Bridge.storage.Delete(PreviousSaveKey);
    }

    private void RequestPrimary()
    {
        if (Bridge.instance == null || Bridge.storage == null)
        {
            SaveSystemTestLog.Error("BridgeSaveLoader.Initialize storage unavailable");
            _initState = SaveStorageInitState.Unknown;
            InvokeInitCallback();
            return;
        }

        SaveSystemTestLog.Info($"BridgeSaveLoader.Initialize Get key={SaveKey} retry={_getRetries}");

        try
        {
            Bridge.storage.Get(SaveKey, OnPrimaryLoaded);
        }
        catch (Exception exception)
        {
            SaveSystemTestLog.Error($"BridgeSaveLoader.Initialize Get exception: {exception}");
            RetryOrStayUnknown();
        }
    }

    private void OnPrimaryLoaded(bool success, string data)
    {
        SaveSystemTestLog.Info(
            $"BridgeSaveLoader.OnPrimaryLoaded success={success} dataLength={data?.Length ?? 0}");

        if (!success)
        {
            RetryOrStayUnknown();
            return;
        }

        _primaryReceived = true;
        _cachedJson = data;

        RequestPrevious();
    }

    private void RequestPrevious()
    {
        if (Bridge.instance == null || Bridge.storage == null)
        {
            CompleteFromSlots();
            return;
        }

        try
        {
            Bridge.storage.Get(PreviousSaveKey, OnPreviousLoaded);
        }
        catch (Exception exception)
        {
            SaveSystemTestLog.Error($"BridgeSaveLoader.Get previous exception: {exception}");
            CompleteFromSlots();
        }
    }

    private void OnPreviousLoaded(bool success, string data)
    {
        SaveSystemTestLog.Info(
            $"BridgeSaveLoader.OnPreviousLoaded success={success} dataLength={data?.Length ?? 0}");

        if (success)
            _cachedPreviousJson = data;

        CompleteFromSlots();
    }

    private void RetryOrStayUnknown()
    {
        _getRetries++;
        if (_getRetries < MaxGetRetries)
        {
            SaveSystemTestLog.Warn($"BridgeSaveLoader retry Get {_getRetries}/{MaxGetRetries}");
            RequestPrimary();
            return;
        }

        SaveSystemTestLog.Error("BridgeSaveLoader Get failed — InitUnknown, will not treat as empty");
        _initState = SaveStorageInitState.Unknown;
        InvokeInitCallback();
    }

    private void CompleteFromSlots()
    {
        string picked = SaveSlotSelector.PickBestJson(_cachedJson, _cachedPreviousJson);
        bool hadUnknown = _initState == SaveStorageInitState.Unknown
                          || _initState == SaveStorageInitState.Pending;

        if (!string.IsNullOrEmpty(picked))
        {
            _cachedJson = picked;
            _hasSave = true;
            _initState = SaveStorageInitState.Loaded;
        }
        else if (_primaryReceived)
        {
            _cachedJson = null;
            _hasSave = false;
            _initState = SaveStorageInitState.Empty;
        }
        else
        {
            _initState = SaveStorageInitState.Unknown;
            _hasSave = false;
        }

        SaveSystemTestLog.Info(
            $"BridgeSaveLoader.CompleteFromSlots state={_initState} hasSave={_hasSave} " +
            $"jsonLength={_cachedJson?.Length ?? 0}");

        bool alreadyNotified = _initCallbackInvoked;
        InvokeInitCallback();

        if (alreadyNotified && hadUnknown && IsInitialized)
        {
            SaveSystemTestLog.Info("BridgeSaveLoader late data arrived after timeout");
            LateDataArrived?.Invoke();
        }
    }

    private void InvokeInitCallback()
    {
        if (_initCallbackInvoked)
            return;

        _initCallbackInvoked = true;
        Action callback = _pendingInitCallback;
        _pendingInitCallback = null;
        callback?.Invoke();
    }

    private void EnqueueWrite(string key, string json)
    {
        RecoverStuckWriteIfNeeded();

        if (_isWriting)
        {
            _writeQueue.Enqueue((key, json));
            SaveSystemTestLog.Warn($"BridgeSaveLoader.Save queued pending write key={key} queue={_writeQueue.Count}");
            return;
        }

        WriteToBridge(key, json);
    }

    private void RecoverStuckWriteIfNeeded()
    {
        if (!_isWriting || _writeStartedAt < 0f)
            return;

        if (Time.realtimeSinceStartup - _writeStartedAt < WriteTimeoutSeconds)
            return;

        SaveSystemTestLog.Warn(
            $"BridgeSaveLoader write lock timed out after {WriteTimeoutSeconds}s, resetting");
        _isWriting = false;
        _writeStartedAt = -1f;
    }

    private void WriteToBridge(string key, string json)
    {
        _writeAttempt++;
        int attempt = _writeAttempt;
        _isWriting = true;
        _writeStartedAt = Time.realtimeSinceStartup;
        _writeKey = key;

        SaveSystemTestLog.Info(
            $"BridgeSaveLoader.WriteToBridge attempt={attempt} key={key} jsonLength={json?.Length ?? 0}");
        SaveSystemTestLog.Info($"BridgeSaveLoader.WriteToBridge payload={json}");

        try
        {
            Bridge.storage.Set(key, json, success => OnSaveCompleted(success, attempt));
        }
        catch (Exception exception)
        {
            _isWriting = false;
            _writeStartedAt = -1f;
            SaveSystemTestLog.Error($"BridgeSaveLoader.WriteToBridge exception attempt={attempt}: {exception}");
        }
    }

    private void OnSaveCompleted(bool success, int attempt)
    {
        _isWriting = false;
        _writeStartedAt = -1f;

        if (success)
            SaveSystemTestLog.Info($"BridgeSaveLoader.OnSaveCompleted SUCCESS attempt={attempt} key={_writeKey}");
        else
            SaveSystemTestLog.Error($"BridgeSaveLoader.OnSaveCompleted FAILED attempt={attempt} key={_writeKey}");

        if (_writeQueue.Count == 0)
            return;

        (string nextKey, string nextJson) = _writeQueue.Dequeue();
        SaveSystemTestLog.Info($"BridgeSaveLoader flushing queued write after attempt={attempt} remaining={_writeQueue.Count}");
        WriteToBridge(nextKey, nextJson);
    }
}
