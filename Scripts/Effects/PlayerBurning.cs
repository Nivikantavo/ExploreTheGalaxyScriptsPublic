using UnityEngine;

public class PlayerBurning : MonoBehaviour
{
    private Player _player;
    private int _damagePerTick;
    private float _tickInterval;
    private float _burnDuration;

    private float _tickTimer;
    private float _remainingDuration;
    private int _lavaOverlapCount;

    private void Awake()
    {
        _player = GetComponent<Player>();
    }

    public void Restart(int damagePerTick, float tickInterval, float burnDuration)
    {
        _damagePerTick = damagePerTick;
        _tickInterval = Mathf.Max(0.01f, tickInterval);
        _burnDuration = Mathf.Max(0f, burnDuration);
        _tickTimer = 0f;
        _remainingDuration = _burnDuration;
        enabled = true;
    }

    public void EnterLava()
    {
        _lavaOverlapCount++;
        RefreshDuration();
    }

    public void ExitLava()
    {
        _lavaOverlapCount = Mathf.Max(0, _lavaOverlapCount - 1);
    }

    public void RefreshDuration()
    {
        _remainingDuration = _burnDuration;
    }

    private void Update()
    {
        if (_lavaOverlapCount > 0)
            _remainingDuration = _burnDuration;

        _tickTimer += Time.deltaTime;
        if (_tickTimer >= _tickInterval)
        {
            _tickTimer = 0f;
            ApplyDamage();
        }

        _remainingDuration -= Time.deltaTime;
        if (_remainingDuration <= 0f)
            enabled = false;
    }

    private void ApplyDamage()
    {
        if (_player == null)
            return;

        _player.TakeDamage(_damagePerTick);
    }
}
