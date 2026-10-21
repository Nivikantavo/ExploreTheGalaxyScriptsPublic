using UnityEngine;

[CreateAssetMenu(fileName = "EnemyInitConfig", menuName = "Scriptable Objects/EnemyInitConfig")]
public class EnemyInitConfig : ScriptableObject
{
    public Enemy EnemyPrefab => _enemyPrefab;
    public InstrumentConfig InstrumentConfig => _instrumentConfig;
    public int MaxHealth => _maxHealth;
    public ResourseConfig RecourseConfig => _resourseConfig;
    public int ResourceCount => _resourceCount;

    [SerializeField] private Enemy _enemyPrefab;
    [SerializeField] private InstrumentConfig _instrumentConfig;
    [SerializeField] private int _maxHealth;
    [SerializeField] private ResourseConfig _resourseConfig;
    [SerializeField] private int _resourceCount;
}
