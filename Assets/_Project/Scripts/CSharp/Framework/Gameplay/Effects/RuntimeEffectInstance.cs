using UnityEngine;

/// <summary>
/// 单个运行时效果实例，负责驱动开始、逐帧更新、定时 Tick 和结束。
/// </summary>
public sealed class RuntimeEffectInstance
{
    private readonly EffectSchedule _schedule;
    private bool _started;
    private int _completedTickCount;
    private float _nextTickTime;

    public int RuntimeId { get; }
    public EffectDefinitionSO Definition { get; }
    public UnitEffectController SourceController { get; }
    public UnitEffectController TargetController { get; }
    public EffectRuntimeContext Context { get; }
    public StatusData SourceData => Context.SourceData;
    public StatusData TargetData => Context.TargetData;
    public float ElapsedTime { get; private set; }
    public bool IsFinished { get; private set; }
    public int CompletedTickCount => _completedTickCount;
    public int TotalTickCount => _schedule.TickCount;
    public float RemainingDuration => Mathf.Max(0f, _schedule.Duration - ElapsedTime);
    public string DebugEffectKey => Definition != null ? Definition.EffectKey : string.Empty;

    public RuntimeEffectInstance(int runtimeId, EffectDefinitionSO definition, in EffectApplyRequest request,
        UnitEffectController targetController, in EffectSchedule schedule, GlobalEffectSystem system)
    {
        RuntimeId = runtimeId;
        Definition = definition;
        SourceController = request.SourceController;
        TargetController = targetController;
        _schedule = schedule;
        Context = new EffectRuntimeContext(system, this, definition, request.SourceController, targetController, request.Origin);
        ElapsedTime = 0f;
        IsFinished = false;
        _started = false;
        _completedTickCount = 0;
        _nextTickTime = _schedule.TickInterval;
    }

    public void Begin()
    {
        if (_started || IsFinished || Definition == null)
            return;

        _started = true;
        Context.ElapsedTime = 0f;
        Context.DeltaTime = 0f;
        Definition.OnEffectStarted(Context);

        if (_schedule.TickCount > 0 && _schedule.TickOnStart)
        {
            ExecuteTick();

            if (_schedule.TickInterval <= 0f)
            {
                while (_completedTickCount < _schedule.TickCount && !IsFinished)
                    ExecuteTick();
            }
        }

        if (ShouldFinish())
            Finish();
    }

    public void Tick(float deltaTime)
    {
        if (!_started || IsFinished || Definition == null)
            return;

        float safeDeltaTime = Mathf.Max(0f, deltaTime);
        ElapsedTime += safeDeltaTime;
        Context.ElapsedTime = ElapsedTime;
        Context.DeltaTime = safeDeltaTime;

        Definition.OnEffectUpdated(Context);

        if (_schedule.TickCount > _completedTickCount && _schedule.TickInterval > 0f)
        {
            while (_completedTickCount < _schedule.TickCount && ElapsedTime + 0.0001f >= _nextTickTime)
            {
                ExecuteTick();
                _nextTickTime += _schedule.TickInterval;
            }
        }

        if (ShouldFinish())
            Finish();
    }

    public void ForceFinish()
    {
        if (IsFinished)
            return;

        Finish();
    }

    private void ExecuteTick()
    {
        if (Definition == null || IsFinished || _completedTickCount >= _schedule.TickCount)
            return;

        _completedTickCount++;
        Context.CurrentTickIndex = _completedTickCount - 1;
        Definition.OnEffectTick(Context);
    }

    private bool ShouldFinish()
    {
        bool ticksCompleted = _completedTickCount >= _schedule.TickCount;
        if (_schedule.Duration <= 0f)
            return ticksCompleted;

        return ElapsedTime >= _schedule.Duration && ticksCompleted;
    }

    private void Finish()
    {
        if (IsFinished)
            return;

        IsFinished = true;
        Definition?.OnEffectEnded(Context);
    }
}
