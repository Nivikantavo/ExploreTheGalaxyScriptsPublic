using System;
using UnityEngine;

public static class SaveSlotSelector
{
    public static bool TryParse(string json, out GameSaveData save)
    {
        save = null;
        if (string.IsNullOrEmpty(json))
            return false;

        try
        {
            save = JsonUtility.FromJson<GameSaveData>(json);
        }
        catch (Exception)
        {
            save = null;
            return false;
        }

        save = SaveDataMigration.ToCurrent(save);
        if (save == null || SaveIntegrityGuard.LooksLikeUninitialized(save))
        {
            save = null;
            return false;
        }

        return true;
    }

    public static string PickBestJson(string primaryJson, string previousJson)
    {
        bool hasPrimary = TryParse(primaryJson, out GameSaveData primarySave);
        bool hasPrevious = TryParse(previousJson, out GameSaveData previousSave);

        if (!hasPrimary && !hasPrevious)
            return string.Empty;

        if (!hasPrimary)
            return previousJson;

        if (!hasPrevious)
            return primaryJson;

        int primaryScore = SaveIntegrityGuard.ComputeProgressScore(primarySave);
        int previousScore = SaveIntegrityGuard.ComputeProgressScore(previousSave);
        if (previousScore > primaryScore)
            return previousJson;

        if (SaveIntegrityGuard.IsRegression(primarySave, previousSave))
            return previousJson;

        return primaryJson;
    }
}
