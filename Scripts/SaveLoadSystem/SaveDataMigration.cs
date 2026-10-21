using UnityEngine;

public static class SaveDataMigration
{
    public static GameSaveData ToCurrent(GameSaveData save)
    {
        if (save == null)
            return null;

        if (save.SchemaVersion >= SaveWorldIds.CurrentSchemaVersion &&
            save.Worlds != null &&
            save.Worlds.Length > 0)
        {
            if (string.IsNullOrEmpty(save.CurrentWorldId))
                save.CurrentWorldId = SaveWorldIds.World1;

            save.UnlockedWorldIds = PlanetTravelAccess.EnsureDefaultUnlocks(
                save.UnlockedWorldIds,
                save.CurrentWorldId);
            SaveWorldAccess.MirrorWorld1ToRoot(save);
            return save;
        }

        var world1 = SaveWorldAccess.FromRoot(save);
        world1.WorldId = SaveWorldIds.World1;

        save.Worlds = new[] { world1 };
        save.SchemaVersion = SaveWorldIds.CurrentSchemaVersion;
        if (string.IsNullOrEmpty(save.CurrentWorldId))
            save.CurrentWorldId = SaveWorldIds.World1;

        save.UnlockedWorldIds = PlanetTravelAccess.EnsureDefaultUnlocks(
            save.UnlockedWorldIds,
            save.CurrentWorldId);
        SaveWorldAccess.MirrorWorld1ToRoot(save);
        return save;
    }

    public static GameSaveData Clone(GameSaveData save)
    {
        if (save == null)
            return null;

        return JsonUtility.FromJson<GameSaveData>(JsonUtility.ToJson(save));
    }
}
