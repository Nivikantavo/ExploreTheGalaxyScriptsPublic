using UnityEngine;

[RequireComponent(typeof(Collider))]
public class WaterTrigger : MonoBehaviour
{
    [SerializeField] private WaterHazardConfig _config;

    private void Reset()
    {
        var col = GetComponent<Collider>();
        col.isTrigger = true;
    }

    private void OnTriggerEnter(Collider other)
    {
        if (_config == null)
        {
            Debug.LogWarning($"{nameof(WaterTrigger)} on '{name}' has no {nameof(WaterHazardConfig)} assigned.", this);
            return;
        }

        if (!other.TryGetComponent(out Player player))
            return;

        var hazard = player.GetComponent<PlayerWaterHazard>();
        if (hazard == null)
            hazard = player.gameObject.AddComponent<PlayerWaterHazard>();

        hazard.EnterWater(_config);
    }

    private void OnTriggerExit(Collider other)
    {
        if (!other.TryGetComponent(out PlayerWaterHazard hazard))
            return;

        hazard.ExitWater();
    }
}
