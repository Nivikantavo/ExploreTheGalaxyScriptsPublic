using UnityEngine;

public static class ProductionSparkPlayer
{
    public static void Play(GameObject sparkPrefab, Vector3 position, Quaternion rotation, float scale)
    {
        if (sparkPrefab == null)
            return;

        GameObject instance = Object.Instantiate(sparkPrefab, position, rotation);
        if (scale > 0f)
            instance.transform.localScale = Vector3.one * scale;
    }
}
