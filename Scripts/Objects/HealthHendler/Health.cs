using UnityEngine;
using UnityEngine.Events;

public class Health
{
    public float MaxHealth => _maxHealth;
    public float CurrentHealth => _currentHealth;

    public event UnityAction OnZeroHealth;

    private float _maxHealth;
    private float _currentHealth;

    public Health(float maxHealth)
    {
        _maxHealth = maxHealth;
        _currentHealth = _maxHealth;
    }

    public void TakeDamage(float damage)
    {
        if(damage <= 0) return;

        _currentHealth = Mathf.Max(0f, _currentHealth - damage);
        if(_currentHealth <= 0)
        {
            OnZeroHealth?.Invoke();
        }
    }

    public void RestoreHealth(float restoreHealth)
    {
        if (restoreHealth <= 0) return;

        _currentHealth += restoreHealth;
        _currentHealth = Mathf.Clamp(_currentHealth, 0, _maxHealth);
    }

    public void RestoreHealth()
    {
        _currentHealth = _maxHealth;
    }

    public void SetMaxHealth(float newMaxHealth)
    {
        if (newMaxHealth <= 0f)
            return;

        float delta = newMaxHealth - _maxHealth;
        _maxHealth = newMaxHealth;

        if (delta > 0f)
            _currentHealth += delta;
        else
            _currentHealth = Mathf.Min(_currentHealth, _maxHealth);
    }
}
