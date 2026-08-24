using UnityEngine;

/// <summary>
/// 单位驱动基类。
/// 负责缓存 StatusData、索敌提供器等共享组件，并向 StatusData 提供初始化快照。
/// </summary>
public interface IUnitResourceSet
{
    float CurrentEnergy { get; }
    float MaxEnergy { get; }
    bool TryConsume(float amount);
    void Gain(float amount);
    void Reset(float maxEnergy);
}

public abstract class UnitDriverBase : MonoBehaviour
{
    [Header("Unit Base")]
    [SerializeField, Tooltip("实现 IUnitTargetingProvider 的索敌提供器组件。留空时会在初始化时自动查找并缓存一次。")]
    private MonoBehaviour unitTargetingProviderSource;

    protected StatusData statusData;
    protected UnitEffectController effectController;
    private IUnitTargetingProvider _defaultUnitTargetingProvider;
    private IUnitTargetingProvider _overrideUnitTargetingProvider;

    public StatusData StatusData => statusData;
    public UnitEffectController EffectController => effectController;
    public IUnitTargetingProvider UnitTargetingProvider { get; private set; }
    public virtual IUnitResourceSet Resources => null;

    protected virtual void Awake()
    {
        CacheSharedBindings();
    }

    protected virtual void OnValidate()
    {
        if (Application.isPlaying)
            return;

        CacheStatusDataReference();
    }

    public void BindStatusData(StatusData data, bool refreshImmediately = true)
    {
        statusData = data;
        if (statusData == null)
            return;

        statusData.AssignDriver(this, refreshImmediately);
    }

    public void RefreshStatusData(bool preserveHealthRatio = true, bool resetModifiers = false)
    {
        statusData?.RefreshFromDriver(preserveHealthRatio, resetModifiers);
    }

    public void SetUnitTargetingProviderOverride(IUnitTargetingProvider targetingProvider)
    {
        _overrideUnitTargetingProvider = targetingProvider;
        ApplyResolvedUnitTargetingProvider();
        OnUnitTargetingProviderChanged();
    }

    public void ClearUnitTargetingProviderOverride()
    {
        _overrideUnitTargetingProvider = null;
        ApplyResolvedUnitTargetingProvider();
        OnUnitTargetingProviderChanged();
    }

    public virtual bool TryResolveNumericValue(string numericKey, out float value)
    {
        value = 0f;
        return false;
    }

    public abstract bool TryBuildStatusSnapshot(out StatusDataSnapshot snapshot);

    protected void CacheSharedBindings()
    {
        CacheStatusDataReference();
        if (effectController == null)
            effectController = GetComponent<UnitEffectController>();
        _defaultUnitTargetingProvider = ResolveUnitTargetingProvider();
        ApplyResolvedUnitTargetingProvider();
        if (statusData != null)
        {
            BindStatusData(statusData);
        }
    }

    protected void CacheStatusDataReference()
    {
        if (statusData == null)
            statusData = GetComponent<StatusData>();
    }

    /// <summary>
    /// 当前主回调口。单位索敌提供器重新绑定后会通过这里通知派生类。
    /// </summary>
    protected virtual void OnUnitTargetingProviderChanged()
    {
    }

    private IUnitTargetingProvider ResolveUnitTargetingProvider()
    {
        if (unitTargetingProviderSource != null)
        {
            if (unitTargetingProviderSource is IUnitTargetingProvider typedProvider)
                return typedProvider;

            Debug.LogWarning(
                "unitTargetingProviderSource 没有实现 IUnitTargetingProvider，将改为自动重新查找索敌组件。",
                this);
            unitTargetingProviderSource = null;
        }

        MonoBehaviour[] selfBehaviours = GetComponents<MonoBehaviour>();
        for (int i = 0; i < selfBehaviours.Length; i++)
        {
            MonoBehaviour behaviour = selfBehaviours[i];
            if (behaviour != null && behaviour is IUnitTargetingProvider provider)
            {
                unitTargetingProviderSource = behaviour;
                return provider;
            }
        }

        MonoBehaviour[] childBehaviours = GetComponentsInChildren<MonoBehaviour>(true);
        for (int i = 0; i < childBehaviours.Length; i++)
        {
            MonoBehaviour behaviour = childBehaviours[i];
            if (behaviour == null || behaviour == this)
                continue;

            if (behaviour is IUnitTargetingProvider provider)
            {
                unitTargetingProviderSource = behaviour;
                return provider;
            }
        }

        return null;
    }

    private void ApplyResolvedUnitTargetingProvider()
    {
        UnitTargetingProvider = _overrideUnitTargetingProvider ?? _defaultUnitTargetingProvider;
        if (statusData != null)
            statusData.SetUnitTargetingProvider(UnitTargetingProvider);
    }
}
