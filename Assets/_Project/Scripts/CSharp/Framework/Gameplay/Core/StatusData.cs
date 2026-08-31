using BehaviorEditor;
using Core.Gear;
using UnityEngine;
using UnityEngine.Serialization;

/// <summary>
/// 通用单位状态数据承载器。
/// 支持玩家、敌人和中立单位；优先向 UnitDriverBase 请求完整状态，缺失时退回最小兜底状态。
/// </summary>
public interface IUnitCombatProxyProvider
{
    StatusData ResolveCombatStatusData();
    IBehaviorUnit ResolvebehaviorUnit();
    IDamageable ResolveDamageable();
    UnitEffectController ResolveEffectController();
}

/// <summary>
/// 统一解析碰撞体背后的真实战斗单位。
/// 先查直接挂载的 StatusData，再查代理提供器，避免 Player 根节点这类“碰撞体代理”对象丢失战斗数据。
/// </summary>
public static class UnitCombatResolver
{
    public static bool TryResolveStatusData(Component source, out StatusData statusData, out bool canCache)
    {
        canCache = false;
        statusData = null;
        if (source == null)
            return false;

        statusData = source.GetComponentInParent<StatusData>();
        if (statusData != null)
        {
            canCache = true;
            return true;
        }

        IUnitCombatProxyProvider proxyProvider = source.GetComponentInParent<IUnitCombatProxyProvider>();
        if (proxyProvider == null)
            return false;

        statusData = proxyProvider.ResolveCombatStatusData();
        return statusData != null;
    }

    public static StatusData ResolveStatusData(Component source)
    {
        return TryResolveStatusData(source, out StatusData statusData, out _) ? statusData : null;
    }

    public static bool TryResolvebehaviorUnit(Component source, out IBehaviorUnit behaviorUnit, out bool canCache)
    {
        canCache = false;
        behaviorUnit = null;
        if (source == null)
            return false;

        if (TryResolveStatusData(source, out StatusData statusData, out canCache))
        {
            behaviorUnit = statusData;
            return true;
        }

        IUnitCombatProxyProvider proxyProvider = source.GetComponentInParent<IUnitCombatProxyProvider>();
        if (proxyProvider == null)
            return false;

        behaviorUnit = proxyProvider.ResolvebehaviorUnit();
        return behaviorUnit != null;
    }

    public static IDamageable ResolveDamageable(Component source)
    {
        if (source == null)
            return null;

        if (TryResolveStatusData(source, out StatusData statusData, out _))
            return statusData;

        IUnitCombatProxyProvider proxyProvider = source.GetComponentInParent<IUnitCombatProxyProvider>();
        return proxyProvider != null ? proxyProvider.ResolveDamageable() : null;
    }

    public static UnitEffectController ResolveEffectController(Component source)
    {
        if (source == null)
            return null;

        if (TryResolveStatusData(source, out StatusData statusData, out _))
        {
            UnitEffectController controller = statusData.GetComponent<UnitEffectController>();
            if (controller != null)
                return controller;

            return statusData.gameObject.AddComponent<UnitEffectController>();
        }

        IUnitCombatProxyProvider proxyProvider = source.GetComponentInParent<IUnitCombatProxyProvider>();
        return proxyProvider != null ? proxyProvider.ResolveEffectController() : null;
    }
}

[DisallowMultipleComponent]
public class StatusData : MonoBehaviour, IBehaviorUnit, IDamageable
{
    [Header("Targeting")]
    [SerializeField, Tooltip("为 true 时，该单位可以被索敌并作为有效受击目标；为 false 时会被所有攻击与索敌忽略。")]
    private bool isTargetable = true;

    [Header("Fallback")]
    [SerializeField, Tooltip("当物体上不存在继承自 UnitDriverBase 的驱动时使用的兜底生命值。")]
    private float fallbackMaxHealth = 100f;

    [SerializeField, Tooltip("当物体上不存在继承自 UnitDriverBase 的驱动时使用的兜底防御力。")]
    private float fallbackDefense = 2f;

    [SerializeField, Tooltip("当物体上不存在继承自 UnitDriverBase 的驱动时使用的兜底阵营。默认中立；若该物体本身就是可被攻击目标，可按需改成敌对阵营。")]
    [FormerlySerializedAs("fallbackAlignment")]
    private UnitAlignment fallbackUnitAlignment = UnitAlignment.Neutral;

    [Space(8)]
    [Header("Runtime Debug")]
    [SerializeField, Tooltip("当前是否已绑定驱动，仅用于 Inspector 调试观察")]
    private bool runtimeHasDriver;

