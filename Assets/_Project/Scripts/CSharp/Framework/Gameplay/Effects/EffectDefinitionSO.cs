using System.Collections.Generic;
using BehaviorEditor;
using UnityEngine;

/// <summary>
/// 一次效果申请请求。行为层只负责发请求，不直接执行具体效果逻辑。
/// </summary>
public readonly struct EffectApplyRequest
{
    public EffectDefinitionSO Definition { get; }
    public UnitEffectController SourceController { get; }
    public UnitEffectController ExplicitTargetController { get; }
    public Vector3 Origin { get; }

    public EffectApplyRequest(EffectDefinitionSO definition, UnitEffectController sourceController,
        UnitEffectController explicitTargetController, Vector3 origin)
    {
        Definition = definition;
        SourceController = sourceController;
        ExplicitTargetController = explicitTargetController;
        Origin = origin;
    }
}

/// <summary>
/// 效果运行调度信息。
/// </summary>
public readonly struct EffectSchedule
{
    public static readonly EffectSchedule Instant = new EffectSchedule(0f, 0, 0f, false);

    public float Duration { get; }
    public int TickCount { get; }
    public float TickInterval { get; }
    public bool TickOnStart { get; }

    public EffectSchedule(float duration, int tickCount, float tickInterval, bool tickOnStart)
    {
        Duration = Mathf.Max(0f, duration);
        TickCount = Mathf.Max(0, tickCount);
        TickInterval = Mathf.Max(0f, tickInterval);
        TickOnStart = tickOnStart;
    }
}

/// <summary>
/// 构建某个目标上的运行时效果实例时使用的上下文。
/// </summary>
public readonly struct EffectBuildContext
{
    public GlobalEffectSystem System { get; }
    public EffectApplyRequest Request { get; }
    public UnitEffectController TargetController { get; }

    public EffectBuildContext(GlobalEffectSystem system, in EffectApplyRequest request, UnitEffectController targetController)
    {
        System = system;
        Request = request;
        TargetController = targetController;
    }
}

/// <summary>
/// 效果执行基类。后续自定义回血、护盾、灼烧、减速等效果都应继承它。
/// </summary>
public abstract class EffectDefinitionSO : BehaviorEffectAsset
{
    public virtual string EffectKey => name;
    public virtual bool ShouldDisplayOnTarget => true;

    public virtual void ResolveTargets(GlobalEffectSystem system, in EffectApplyRequest request,
        List<UnitEffectController> results)
    {
        if (results == null)
            return;

        UnitEffectController target = request.ExplicitTargetController != null
            ? request.ExplicitTargetController
            : request.SourceController;

        AddTargetIfValid(results, target);
    }

    public virtual EffectSchedule BuildSchedule(in EffectBuildContext context)
    {
        return EffectSchedule.Instant;
    }

    public virtual void OnEffectStarted(EffectRuntimeContext context)
    {
    }

    public virtual void OnEffectUpdated(EffectRuntimeContext context)
    {
    }

    public virtual void OnEffectTick(EffectRuntimeContext context)
    {
    }

    public virtual void OnEffectEnded(EffectRuntimeContext context)
    {
    }

    protected static void AddTargetIfValid(List<UnitEffectController> results, UnitEffectController target)
    {
        if (results == null || target == null)
            return;

        for (int i = 0; i < results.Count; i++)
        {
            if (results[i] == target)
                return;
        }

        results.Add(target);
    }
}
