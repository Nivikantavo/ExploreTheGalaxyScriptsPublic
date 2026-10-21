using UnityEngine;

public class PlayerPrefsSaveLoader : ISaveStorage
{
    private const string SaveKey = "game_save_v1";
    private const string PreviousSaveKey = "game_save_v1_prev";
    private string _cachedJson;
    private bool _hasCachedJson;

    public SaveStorageInitState InitState =>
        HasSave() ? SaveStorageInitState.Loaded : SaveStorageInitState.Empty;

    public bool HasSave() => PlayerPrefs.HasKey(SaveKey) || PlayerPrefs.HasKey(PreviousSaveKey);

    public void Save(string json)
    {
        if (_hasCachedJson && string.Equals(_cachedJson, json, System.StringComparison.Ordinal))
            return;

        if (!_hasCachedJson)
        {
            string current = PlayerPrefs.GetString(SaveKey, string.Empty);
            if (!string.IsNullOrEmpty(current) && !string.Equals(current, json, System.StringComparison.Ordinal))
                PlayerPrefs.SetString(PreviousSaveKey, current);
        }
        else if (!string.IsNullOrEmpty(_cachedJson))
        {
            PlayerPrefs.SetString(PreviousSaveKey, _cachedJson);
        }

        PlayerPrefs.SetString(SaveKey, json);
        PlayerPrefs.Save();
        _cachedJson = json;
        _hasCachedJson = true;
    }

    public void SaveBothSlots(string json)
    {
        PlayerPrefs.SetString(SaveKey, json);
        PlayerPrefs.SetString(PreviousSaveKey, json);
        PlayerPrefs.Save();
        _cachedJson = json;
        _hasCachedJson = true;
    }

    public string Load()
    {
        _cachedJson = PlayerPrefs.GetString(SaveKey, string.Empty);
        _hasCachedJson = true;
        return _cachedJson;
    }

    public string LoadPrevious() => PlayerPrefs.GetString(PreviousSaveKey, string.Empty);

    public void Delete()
    {
        PlayerPrefs.DeleteKey(SaveKey);
        PlayerPrefs.DeleteKey(PreviousSaveKey);
        PlayerPrefs.Save();
        _cachedJson = string.Empty;
        _hasCachedJson = true;
    }
}