    [SerializeField, Tooltip("当前是否使用了完整状态快照，仅用于 Inspector 调试观察")]
    private bool runtimeHasFullStatus;

    [SerializeField, Tooltip("运行时单位 ID，仅用于 Inspector 调试观察")]
    private int runtimeUnitId;

    [SerializeField, Tooltip("运行时单位阵营，仅用于 Inspector 调试观察")]
    [FormerlySerializedAs("runtimeAlignment")]
    private UnitAlignment runtimeUnitAlignment;

    [SerializeField, Tooltip("运行时等级，仅用于 Inspector 调试观察")]
    private int runtimeLevel;

    [SerializeField, Tooltip("运行时最大生命值，仅用于 Inspector 调试观察")]
    private float runtimeMaxHealth;

    [SerializeField, Tooltip("运行时基础生命值，仅用于 Inspector 调试观察")]
    private float runtimeBaseHealth;

    [SerializeField, Tooltip("运行时当前生命值，仅用于 Inspector 调试观察")]
    private float runtimeCurrentHealth;

    [SerializeField, Tooltip("运行时攻击力，仅用于 Inspector 调试观察")]
    private float runtimeAttackPower;

    [SerializeField, Tooltip("运行时基础攻击力，仅用于 Inspector 调试观察")]
    private float runtimeBaseAttackPower;

    [SerializeField, Tooltip("运行时防御力，仅用于 Inspector 调试观察")]
    private float runtimeDefense;

    [SerializeField, Tooltip("运行时基础防御力，仅用于 Inspector 调试观察")]
    private float runtimeBaseDefense;

    [SerializeField, Tooltip("运行时暴击率，仅用于 Inspector 调试观察")]
    private float runtimeCritRate;

    [SerializeField, Tooltip("运行时基础暴击率，仅用于 Inspector 调试观察")]
    private float runtimeBaseCritRate;

    [SerializeField, Tooltip("运行时暴击伤害倍率，仅用于 Inspector 调试观察")]
    private float runtimeCritDamage;

    [SerializeField, Tooltip("运行时基础暴击伤害倍率，仅用于 Inspector 调试观察")]
    private float runtimeBaseCritDamage;

    [SerializeField, Tooltip("运行时伤害提升倍率，仅用于 Inspector 调试观察")]
    private float runtimeDamageBonus;

    [SerializeField, Tooltip("运行时基础伤害提升倍率，仅用于 Inspector 调试观察")]
    private float runtimeBaseDamageBonus;

    [SerializeField, Tooltip("运行时穿透加成，仅用于 Inspector 调试观察")]
    private float runtimePenetration;

    [SerializeField, Tooltip("运行时基础穿透加成，仅用于 Inspector 调试观察")]
    private float runtimeBasePenetration;

    [SerializeField, Tooltip("运行时生命百分比修正，仅用于 Inspector 调试观察")]
    private float runtimeHealthPercentModifier;

    [SerializeField, Tooltip("运行时攻击百分比修正，仅用于 Inspector 调试观察")]
    private float runtimeAttackPercentModifier;

    [SerializeField, Tooltip("运行时防御百分比修正，仅用于 Inspector 调试观察")]
    private float runtimeDefensePercentModifier;

    [SerializeField, Tooltip("运行时生命固定修正，仅用于 Inspector 调试观察")]
    private float runtimeHealthFlatModifier;

    [SerializeField, Tooltip("运行时攻击修正值，仅用于 Inspector 调试观察")]
    private float runtimeAttackFlatModifier;

    [SerializeField, Tooltip("运行时防御修正值，仅用于 Inspector 调试观察")]
    private float runtimeDefenseFlatModifier;

    [SerializeField, Tooltip("运行时免伤百分比修正，仅用于 Inspector 调试观察")]
    private float runtimeDamageReductionPercentModifier;

    [SerializeField, Tooltip("运行时是否已死亡，仅用于 Inspector 调试观察")]
    private bool runtimeIsDead;

    [SerializeField, Tooltip("运行时当前是否允许被索敌和受击，仅用于 Inspector 调试观察")]
    private bool runtimeIsTargetable = true;

