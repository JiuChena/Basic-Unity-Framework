using UnityEngine;

/// <summary>
/// 运行时效果回调上下文，供效果逻辑读取施法者、目标和数值解析能力。
/// </summary>
public sealed class EffectRuntimeContext
{
    public GlobalEffectSystem System { get; }
    public RuntimeEffectInstance Instance { get; }
    public EffectDefinitionSO Definition { get; }
    public UnitEffectController SourceController { get; }
    public UnitEffectController TargetController { get; }
    public StatusData SourceData { get; }
    public StatusData TargetData { get; }
    public UnitDriverBase SourceDriver { get; }
    public UnitDriverBase TargetDriver { get; }
    public GameObject SourceObject => SourceController != null ? SourceController.gameObject : null;
    public GameObject TargetObject => TargetController != null ? TargetController.gameObject : null;
    public Vector3 Origin { get; }

    public float ElapsedTime { get; internal set; }
    public float DeltaTime { get; internal set; }
    public int CurrentTickIndex { get; internal set; }

    public EffectRuntimeContext(GlobalEffectSystem system, RuntimeEffectInstance instance, EffectDefinitionSO definition,
        UnitEffectController sourceController, UnitEffectController targetController, Vector3 origin)
    {
        System = system;
        Instance = instance;
        Definition = definition;
        SourceController = sourceController;
        TargetController = targetController;
        SourceData = sourceController != null ? sourceController.DataPanel : null;
        TargetData = targetController != null ? targetController.DataPanel : null;
        SourceDriver = sourceController != null ? sourceController.Driver : null;
        TargetDriver = targetController != null ? targetController.Driver : null;
        Origin = origin;
        ElapsedTime = 0f;
        DeltaTime = 0f;
        CurrentTickIndex = 0;
    }

    public float ResolveNumericValue(string numericKey, float fallbackValue)
    {
        if (SourceDriver == null || string.IsNullOrWhiteSpace(numericKey))
            return fallbackValue;

        return SourceDriver.TryResolveNumericValue(numericKey, out float resolvedValue) ? resolvedValue : fallbackValue;
    }

    public float GetSourceStat(GameplayEffectScalingStat scalingStat)
    {
        switch (scalingStat)
        {
            case GameplayEffectScalingStat.Attack:
                return SourceData != null ? SourceData.AttackPower : 0f;

            case GameplayEffectScalingStat.Defense:
                return SourceData != null ? SourceData.Defense : 0f;

            case GameplayEffectScalingStat.MaxHealth:
                return SourceData != null ? SourceData.MaxHealth : 0f;

            case GameplayEffectScalingStat.CurrentHealth:
                return SourceData != null ? SourceData.CurrentHealth : 0f;

            case GameplayEffectScalingStat.MaxEnergy:
                return SourceDriver != null && SourceDriver.Resources != null ? SourceDriver.Resources.MaxEnergy : 0f;

            case GameplayEffectScalingStat.CurrentEnergy:
                return SourceDriver != null && SourceDriver.Resources != null ? SourceDriver.Resources.CurrentEnergy : 0f;

            default:
                return 0f;
        }
    }

    public bool TryApplyNestedEffect(EffectDefinitionSO definition)
    {
        if (System == null || definition == null || TargetController == null)
            return false;

        EffectApplyRequest request = new EffectApplyRequest(definition, SourceController, TargetController, Origin);
        return System.ApplyEffect(request);
    }
}
