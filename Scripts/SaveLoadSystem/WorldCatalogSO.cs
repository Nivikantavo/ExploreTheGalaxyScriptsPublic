using UnityEngine;

[CreateAssetMenu(menuName = "Save/World Catalog", fileName = "WorldCatalog")]
public class WorldCatalogSO : ScriptableObject
{
    [SerializeField] private string _defaultWorldId = SaveWorldIds.World1;
    [SerializeField] private string _fallbackSceneName = WorldCatalog.World1SceneName;
    [SerializeField] private WorldCatalogEntry[] _worlds;

    public string DefaultWorldId =>
        string.IsNullOrEmpty(_defaultWorldId) ? SaveWorldIds.World1 : _defaultWorldId;

    public WorldCatalogEntry[] Worlds => _worlds;

    public WorldCatalogEntry[] GetOrderedWorlds()
    {
        return PlanetTravelAccess.GetOrdered(_worlds);
    }

    public string ResolveSceneName(string worldId)
    {
        if (string.IsNullOrEmpty(worldId))
            worldId = DefaultWorldId;

        return WorldCatalog.ResolveSceneName(worldId, _worlds, _fallbackSceneName);
    }

    public string ResolveWorldIdFromScene(string sceneName)
    {
        return WorldCatalog.ResolveWorldIdFromScene(sceneName, _worlds);
    }
}