    public UnitDriverBase Driver { get; private set; }
    public IUnitTargetingProvider UnitTargetingProvider { get; private set; }
    public bool HasFullStatus { get; private set; }
    public bool UsesFallbackStatus => Driver == null || !HasFullStatus;
    public int UnitId { get; private set; }
    public int UnitLevel { get; private set; } = 1;
    public bool IsTargetable => isTargetable;
    public UnitAlignment UnitAlignment { get; private set; }
    public float BaseHealth { get; private set; }
    public float MaxHealth { get; private set; }
    public float CurrentHealth { get; private set; }
    public float BaseAttackPower { get; private set; }
    public float BaseDefense { get; private set; }
    public float AttackPower { get; private set; }
    public float Defense { get; private set; }
    public float BaseCritRate { get; private set; }
    public float CritRate { get; private set; }
    public float BaseCritDamage { get; private set; }
    public float CritDamage { get; private set; }
    public float BaseDamageBonus { get; private set; }
    public float DamageBonus { get; private set; }
    public float BasePenetration { get; private set; }
    public float Penetration { get; private set; }
    public float DamageReductionPercent => Mathf.Clamp01(_damageReductionPercentModifier);
    public bool IsDead { get; private set; }

    bool IDamageable.IsAlive => !IsDead;
    int IBehaviorUnit.UnitId => UnitId;
    GameObject IBehaviorUnit.RuntimeGameObject => gameObject;
    Transform IBehaviorUnit.RuntimeTransform => transform;
    string IBehaviorUnit.DebugName => name;

    private float _healthPercentModifier;
    private float _attackPercentModifier;
    private float _defensePercentModifier;
    private float _healthFlatModifier;
    private float _attackFlatModifier;
    private float _defenseFlatModifier;
    private float _critRateModifier;
    private float _critDamageModifier;
    private float _damageBonusModifier;
    private float _penetrationModifier;
    private float _damageReductionPercentModifier;

    protected virtual void Awake()
    {
        TryResolveDriver();
        if (Driver != null)
            RefreshFromDriver(false, true);
        else
            ApplyFallbackSnapshot();
    }

    public void AssignDriver(UnitDriverBase driver, bool refreshImmediately = true)
    {
        Driver = driver;
        runtimeHasDriver = Driver != null;

        if (Driver != null)
            SetUnitTargetingProvider(Driver.UnitTargetingProvider);

        if (refreshImmediately)
            RefreshFromDriver(false, true);
    }

    public void SetUnitTargetingProvider(IUnitTargetingProvider targetingProvider)
    {
        UnitTargetingProvider = targetingProvider;
    }

    public void RefreshFromDriver(bool preserveHealthRatio = true, bool resetModifiers = false)
    {
        if (Driver == null)
        {
            ApplyFallbackSnapshot();
            return;
        }

        if (!Driver.TryBuildStatusSnapshot(out StatusDataSnapshot snapshot))
        {
            ApplyFallbackSnapshot();
            return;
        }

        ApplySnapshot(snapshot, preserveHealthRatio, resetModifiers);
    }

    public void ReceiveDamage(float damage, Vector3 knockback, float hitStunDuration, GameObject source)
    {
        if (IsDead || !IsTargetable)
            return;

        float finalDamage = Mathf.Max(0f, damage) * (1f - Mathf.Clamp01(_damageReductionPercentModifier));
        CurrentHealth = Mathf.Max(0f, CurrentHealth - finalDamage);
        if (CurrentHealth <= 0f)
        {
            IsDead = true;
            EventCenter.Instance.SetEventTrigger(GameplayEventNames.UnitDeath, gameObject);
            SyncRuntimeDebugFields();
            return;
        }

        SyncRuntimeDebugFields();
    }

    public float RestoreHealth(float amount)
    {
        if (IsDead || amount <= 0f)
            return 0f;

        float beforeHealth = CurrentHealth;
        CurrentHealth = Mathf.Min(MaxHealth, CurrentHealth + amount);
        SyncRuntimeDebugFields();
        return CurrentHealth - beforeHealth;
    }

    public void AddStatModifier(float attackBonus, float defenseBonus)
    {
        _attackFlatModifier += attackBonus;
        _defenseFlatModifier += defenseBonus;
        RecalculateDerivedStats();
        SyncRuntimeDebugFields();
    }

    public void RemoveStatModifier(float attackBonus, float defenseBonus)
    {
        _attackFlatModifier -= attackBonus;
        _defenseFlatModifier -= defenseBonus;
        RecalculateDerivedStats();
        SyncRuntimeDebugFields();
    }

    public void AddPrimaryStatPercentModifier(float healthPercentBonus, float attackPercentBonus, float defensePercentBonus)
    {
        _healthPercentModifier += healthPercentBonus;
        _attackPercentModifier += attackPercentBonus;
        _defensePercentModifier += defensePercentBonus;
        RecalculateDerivedStats();
        SyncRuntimeDebugFields();
    }

