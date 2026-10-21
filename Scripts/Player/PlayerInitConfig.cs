using UnityEngine;
using UnityEngine.Serialization;

[CreateAssetMenu(fileName = "PlayerInitConfig", menuName = "Scriptable Objects/PlayerInitConfig")]
public class PlayerInitConfig : ScriptableObject
{
    public int MaxHealth => _maxHealth;
    public float HealthRegenDelaySeconds => _healthRegenDelaySeconds;
    public float HealthRegenPercentPerSecond => _healthRegenPercentPerSecond;

    [SerializeField] private int _maxHealth;
    [SerializeField, Min(0f)] private float _healthRegenDelaySeconds = 5f;
    [FormerlySerializedAs("_healthRegenPerSecond")]
    [SerializeField, Range(0f, 100f), Tooltip("Процент от текущего максимального здоровья, восстанавливаемый за секунду.")]
    private float _healthRegenPercentPerSecond = 10f;
}
