using UnityEngine;

[RequireComponent(typeof(Collider))]
public class LavaTrigger : MonoBehaviour
{
    [SerializeField] private int _damagePerTick = 1;
    [SerializeField] private float _tickInterval = 0.5f;
    [SerializeField] private float _burnDuration = 3f;

    private void Reset()
    {
        var col = GetComponent<Collider>();
        col.isTrigger = true;
    }

    private void OnTriggerEnter(Collider other)
    {
        if (!other.TryGetComponent(out Player player))
            return;

        var burning = player.GetComponent<PlayerBurning>();
        if (burning == null)
            burning = player.gameObject.AddComponent<PlayerBurning>();

        burning.Restart(_damagePerTick, _tickInterval, _burnDuration);
        burning.EnterLava();
    }

    private void OnTriggerExit(Collider other)
    {
        if (!other.TryGetComponent(out PlayerBurning burning))
            return;

        burning.ExitLava();
    }
}