    public void RemovePrimaryStatPercentModifier(float healthPercentBonus, float attackPercentBonus, float defensePercentBonus)
    {
        _healthPercentModifier -= healthPercentBonus;
        _attackPercentModifier -= attackPercentBonus;
        _defensePercentModifier -= defensePercentBonus;
        RecalculateDerivedStats();
        SyncRuntimeDebugFields();
    }

    public void AddPrimaryStatFlatModifier(float healthFlatBonus, float attackFlatBonus, float defenseFlatBonus)
    {
        _healthFlatModifier += healthFlatBonus;
        _attackFlatModifier += attackFlatBonus;
        _defenseFlatModifier += defenseFlatBonus;
        RecalculateDerivedStats();
        SyncRuntimeDebugFields();
    }

    public void RemovePrimaryStatFlatModifier(float healthFlatBonus, float attackFlatBonus, float defenseFlatBonus)
    {
        _healthFlatModifier -= healthFlatBonus;
        _attackFlatModifier -= attackFlatBonus;
        _defenseFlatModifier -= defenseFlatBonus;
        RecalculateDerivedStats();
        SyncRuntimeDebugFields();
    }

    public void AddSecondaryStatModifier(float critRateBonus, float critDamageBonus, float damageBonus, float penetrationBonus)
    {
        _critRateModifier += critRateBonus;
        _critDamageModifier += critDamageBonus;
        _damageBonusModifier += damageBonus;
        _penetrationModifier += penetrationBonus;
        RecalculateDerivedStats();
        SyncRuntimeDebugFields();
    }

    public void RemoveSecondaryStatModifier(float critRateBonus, float critDamageBonus, float damageBonus, float penetrationBonus)
    {
        _critRateModifier -= critRateBonus;
        _critDamageModifier -= critDamageBonus;
        _damageBonusModifier -= damageBonus;
        _penetrationModifier -= penetrationBonus;
        RecalculateDerivedStats();
        SyncRuntimeDebugFields();
    }

    public void AddDamageReductionModifier(float damageReductionPercentBonus)
    {
        _damageReductionPercentModifier += damageReductionPercentBonus;
        SyncRuntimeDebugFields();
    }

    public void RemoveDamageReductionModifier(float damageReductionPercentBonus)
    {
        _damageReductionPercentModifier -= damageReductionPercentBonus;
        SyncRuntimeDebugFields();
    }

    [ContextMenu("Log Runtime Debug Snapshot")]
    private void LogRuntimeDebugSnapshot()
    {
        Debug.Log(
            $"[{name}] Runtime Debug | UnitId={runtimeUnitId} | Alignment={runtimeUnitAlignment} | HP={runtimeCurrentHealth:F1}/{runtimeMaxHealth:F1} | ATK={runtimeAttackPower:F1} | DEF={runtimeDefense:F1} | Dead={runtimeIsDead} | Targetable={runtimeIsTargetable} | Driver={runtimeHasDriver} | FullStatus={runtimeHasFullStatus}",
            this);
    }

    [ContextMenu("Apply 10 Debug Damage")]
    private void ApplyDebugDamage()
    {
        ReceiveDamage(10f, Vector3.zero, 0.15f, gameObject);
        Debug.Log($"[{name}] 已施加 10 点调试伤害。", this);
    }

    private void TryResolveDriver()
    {
        if (Driver != null)
            return;

        UnitDriverBase driver = GetComponent<UnitDriverBase>();
        if (driver == null)
            driver = GetComponentInParent<UnitDriverBase>();

        if (driver != null)
            AssignDriver(driver, refreshImmediately: false);
    }

    private void ApplyFallbackSnapshot()
    {
        ApplySnapshot(StatusDataSnapshot.CreateFallback(
            fallbackUnitAlignment,
            Mathf.Max(1f, fallbackMaxHealth),
            Mathf.Max(0f, fallbackDefense)), false, true);
    }

