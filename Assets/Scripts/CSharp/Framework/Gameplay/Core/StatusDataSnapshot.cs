/// <summary>
/// 单位状态初始化快照。
/// 由驱动基类生成，交给 StatusData 进行运行时承载。
/// </summary>
public struct StatusDataSnapshot
{
    public bool hasFullStatus;
    public int unitId;
    public UnitAlignment unitAlignment;
    public int unitLevel;
    public float baseHealth;
    public float baseAttackPower;
    public float baseDefense;
    public float baseCritRate;
    public float baseCritDamage;
    public float baseDamageBonus;
    public float basePenetration;
    public int UnitLevel
    {
        readonly get => unitLevel;
        set => unitLevel = value;
    }
    public UnitAlignment UnitAlignment
    {
        readonly get => unitAlignment;
        set => unitAlignment = value;
    }

    public static StatusDataSnapshot CreateFallback(UnitAlignment alignment, float health, float defense)
    {
        return new StatusDataSnapshot
        {
            hasFullStatus = false,
            unitId = 0,
            unitAlignment = alignment,
            unitLevel = 1,
            baseHealth = health,
            baseAttackPower = 0f,
            baseDefense = defense,
            baseCritRate = 0f,
            baseCritDamage = 1f,
            baseDamageBonus = 0f,
            basePenetration = 0f,
        };
    }
}
