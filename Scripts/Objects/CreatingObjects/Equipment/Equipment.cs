public class Equipment
{
    public EquipmentType Type { get; private set; }
    public int Level { get; private set; }
    public int BonusHealth { get; private set; }

    public Equipment(EquipmentConfig config)
    {
        Apply(config);
    }

    public void Upgrade(EquipmentConfig config) => Apply(config);

    public void Restore(EquipmentConfig config) => Apply(config);

    private void Apply(EquipmentConfig config)
    {
        Type = config.Type;
        Level = config.Level;
        BonusHealth = config.BonusHealth;
    }
}
