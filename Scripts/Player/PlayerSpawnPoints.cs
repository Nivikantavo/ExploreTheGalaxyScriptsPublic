using UnityEngine;

public class PlayerSpawnPoints
{
    public Transform DefaultSpawn { get; }
    public Transform TutorialSpawn { get; }

    public PlayerSpawnPoints(Transform defaultSpawn, Transform tutorialSpawn)
    {
        DefaultSpawn = defaultSpawn;
        TutorialSpawn = tutorialSpawn;
    }
}