    private void ApplySnapshot(StatusDataSnapshot snapshot, bool preserveHealthRatio, bool resetModifiers)
    {
        float previousHealthRatio = MaxHealth > 0f ? CurrentHealth / MaxHealth : 1f;
        bool preserveExistingHealth = preserveHealthRatio && MaxHealth > 0f;
        bool wasDead = IsDead;

        HasFullStatus = snapshot.hasFullStatus;
        UnitId = snapshot.unitId;
        UnitLevel = Mathf.Max(1, snapshot.UnitLevel);
        UnitAlignment = snapshot.UnitAlignment;
        BaseHealth = Mathf.Max(1f, snapshot.baseHealth);
        BaseAttackPower = snapshot.hasFullStatus ? Mathf.Max(0f, snapshot.baseAttackPower) : 0f;
        BaseDefense = Mathf.Max(0f, snapshot.baseDefense);
        BaseCritRate = snapshot.hasFullStatus ? Mathf.Clamp01(snapshot.baseCritRate) : 0f;
        BaseCritDamage = snapshot.hasFullStatus ? Mathf.Max(1f, snapshot.baseCritDamage) : 1f;
        BaseDamageBonus = snapshot.hasFullStatus ? Mathf.Max(0f, snapshot.baseDamageBonus) : 0f;
        BasePenetration = snapshot.hasFullStatus ? Mathf.Clamp01(snapshot.basePenetration) : 0f;

        if (resetModifiers)
        {
            _healthPercentModifier = 0f;
            _attackPercentModifier = 0f;
            _defensePercentModifier = 0f;
            _healthFlatModifier = 0f;
            _attackFlatModifier = 0f;
            _defenseFlatModifier = 0f;
            _critRateModifier = 0f;
            _critDamageModifier = 0f;
            _damageBonusModifier = 0f;
            _penetrationModifier = 0f;
            _damageReductionPercentModifier = 0f;
        }

        RecalculateDerivedStats();

        if (wasDead)
        {
            CurrentHealth = 0f;
            IsDead = true;
        }
        else if (preserveExistingHealth)
        {
            CurrentHealth = Mathf.Clamp(MaxHealth * previousHealthRatio, 0f, MaxHealth);
            IsDead = CurrentHealth <= 0f;
        }
        else
        {
            CurrentHealth = MaxHealth;
            IsDead = false;
        }

        SyncRuntimeDebugFields();
    }

    private void RecalculateDerivedStats()
    {
        MaxHealth = Mathf.Max(1f, BaseHealth * (1f + _healthPercentModifier) + _healthFlatModifier);
        AttackPower = Mathf.Max(0f, BaseAttackPower * (1f + _attackPercentModifier) + _attackFlatModifier);
        Defense = Mathf.Max(0f, BaseDefense * (1f + _defensePercentModifier) + _defenseFlatModifier);
        CritRate = Mathf.Clamp01(BaseCritRate + _critRateModifier);
        CritDamage = Mathf.Max(1f, BaseCritDamage + _critDamageModifier);
        DamageBonus = Mathf.Max(0f, BaseDamageBonus + _damageBonusModifier);
        Penetration = Mathf.Clamp01(BasePenetration + _penetrationModifier);
        CurrentHealth = Mathf.Clamp(CurrentHealth, 0f, MaxHealth);
    }

    private void SyncRuntimeDebugFields()
    {
        runtimeHasDriver = Driver != null;
        runtimeHasFullStatus = HasFullStatus;
        runtimeUnitId = UnitId;
        runtimeUnitAlignment = UnitAlignment;
        runtimeLevel = UnitLevel;
        runtimeBaseHealth = BaseHealth;
        runtimeMaxHealth = MaxHealth;
        runtimeCurrentHealth = CurrentHealth;
        runtimeBaseAttackPower = BaseAttackPower;
        runtimeAttackPower = AttackPower;
        runtimeBaseDefense = BaseDefense;
        runtimeDefense = Defense;
        runtimeBaseCritRate = BaseCritRate;
        runtimeCritRate = CritRate;
        runtimeBaseCritDamage = BaseCritDamage;
        runtimeCritDamage = CritDamage;
        runtimeBaseDamageBonus = BaseDamageBonus;
        runtimeDamageBonus = DamageBonus;
        runtimeBasePenetration = BasePenetration;
        runtimePenetration = Penetration;
        runtimeHealthPercentModifier = _healthPercentModifier;
        runtimeAttackPercentModifier = _attackPercentModifier;
        runtimeDefensePercentModifier = _defensePercentModifier;
        runtimeHealthFlatModifier = _healthFlatModifier;
        runtimeAttackFlatModifier = _attackFlatModifier;
        runtimeDefenseFlatModifier = _defenseFlatModifier;
        runtimeDamageReductionPercentModifier = Mathf.Clamp01(_damageReductionPercentModifier);
        runtimeIsDead = IsDead;
        runtimeIsTargetable = isTargetable;
    }
}
