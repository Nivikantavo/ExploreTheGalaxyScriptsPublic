using UnityEngine;

public class WorldSaveIdentity : MonoBehaviour
{
    [SerializeField] private string _worldId = SaveWorldIds.World1;

    public string WorldId => _worldId;

    public static bool TryFindHostWorldId(out string worldId)
    {
        WorldSaveIdentity[] identities = FindObjectsByType<WorldSaveIdentity>(
            FindObjectsInactive.Include,
            FindObjectsSortMode.None);

        for (int i = 0; i < identities.Length; i++)
        {
            WorldSaveIdentity identity = identities[i];
            if (identity == null || string.IsNullOrEmpty(identity.WorldId))
                continue;

            worldId = identity.WorldId;
            return true;
        }

        worldId = null;
        return false;
    }
}
